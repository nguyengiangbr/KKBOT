// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Numerics.Tensors;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using KKBOT.MLPlatform.Abstractions;
using KKBOT.Observability;
using KKBOT.Observability.Tracing;

namespace KKBOT.MLPlatform.ModelServer;

/// <summary>
/// High-performance ONNX model server with batching, caching, and auto-scaling support.
/// </summary>
public sealed class OnnxModelServer : IModelServer, IHostedService, IDisposable
{
    private readonly ILogger<OnnxModelServer> _logger;
    private readonly ModelServerOptions _options;
    private readonly IMetricsRegistry _metrics;
    private readonly ConcurrentDictionary<string, ModelSession> _modelSessions = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _modelLocks = new();
    private readonly Channel<BatchedInferenceRequest> _batchingChannel;
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly Task _batchingTask;
    private bool _disposed;

    public OnnxModelServer(
        ILogger<OnnxModelServer> logger,
        IOptions<ModelServerOptions> options,
        IMetricsRegistry metrics)
    {
        _logger = logger;
        _options = options.Value;
        _metrics = metrics;

        _batchingChannel = Channel.CreateBounded<BatchedInferenceRequest>(new BoundedChannelOptions(_options.MaxBatchQueueSize)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        _batchingTask = ProcessBatchesAsync(_shutdownCts.Token);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting ONNX Model Server");

        // Load configured models
        foreach (var modelConfig in _options.Models)
        {
            await LoadModelAsync(modelConfig, cancellationToken);
        }

        _logger.LogInformation("ONNX Model Server started with {Count} models", _modelSessions.Count);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping ONNX Model Server");

        _shutdownCts.Cancel();
        try
        {
            await _batchingTask;
        }
        catch (OperationCanceledException) { }

        foreach (var session in _modelSessions.Values)
        {
            session.Dispose();
        }
        _modelSessions.Clear();

        _logger.LogInformation("ONNX Model Server stopped");
    }

    public async Task<InferenceResponse> PredictAsync(InferenceRequest request, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var queueStopwatch = Stopwatch.StartNew();

        try
        {
            if (!_modelSessions.TryGetValue(request.ModelId, out var session))
            {
                return CreateErrorResponse(request, $"Model {request.ModelId} not found", stopwatch.Elapsed);
            }

            // Wait for model to be ready
            var modelLock = _modelLocks.GetOrAdd(request.ModelId, _ => new SemaphoreSlim(1, 1));
            await modelLock.WaitAsync(ct);
            try
            {
                if (!session.IsReady)
                {
                    return CreateErrorResponse(request, "Model not ready", stopwatch.Elapsed);
                }

                queueStopwatch.Stop();

                // Prepare inputs
                var inputs = PrepareInputs(request, session);
                var computeStopwatch = Stopwatch.StartNew();

                // Run inference
                var outputs = await RunInferenceAsync(session, inputs, ct);

                computeStopwatch.Stop();
                stopwatch.Stop();

                var response = new InferenceResponse
                {
                    RequestId = request.RequestId,
                    ModelId = request.ModelId,
                    ModelVersion = session.Metadata.Version,
                    Outputs = outputs,
                    Metrics = new InferenceMetrics
                    {
                        LatencyMs = stopwatch.Elapsed.TotalMilliseconds,
                        QueueTimeMs = queueStopwatch.Elapsed.TotalMilliseconds,
                        ComputeTimeMs = computeStopwatch.Elapsed.TotalMilliseconds,
                        MemoryUsedBytes = GC.GetTotalMemory(false),
                        BatchSize = 1
                    },
                    Success = true,
                    CompletedAt = DateTimeOffset.UtcNow
                };

                _metrics.RecordHttpRequestDuration($"inference_{request.ModelId}", stopwatch.Elapsed.TotalMilliseconds);
                return response;
            }
            finally
            {
                modelLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return CreateErrorResponse(request, "Request cancelled", stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Inference failed for model {ModelId}", request.ModelId);
            return CreateErrorResponse(request, ex.Message, stopwatch.Elapsed);
        }
    }

    public async IAsyncEnumerable<InferenceResponse> PredictStreamAsync(InferenceRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // For streaming, we can yield partial results for autoregressive models
        var response = await PredictAsync(request, ct);
        yield return response;
    }

    public Task<ModelHealth> GetHealthAsync(string modelId, CancellationToken ct = default)
    {
        if (!_modelSessions.TryGetValue(modelId, out var session))
        {
            return Task.FromResult(new ModelHealth
            {
                ModelId = modelId,
                Healthy = false,
                ReadyInstances = 0,
                TotalInstances = 0
            });
        }

        return Task.FromResult(new ModelHealth
        {
            ModelId = modelId,
            Healthy = session.IsReady,
            ReadyInstances = session.IsReady ? 1 : 0,
            TotalInstances = 1,
            AvgLatencyMs = session.AvgLatencyMs,
            ErrorRate = session.ErrorRate
        });
    }

    public Task<IReadOnlyList<ModelInstance>> GetInstancesAsync(string modelId, CancellationToken ct = default)
    {
        if (!_modelSessions.TryGetValue(modelId, out var session))
        {
            return Task.FromResult(Array.Empty<ModelInstance>());
        }

        return Task.FromResult<IReadOnlyList<ModelInstance>>(new[]
        {
            new ModelInstance
            {
                InstanceId = $"{modelId}-0",
                Host = Environment.MachineName,
                Port = _options.GrpcPort,
                Ready = session.IsReady,
                ActiveRequests = session.ActiveRequests,
                StartedAt = session.LoadedAt
            }
        });
    }

    private async Task LoadModelAsync(ModelConfig config, CancellationToken ct)
    {
        _logger.LogInformation("Loading model {ModelId} from {Path}", config.ModelId, config.ModelPath);

        var modelLock = _modelLocks.GetOrAdd(config.ModelId, _ => new SemaphoreSlim(1, 1));
        await modelLock.WaitAsync(ct);
        try
        {
            if (_modelSessions.ContainsKey(config.ModelId))
            {
                _logger.LogWarning("Model {ModelId} already loaded", config.ModelId);
                return;
            }

            var sessionOptions = CreateSessionOptions(config);
            var session = new InferenceSession(config.ModelPath, sessionOptions);

            var metadata = CreateMetadata(config, session);
            var modelSession = new ModelSession
            {
                Session = session,
                Metadata = metadata,
                InputNames = session.InputMetadata.Keys.ToArray(),
                OutputNames = session.OutputMetadata.Keys.ToArray(),
                IsReady = true,
                LoadedAt = DateTimeOffset.UtcNow
            };

            _modelSessions[config.ModelId] = modelSession;
            _logger.LogInformation("Model {ModelId} loaded successfully", config.ModelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load model {ModelId}", config.ModelId);
            throw;
        }
        finally
        {
            modelLock.Release();
        }
    }

    private SessionOptions CreateSessionOptions(ModelConfig config)
    {
        var options = new SessionOptions();

        // Execution provider
        if (config.UseGpu && _options.EnableGpu)
        {
            options.AppendExecutionProvider_CUDA(config.GpuDeviceId);
        }
        else
        {
            options.AppendExecutionProvider_CPU();
        }

        // Graph optimization
        options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;

        // Intra/inter op threading
        options.IntraOpNumThreads = config.IntraOpThreads > 0 ? config.IntraOpThreads : Environment.ProcessorCount;
        options.InterOpNumThreads = config.InterOpThreads > 0 ? config.InterOpThreads : 1;

        // Memory pattern
        options.EnableMemoryPattern = config.EnableMemoryPattern;
        options.EnableCpuMemArena = config.EnableCpuMemArena;

        // Profiling
        if (config.EnableProfiling)
        {
            options.EnableProfiling = true;
            options.ProfileFilePrefix = $"onnx_profile_{config.ModelId}";
        }

        return options;
    }

    private ModelMetadata CreateMetadata(ModelConfig config, InferenceSession session)
    {
        var inputs = session.InputMetadata.Select(kvp => new ModelInput
        {
            Name = kvp.Key,
            DataType = kvp.Value.ElementType.Name,
            Shape = kvp.Value.Dimensions.Select(d => (int)d).ToArray()
        }).ToArray();

        var outputs = session.OutputMetadata.Select(kvp => new ModelOutput
        {
            Name = kvp.Key,
            DataType = kvp.Value.ElementType.Name,
            Shape = kvp.Value.Dimensions.Select(d => (int)d).ToArray()
        }).ToArray();

        return new ModelMetadata
        {
            ModelId = config.ModelId,
            Name = config.Name,
            Version = config.Version,
            Type = ModelType.Onnx,
            Framework = ModelFramework.PyTorch,
            Description = config.Description,
            Signature = new ModelSignature { Inputs = inputs, Outputs = outputs },
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private Dictionary<string, float[]> PrepareInputs(InferenceRequest request, ModelSession session)
    {
        var inputs = new Dictionary<string, float[]>();

        foreach (var kvp in request.Inputs)
        {
            if (session.InputNames.Contains(kvp.Key))
            {
                inputs[kvp.Key] = kvp.Value;
            }
            else
            {
                _logger.LogWarning("Input {InputName} not found in model {ModelId}", kvp.Key, request.ModelId);
            }
        }

        // Add missing optional inputs with defaults
        foreach (var inputName in session.InputNames)
        {
            if (!inputs.ContainsKey(inputName))
            {
                var meta = session.Session.InputMetadata[inputName];
                var totalSize = meta.Dimensions.Aggregate(1L, (acc, dim) => acc * (dim == -1 ? 1 : dim));
                inputs[inputName] = new float[totalSize];
            }
        }

        return inputs;
    }

    private async Task<Dictionary<string, float[]>> RunInferenceAsync(ModelSession session, Dictionary<string, float[]> inputs, CancellationToken ct)
    {
        session.ActiveRequests++;
        try
        {
            var container = new List<NamedOnnxValue>();

            foreach (var kvp in inputs)
            {
                var meta = session.Session.InputMetadata[kvp.Key];
                var tensor = CreateTensor(meta, kvp.Value);
                container.Add(NamedOnnxValue.CreateFromTensor(kvp.Key, tensor));
            }

            var results = session.Session.Run(container);

            var outputs = new Dictionary<string, float[]>();
            foreach (var result in results)
            {
                if (result.Value is Tensor<float> tensor)
                {
                    outputs[result.Name] = tensor.ToArray();
                }
                result.Dispose();
            }

            // Update metrics
            session.UpdateMetrics(true);

            return outputs;
        }
        catch (Exception ex)
        {
            session.UpdateMetrics(false);
            throw;
        }
        finally
        {
            session.ActiveRequests--;
        }
    }

    private Tensor<float> CreateTensor(NodeMetadata meta, float[] data)
    {
        var dims = meta.Dimensions.Select(d => d == -1 ? 1 : (int)d).ToArray();
        return new DenseTensor<float>(data, dims);
    }

    private InferenceResponse CreateErrorResponse(InferenceRequest request, string error, TimeSpan elapsed)
    {
        return new InferenceResponse
        {
            RequestId = request.RequestId,
            ModelId = request.ModelId,
            ModelVersion = "unknown",
            Outputs = new Dictionary<string, float[]>(),
            Metrics = new InferenceMetrics
            {
                LatencyMs = elapsed.TotalMilliseconds,
                QueueTimeMs = 0,
                ComputeTimeMs = 0,
                MemoryUsedBytes = GC.GetTotalMemory(false),
                BatchSize = 1
            },
            Success = false,
            ErrorMessage = error,
            CompletedAt = DateTimeOffset.UtcNow
        };
    }

    private async Task ProcessBatchesAsync(CancellationToken ct)
    {
        var batch = new List<BatchedInferenceRequest>();

        while (!ct.IsCancellationRequested)
        {
            batch.Clear();

            // Wait for first request
            try
            {
                var request = await _batchingChannel.Reader.ReadAsync(ct);
                batch.Add(request);
            }
            catch (ChannelClosedException)
            {
                break;
            }

            // Collect more requests up to batch size
            var timeout = TimeSpan.FromMilliseconds(_options.BatchTimeoutMs);
            var deadline = DateTimeOffset.UtcNow + timeout;

            while (batch.Count < _options.MaxBatchSize && DateTimeOffset.UtcNow < deadline)
            {
                var remaining = deadline - DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero) break;

                try
                {
                    var request = await _batchingChannel.Reader.ReadAsync(remaining, ct);
                    batch.Add(request);
                }
                catch (ChannelClosedException)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            // Process batch
            if (batch.Count > 0)
            {
                await ProcessBatchAsync(batch, ct);
            }
        }
    }

    private async Task ProcessBatchAsync(List<BatchedInferenceRequest> batch, CancellationToken ct)
    {
        // Group by model
        var groups = batch.GroupBy(r => r.Request.ModelId).ToList();

        foreach (var group in groups)
        {
            if (!_modelSessions.TryGetValue(group.Key, out var session))
            {
                foreach (var req in group)
                {
                    req.CompletionSource.TrySetResult(CreateErrorResponse(req.Request, $"Model {group.Key} not found", TimeSpan.Zero));
                }
                continue;
            }

            // TODO: Implement true batching for models that support it
            // For now, process sequentially
            foreach (var req in group)
            {
                var response = await PredictAsync(req.Request, ct);
                req.CompletionSource.TrySetResult(response);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _shutdownCts.Cancel();
        _batchingChannel.Writer.Complete();
        _batchingTask.Wait(TimeSpan.FromSeconds(5));

        foreach (var session in _modelSessions.Values)
        {
            session.Dispose();
        }
        _modelSessions.Clear();
    }
}

/// <summary>
/// Internal model session state.
/// </summary>
internal sealed class ModelSession : IDisposable
{
    public InferenceSession Session { get; init; }
    public ModelMetadata Metadata { get; init; }
    public string[] InputNames { get; init; }
    public string[] OutputNames { get; init; }
    public bool IsReady { get; set; }
    public DateTimeOffset LoadedAt { get; init; }
    public int ActiveRequests { get; set; }
    public double AvgLatencyMs { get; private set; }
    public double ErrorRate { get; private set; }

    private long _totalRequests;
    private long _failedRequests;
    private double _totalLatencyMs;

    public void UpdateMetrics(bool success, double latencyMs = 0)
    {
        Interlocked.Increment(ref _totalRequests);
        if (!success) Interlocked.Increment(ref _failedRequests);
        if (latencyMs > 0) Interlocked.Add(ref _totalLatencyMs, (long)(latencyMs * 10000)); // ticks

        var total = _totalRequests;
        var failed = _failedRequests;
        AvgLatencyMs = total > 0 ? _totalLatencyMs / total / 10000.0 : 0;
        ErrorRate = total > 0 ? (double)failed / total : 0;
    }

    public void Dispose()
    {
        Session?.Dispose();
    }
}

/// <summary>
/// Batched inference request for throughput optimization.
/// </summary>
internal sealed class BatchedInferenceRequest
{
    public InferenceRequest Request { get; init; }
    public TaskCompletionSource<InferenceResponse> CompletionSource { get; init; } = new();
}

/// <summary>
/// Model server configuration.
/// </summary>
public sealed class ModelServerOptions
{
    public List<ModelConfig> Models { get; set; } = new();
    public int GrpcPort { get; set; } = 50051;
    public int HttpPort { get; set; } = 50052;
    public bool EnableGpu { get; set; } = true;
    public int MaxBatchSize { get; set; } = 32;
    public int BatchTimeoutMs { get; set; } = 10;
    public int MaxBatchQueueSize { get; set; } = 1000;
    public int DefaultIntraOpThreads { get; set; } = 0;
    public int DefaultInterOpThreads { get; set; } = 0;
}

public sealed class ModelConfig
{
    public string ModelId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string ModelPath { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool UseGpu { get; set; } = true;
    public int GpuDeviceId { get; set; } = 0;
    public int IntraOpThreads { get; set; } = 0;
    public int InterOpThreads { get; set; } = 0;
    public bool EnableMemoryPattern { get; set; } = true;
    public bool EnableCpuMemArena { get; set; } = true;
    public bool EnableProfiling { get; set; } = false;
}

/// <summary>
/// DI Extensions.
/// </summary>
public static class ModelServerExtensions
{
    public static IServiceCollection AddKkbotModelServer(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ModelServerOptions>(configuration.GetSection("ModelServer"));
        services.AddSingleton<IModelServer, OnnxModelServer>();
        services.AddHostedService(sp => sp.GetRequiredService<IModelServer>());
        return services;
    }
}
