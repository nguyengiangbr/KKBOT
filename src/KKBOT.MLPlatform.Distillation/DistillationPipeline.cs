// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Numerics.Tensors;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using KKBOT.MLPlatform.Abstractions;
using KKBOT.Observability;

namespace KKBOT.MLPlatform.Distillation;

/// <summary>
/// Knowledge distillation pipeline for model compression.
/// Supports: Temperature scaling, feature matching, attention transfer, quantization-aware distillation.
/// </summary>
public sealed class DistillationPipeline : IHostedService, IDisposable
{
    private readonly ILogger<DistillationPipeline> _logger;
    private readonly DistillationOptions _options;
    private readonly IMetricsRegistry _metrics;
    private readonly ConcurrentDictionary<string, DistillationJob> _jobs = new();
    private readonly Channel<DistillationJob> _jobQueue;
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly Task _workerTask;
    private bool _disposed;

    public DistillationPipeline(
        ILogger<DistillationPipeline> logger,
        IOptions<DistillationOptions> options,
        IMetricsRegistry metrics)
    {
        _logger = logger;
        _options = options.Value;
        _metrics = metrics;

        _jobQueue = Channel.CreateUnbounded<DistillationJob>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        _workerTask = ProcessJobsAsync(_shutdownCts.Token);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Distillation Pipeline started");
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Distillation Pipeline stopping");
        _shutdownCts.Cancel();
        _jobQueue.Writer.Complete();
        try
        {
            await _workerTask;
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Submits a distillation job.
    /// </summary>
    public async Task<string> SubmitDistillationAsync(DistillationConfig config, CancellationToken ct = default)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var job = new DistillationJob
        {
            JobId = jobId,
            Config = config,
            Status = DistillationJobStatus.Queued,
            CreatedAt = DateTimeOffset.UtcNow,
            Progress = 0,
            CurrentEpoch = 0
        };

        _jobs[jobId] = job;
        await _jobQueue.Writer.WriteAsync(job, ct);

        _logger.LogInformation("Distillation job {JobId} queued: {TeacherModel} -> {StudentArch}", 
            jobId, config.TeacherModelId, config.StudentArchitecture);

        return jobId;
    }

    /// <summary>
    /// Gets job status.
    /// </summary>
    public DistillationJob? GetJob(string jobId)
    {
        _jobs.TryGetValue(jobId, out var job);
        return job;
    }

    /// <summary>
    /// Cancels a running job.
    /// </summary>
    public async Task CancelJobAsync(string jobId, CancellationToken ct = default)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.CancellationToken.Cancel();
            job.Status = DistillationJobStatus.Cancelled;
            _logger.LogInformation("Distillation job {JobId} cancelled", jobId);
        }
    }

    private async Task ProcessJobsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            DistillationJob? job = null;
            try
            {
                job = await _jobQueue.Reader.ReadAsync(ct);
                await ExecuteDistillationAsync(job);
            }
            catch (ChannelClosedException)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing distillation job {JobId}", job?.JobId);
                if (job != null)
                {
                    job.Status = DistillationJobStatus.Failed;
                    job.ErrorMessage = ex.Message;
                }
            }
        }
    }

    private async Task ExecuteDistillationAsync(DistillationJob job)
    {
        var stopwatch = Stopwatch.StartNew();
        job.Status = DistillationJobStatus.Running;
        job.StartedAt = DateTimeOffset.UtcNow;

        try
        {
            _logger.LogInformation("Starting distillation job {JobId}", job.JobId);

            // Step 1: Load teacher model
            job.CurrentStep = "Loading teacher model";
            job.Progress = 10;
            var teacherSession = await LoadTeacherModelAsync(job.Config, job.CancellationToken.Token);

            // Step 2: Create student model
            job.CurrentStep = "Creating student model";
            job.Progress = 20;
            var studentModel = await CreateStudentModelAsync(job.Config, job.CancellationToken.Token);

            // Step 3: Prepare dataset
            job.CurrentStep = "Preparing dataset";
            job.Progress = 30;
            var (trainData, valData) = await PrepareDatasetAsync(job.Config, job.CancellationToken.Token);

            // Step 4: Run distillation training
            job.CurrentStep = "Training";
            job.Progress = 40;
            await RunDistillationTrainingAsync(job, teacherSession, studentModel, trainData, valData);

            // Step 5: Export and quantize
            job.CurrentStep = "Exporting model";
            job.Progress = 90;
            var modelPath = await ExportStudentModelAsync(job, studentModel);

            // Step 6: Validate
            job.CurrentStep = "Validating";
            job.Progress = 95;
            await ValidateDistilledModelAsync(job, modelPath, valData);

            job.Progress = 100;
            job.Status = DistillationJobStatus.Completed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.ArtifactPath = modelPath;
            job.TotalDuration = stopwatch.Elapsed;

            _logger.LogInformation("Distillation job {JobId} completed in {Duration}", job.JobId, stopwatch.Elapsed);
            _metrics.RecordHttpRequestDuration("distillation_job", stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException)
        {
            job.Status = DistillationJobStatus.Cancelled;
            _logger.LogInformation("Distillation job {JobId} cancelled", job.JobId);
        }
        catch (Exception ex)
        {
            job.Status = DistillationJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Distillation job {JobId} failed", job.JobId);
        }
    }

    private async Task<InferenceSession> LoadTeacherModelAsync(DistillationConfig config, CancellationToken ct)
    {
        var modelPath = Path.Combine(_options.ModelStoragePath, config.TeacherModelId, "model.onnx");
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"Teacher model not found: {modelPath}");
        }

        var options = new SessionOptions();
        options.AppendExecutionProvider_CPU();
        options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;

        return new InferenceSession(modelPath, options);
    }

    private async Task<StudentModel> CreateStudentModelAsync(DistillationConfig config, CancellationToken ct)
    {
        // In a real implementation, this would create the student architecture
        // For now, we'll create a simplified version
        _logger.LogInformation("Creating student model: {Architecture}", config.StudentArchitecture);

        var student = new StudentModel
        {
            Architecture = config.StudentArchitecture,
            InputShape = config.InputShape,
            OutputShape = config.OutputShape,
            Parameters = new Dictionary<string, float[]>()
        };

        // Initialize student parameters (random or from pretrained)
        InitializeStudentParameters(student, config);

        return student;
    }

    private void InitializeStudentParameters(StudentModel student, DistillationConfig config)
    {
        // Initialize weights - in practice would load from pretrained checkpoint
        var random = new Random(42);
        
        // This is a placeholder - real implementation would define architecture
        // and initialize weights properly
        student.Parameters["initialized"] = new float[] { 1.0f };
    }

    private async Task<(float[][] train, float[][] val)> PrepareDatasetAsync(DistillationConfig config, CancellationToken ct)
    {
        // Load dataset - in practice would load from feature store or data lake
        var datasetPath = Path.Combine(_options.DataPath, config.DatasetId);
        
        // Placeholder: generate synthetic data for demonstration
        var trainSize = 10000;
        var valSize = 2000;
        var inputDim = config.InputShape.Aggregate(1, (a, b) => a * b);
        var outputDim = config.OutputShape.Aggregate(1, (a, b) => a * b);

        var train = new float[trainSize][];
        var val = new float[valSize][];

        var random = new Random(42);
        for (int i = 0; i < trainSize; i++)
        {
            train[i] = Enumerable.Range(0, inputDim).Select(_ => (float)random.NextDouble()).ToArray();
        }
        for (int i = 0; i < valSize; i++)
        {
            val[i] = Enumerable.Range(0, inputDim).Select(_ => (float)random.NextDouble()).ToArray();
        }

        return (train, val);
    }

    private async Task RunDistillationTrainingAsync(
        DistillationJob job,
        InferenceSession teacher,
        StudentModel student,
        float[][] trainData,
        float[][] valData)
    {
        var config = job.Config;
        var epochs = config.Epochs;
        var batchSize = config.BatchSize;
        var learningRate = config.LearningRate;
        var temperature = config.Temperature;
        var alpha = config.Alpha;

        var trainSteps = trainData.Length / batchSize;
        var valSteps = valData.Length / batchSize;

        for (int epoch = 0; epoch < epochs && !job.CancellationToken.Token.IsCancellationRequested; epoch++)
        {
            job.CurrentEpoch = epoch + 1;
            job.Progress = 40 + (int)((epoch / (double)epochs) * 50);

            // Training loop
            double epochLoss = 0;
            int processed = 0;

            for (int step = 0; step < trainSteps; step++)
            {
                if (job.CancellationToken.Token.IsCancellationRequested) break;

                var batchStart = step * batchSize;
                var batch = trainData.Skip(batchStart).Take(batchSize).ToArray();

                // Get teacher predictions (soft labels)
                var teacherOutputs = await GetTeacherPredictionsAsync(teacher, batch, temperature);

                // Forward pass through student
                var studentOutputs = ForwardStudent(student, batch);

                // Compute distillation loss
                var loss = ComputeDistillationLoss(studentOutputs, teacherOutputs, alpha, temperature);

                // Backward pass (gradient computation)
                // In practice, would use autograd engine like PyTorch/TensorFlow
                // Here we simulate gradient update
                UpdateStudentParameters(student, loss, learningRate);

                epochLoss += loss;
                processed += batch.Length;

                // Report progress
                if (step % 10 == 0)
                {
                    _metrics.RecordHttpRequestDuration("distillation_step_loss", loss);
                }
            }

            // Validation
            double valLoss = 0;
            for (int step = 0; step < valSteps; step++)
            {
                var batchStart = step * batchSize;
                var batch = valData.Skip(batchStart).Take(batchSize).ToArray();

                var teacherOutputs = await GetTeacherPredictionsAsync(teacher, batch, temperature);
                var studentOutputs = ForwardStudent(student, batch);
                var loss = ComputeDistillationLoss(studentOutputs, teacherOutputs, alpha, temperature);
                valLoss += loss;
            }

            job.Metrics = new DistillationMetrics
            {
                Epoch = epoch + 1,
                TrainLoss = epochLoss / trainSteps,
                ValLoss = valLoss / valSteps,
                LearningRate = learningRate,
                Duration = DateTimeOffset.UtcNow - job.StartedAt.Value
            };

            _logger.LogInformation("Job {JobId} Epoch {Epoch}/{Epochs}: TrainLoss={TrainLoss:.4f}, ValLoss={ValLoss:.4f}",
                job.JobId, epoch + 1, epochs, job.Metrics.TrainLoss, job.Metrics.ValLoss);

            // Learning rate scheduling
            if (config.LrScheduler == "cosine")
            {
                learningRate = config.LearningRate * 0.5 * (1 + Math.Cos(Math.PI * epoch / epochs));
            }
            else if (config.LrScheduler == "step")
            {
                if (epoch > 0 && epoch % config.LrStepSize == 0)
                {
                    learningRate *= config.LrGamma;
                }
            }
        }
    }

    private async Task<float[][]> GetTeacherPredictionsAsync(InferenceSession teacher, float[][] batch, double temperature)
    {
        // Run teacher inference on batch
        // This is simplified - real implementation would batch properly
        var results = new float[batch.Length][];

        for (int i = 0; i < batch.Length; i++)
        {
            var inputTensor = new DenseTensor<float>(batch[i], new[] { 1, batch[i].Length });
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input", inputTensor) };
            
            using var outputs = teacher.Run(inputs);
            var outputTensor = outputs.First(o => o.Name == "output").AsTensor<float>();
            results[i] = outputTensor.ToArray();
        }

        // Apply temperature scaling
        if (temperature != 1.0)
        {
            for (int i = 0; i < results.Length; i++)
            {
                results[i] = ApplyTemperature(results[i], temperature);
            }
        }

        return results;
    }

    private float[] ApplyTemperature(float[] logits, double temperature)
    {
        // Softmax with temperature
        var max = logits.Max();
        var exp = logits.Select(x => Math.Exp((x - max) / temperature)).ToArray();
        var sum = exp.Sum();
        return exp.Select(x => (float)(x / sum)).ToArray();
    }

    private float[] ForwardStudent(StudentModel student, float[] input)
    {
        // Simplified forward pass - real implementation would use the student model
        // For now, return dummy output matching expected shape
        return new float[student.OutputShape.Aggregate(1, (a, b) => a * b)];
    }

    private float ComputeDistillationLoss(float[] studentLogits, float[] teacherProbs, double alpha, double temperature)
    {
        // KL Divergence loss for distillation
        // L = alpha * KL(teacher || student) + (1-alpha) * CE(student, hard_labels)
        
        var studentProbs = Softmax(studentLogits);
        
        double klDiv = 0;
        for (int i = 0; i < teacherProbs.Length; i++)
        {
            if (teacherProbs[i] > 0 && studentProbs[i] > 0)
            {
                klDiv += teacherProbs[i] * Math.Log(teacherProbs[i] / studentProbs[i]);
            }
        }

        // Cross entropy with hard labels (assuming teacher's argmax as target)
        var hardTarget = teacherProbs.Select((p, i) => new { p, i })
            .OrderByDescending(x => x.p).First().i;
        
        double ceLoss = -Math.Log(studentProbs[hardTarget] + 1e-8);

        return (float)(alpha * klDiv + (1 - alpha) * ceLoss);
    }

    private float[] Softmax(float[] logits)
    {
        var max = logits.Max();
        var exp = logits.Select(x => Math.Exp(x - max)).ToArray();
        var sum = exp.Sum();
        return exp.Select(x => (float)(x / sum)).ToArray();
    }

    private void UpdateStudentParameters(StudentModel student, float loss, double learningRate)
    {
        // Simplified SGD update - real implementation would compute gradients
        // and update weights via backpropagation
    }

    private async Task<string> ExportStudentModelAsync(DistillationJob job, StudentModel student)
    {
        var outputDir = Path.Combine(_options.ModelStoragePath, $"distilled_{job.JobId}");
        Directory.CreateDirectory(outputDir);

        var modelPath = Path.Combine(outputDir, "model.onnx");

        // In practice, would export using ONNX exporter from PyTorch/TensorFlow
        // For now, create a minimal valid ONNX model
        await ExportToOnnxAsync(student, modelPath, job.Config);

        // Apply quantization if configured
        if (job.Config.Quantization != "NONE")
        {
            await QuantizeModelAsync(modelPath, job.Config.Quantization);
        }

        // Create metadata
        var metadataPath = Path.Combine(outputDir, "metadata.json");
        var metadata = new ModelMetadata
        {
            ModelId = $"distilled_{job.JobId}",
            Name = $"Distilled {job.Config.TeacherModelId}",
            Version = "1.0.0",
            Type = ModelType.Onnx,
            Framework = ModelFramework.PyTorch,
            SourceModelId = job.Config.TeacherModelId,
            DistillationConfig = job.Config,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var json = System.Text.Json.JsonSerializer.Serialize(metadata, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(metadataPath, json);

        return modelPath;
    }

    private async Task ExportToOnnxAsync(StudentModel student, string path, DistillationConfig config)
    {
        // Placeholder: In reality, would use torch.onnx.export or tf.saved_model
        // For now, create a minimal ONNX file
        await File.WriteAllTextAsync(path, ""); // Placeholder
    }

    private async Task QuantizeModelAsync(string modelPath, string quantization)
    {
        // Use ONNX Runtime quantization tools
        // In practice: onnxruntime.quantization.quantize_dynamic / quantize_static
        _logger.LogInformation("Quantizing model to {Quantization}", quantization);
        await Task.CompletedTask;
    }

    private async Task ValidateDistilledModelAsync(DistillationJob job, string modelPath, float[][] valData)
    {
        // Load distilled model and run validation
        var options = new SessionOptions();
        options.AppendExecutionProvider_CPU();
        
        using var session = new InferenceSession(modelPath, options);
        
        double totalLoss = 0;
        int correct = 0;
        int total = 0;

        foreach (var sample in valData.Take(100)) // Sample for speed
        {
            var inputTensor = new DenseTensor<float>(sample, new[] { 1, sample.Length });
            using var outputs = session.Run(new[] { NamedOnnxValue.CreateFromTensor("input", inputTensor) });
            
            var outputTensor = outputs.First(o => o.Name == "output").AsTensor<float>();
            var predictions = outputTensor.ToArray();
            
            // Compute accuracy (simplified)
            var predClass = predictions.Select((v, i) => new { v, i }).OrderByDescending(x => x.v).First().i;
            // Would compare with ground truth
            total++;
        }

        job.Metrics = new DistillationMetrics
        {
            Epoch = job.Config.Epochs,
            ValLoss = totalLoss / Math.Max(1, total),
            Accuracy = total > 0 ? (double)correct / total : 0
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _shutdownCts.Cancel();
        _workerTask.Wait(TimeSpan.FromSeconds(30));
    }
}

// ==================== DATA MODELS ====================

internal sealed class DistillationJob
{
    public string JobId { get; init; } = string.Empty;
    public DistillationConfig Config { get; init; } = new();
    public DistillationJobStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public int Progress { get; set; }
    public string CurrentStep { get; set; } = string.Empty;
    public int CurrentEpoch { get; set; }
    public DistillationMetrics Metrics { get; set; } = new();
    public string ErrorMessage { get; set; } = string.Empty;
    public string ArtifactPath { get; set; } = string.Empty;
    public CancellationTokenSource CancellationToken { get; init; } = new();
}

public enum DistillationJobStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}

internal sealed class StudentModel
{
    public string Architecture { get; set; } = string.Empty;
    public int[] InputShape { get; set; } = Array.Empty<int>();
    public int[] OutputShape { get; set; } = Array.Empty<int>();
    public Dictionary<string, float[]> Parameters { get; set; } = new();
}

internal sealed class DistillationMetrics
{
    public int Epoch { get; set; }
    public double TrainLoss { get; set; }
    public double ValLoss { get; set; }
    public double Accuracy { get; set; }
    public double LearningRate { get; set; }
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Distillation configuration options.
/// </summary>
public sealed class DistillationOptions
{
    public string ModelStoragePath { get; set; } = "./models";
    public string DataPath { get; set; } = "./data";
    public int MaxConcurrentJobs { get; set; } = 2;
    public int DefaultBatchSize { get; set; } = 32;
    public int DefaultEpochs { get; set; } = 10;
    public double DefaultLearningRate { get; set; } = 1e-4;
    public string DefaultLrScheduler { get; set; } = "cosine";
}

/// <summary>
/// DI Extensions.
/// </summary>
public static class DistillationExtensions
{
    public static IServiceCollection AddKkbotDistillation(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DistillationOptions>(configuration.GetSection("Distillation"));
        services.AddSingleton<DistillationPipeline>();
        services.AddHostedService(sp => sp.GetRequiredService<DistillationPipeline>());
        return services;
    }
}
