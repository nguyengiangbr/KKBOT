// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.AI.Optimization.Abstractions;
using KKBOT.AI.Optimization.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace KKBOT.AI.Optimization.Services;

public sealed class ModelOptimizer : IModelOptimizer
{
    private readonly ILogger<ModelOptimizer> _logger;
    private readonly IModelConverter _converter;
    private readonly IQuantizationService _quantizer;
    private readonly IModelBenchmarkService _benchmark;
    private readonly IModelRegistry _registry;
    private readonly OptimizationOptions _options;
    private readonly ConcurrentDictionary<string, OptimizationResult> _runningOptimizations = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens = new();

    public ModelOptimizer(
        ILogger<ModelOptimizer> logger,
        IModelConverter converter,
        IQuantizationService quantizer,
        IModelBenchmarkService benchmark,
        IModelRegistry registry,
        IOptions<OptimizationOptions> options)
    {
        _logger = logger;
        _converter = converter;
        _quantizer = quantizer;
        _benchmark = benchmark;
        _registry = registry;
        _options = options.Value;
    }

    public async Task<OptimizationResult> OptimizeAsync(OptimizationConfig config, CancellationToken ct = default)
    {
        var result = new OptimizationResult
        {
            OptimizationId = config.OptimizationId,
            ModelId = config.ModelId,
            Status = OptimizationStatus.Running,
            StartedAt = DateTime.UtcNow
        };

        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _cancellationTokens[config.OptimizationId] = cts;
        _runningOptimizations[config.OptimizationId] = result;

        _logger.LogInformation("Starting optimization {OptimizationId} for model {ModelId}", config.OptimizationId, config.ModelId);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Load model
            var modelSpec = await _converter.LoadModelAsync(config.ModelId, ct);
            result.Metrics.OriginalModelSizeMB = GetModelSize(config.ModelId);
            result.Metrics.OriginalLatencyMs = await BenchmarkOriginalAsync(config.ModelId, config, ct);

            var currentModelPath = config.ModelId;
            var currentSpec = modelSpec;

            // Execute optimization pipeline
            foreach (var stage in config.EnabledStages)
            {
                cts.Token.ThrowIfCancellationRequested();

                var stageResult = await ExecuteStageAsync(stage, currentModelPath, currentSpec, config, cts.Token);
                result.StageResults.Add(stageResult);

                if (stageResult.Status == StageStatus.Failed)
                {
                    if (config.EnabledStages.FirstOrDefault(s => s == stage) == config.EnabledStages.Last())
                    {
                        throw new InvalidOperationException($"Critical stage {stage} failed: {stageResult.ErrorMessage}");
                    }
                    result.Warnings.Add($"Stage {stage} failed: {stageResult.ErrorMessage}, continuing...");
                }
                else if (stageResult.Status == StageStatus.Completed)
                {
                    if (!string.IsNullOrEmpty(stageResult.OutputPath))
                    {
                        currentModelPath = stageResult.OutputPath;
                        currentSpec = await _converter.LoadModelAsync(stageResult.OutputPath, ct);
                    }
                }
            }

            // Final benchmark
            result.Metrics.OptimizedLatencyMs = await BenchmarkOptimizedAsync(currentModelPath, config, ct);
            result.Metrics.OptimizedModelSizeMB = GetModelSize(currentModelPath);
            
            // Calculate metrics
            CalculateFinalMetrics(result);
            
            // Validate accuracy
            var validation = await ValidateAccuracyAsync(config.ModelId, currentModelPath, ct);
            if (!validation.PassedValidation && _options.FailOnAccuracyDrop)
            {
                result.Status = OptimizationStatus.Failed;
                result.ErrorMessage = $"Accuracy drop {validation.OverallAccuracyDrop:P2} exceeds threshold";
                return result;
            }

            result.Metrics.AccuracyDrop = validation.OverallAccuracyDrop;
            result.OptimizedModelPath = currentModelPath;
            result.OptimizedModelSpec = await _converter.LoadModelAsync(currentModelPath, ct);
            result.Status = OptimizationStatus.Completed;

            _logger.LogInformation("Optimization {OptimizationId} completed in {Duration}", config.OptimizationId, stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            result.Status = OptimizationStatus.Cancelled;
            _logger.LogInformation("Optimization {OptimizationId} was cancelled", config.OptimizationId);
        }
        catch (Exception ex)
        {
            result.Status = OptimizationStatus.Failed;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Optimization {OptimizationId} failed", config.OptimizationId);
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            result.CompletedAt = DateTime.UtcNow;
            _cancellationTokens.TryRemove(config.OptimizationId, out _);
            _runningOptimizations.TryUpdate(config.OptimizationId, result, result);
        }

        return result;
    }

    public async Task<OptimizationResult?> GetOptimizationResultAsync(string optimizationId, CancellationToken ct = default)
    {
        _runningOptimizations.TryGetValue(optimizationId, out var result);
        return result;
    }

    public async Task<List<OptimizationResult>> GetOptimizationHistoryAsync(string modelId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        // In a real implementation, this would query a database
        return new List<OptimizationResult>();
    }

    public async Task<bool> CancelOptimizationAsync(string optimizationId, CancellationToken ct = default)
    {
        if (_cancellationTokens.TryRemove(optimizationId, out var cts))
        {
            cts.Cancel();
            if (_runningOptimizations.TryGetValue(optimizationId, out var result))
            {
                result.Status = OptimizationStatus.Cancelled;
                result.CompletedAt = DateTime.UtcNow;
            }
            return true;
        }
        return false;
    }

    // Private helper methods

    private async Task<StageResult> ExecuteStageAsync(OptimizationStage stage, string modelPath, ModelSpec spec, OptimizationConfig config, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new StageResult
        {
            Stage = stage,
            Status = StageStatus.Running,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            var outputPath = string.Empty;

            switch (stage)
            {
                case OptimizationStage.GraphOptimization:
                    outputPath = await RunGraphOptimizationAsync(modelPath, config, ct);
                    break;
                case OptimizationStage.ConstantFolding:
                    outputPath = await RunConstantFoldingAsync(modelPath, config, ct);
                    break;
                case OptimizationStage.DeadCodeElimination:
                    outputPath = await RunDeadCodeEliminationAsync(modelPath, config, ct);
                    break;
                case OptimizationStage.OperatorFusion:
                    outputPath = await RunOperatorFusionAsync(modelPath, config, ct);
                    break;
                case OptimizationStage.LayoutOptimization:
                    outputPath = await RunLayoutOptimizationAsync(modelPath, config, ct);
                    break;
                case OptimizationStage.Quantization:
                    outputPath = await RunQuantizationAsync(modelPath, config, ct);
                    break;
                case OptimizationStage.Calibration:
                    await RunCalibrationAsync(modelPath, config, ct);
                    outputPath = modelPath;
                    break;
                case OptimizationStage.KernelSelection:
                    outputPath = await RunKernelSelectionAsync(modelPath, config, ct);
                    break;
                case OptimizationStage.MemoryPlanning:
                    outputPath = await RunMemoryPlanningAsync(modelPath, config, ct);
                    break;
                case OptimizationStage.CodeGeneration:
                    outputPath = await RunCodeGenerationAsync(modelPath, config, ct);
                    break;
                case OptimizationStage.Verification:
                    await RunVerificationAsync(modelPath, config, ct);
                    outputPath = modelPath;
                    break;
                case OptimizationStage.Benchmarking:
                    await RunBenchmarkingAsync(modelPath, config, ct);
                    outputPath = modelPath;
                    break;
                case OptimizationStage.Packaging:
                    outputPath = await CreateDeploymentPackageAsync(modelPath, ct);
                    break;
            }

            result.OutputPath = outputPath;
            result.Status = StageStatus.Completed;
        }
        catch (Exception ex)
        {
            result.Status = StageStatus.Failed;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            result.CompletedAt = DateTime.UtcNow;
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }

    private async Task<string> RunGraphOptimizationAsync(string modelPath, OptimizationConfig config, CancellationToken ct)
    {
        // ONNX graph optimization using ONNX Runtime
        var outputPath = Path.ChangeExtension(modelPath, ".optimized.onnx");
        
        // In a real implementation, this would use ONNX Runtime's graph optimizer
        // For now, copy the file
        File.Copy(modelPath, outputPath, true);
        
        return outputPath;
    }

    private async Task<string> RunConstantFoldingAsync(string modelPath, OptimizationConfig config, CancellationToken ct)
    {
        var outputPath = Path.ChangeExtension(modelPath, ".folded.onnx");
        File.Copy(modelPath, outputPath, true);
        return outputPath;
    }

    private async Task<string> RunDeadCodeEliminationAsync(string modelPath, OptimizationConfig config, CancellationToken ct)
    {
        var outputPath = Path.ChangeExtension(modelPath, ".dce.onnx");
        File.Copy(modelPath, outputPath, true);
        return outputPath;
    }

    private async Task<string> RunOperatorFusionAsync(string modelPath, OptimizationConfig config, CancellationToken ct)
    {
        var outputPath = Path.ChangeExtension(modelPath, ".fused.onnx");
        File.Copy(modelPath, outputPath, true);
        return outputPath;
    }

    private async Task<string> RunLayoutOptimizationAsync(string modelPath, OptimizationConfig config, CancellationToken ct)
    {
        var outputPath = Path.ChangeExtension(modelPath, ".layout.onnx");
        File.Copy(modelPath, outputPath, true);
        return outputPath;
    }

    private async Task<string> RunQuantizationAsync(string modelPath, OptimizationConfig config, CancellationToken ct)
    {
        var quantConfig = config.Quantization;
        if (!quantConfig.Enabled)
            return modelPath;

        var outputPath = Path.ChangeExtension(modelPath, $".{quantConfig.Mode.ToString().ToLower()}.onnx");
        
        if (quantConfig.Mode == PrecisionMode.Int8 || quantConfig.Mode == PrecisionMode.Int4)
        {
            // Run calibration first
            var calibrationResult = await _quantizer.CalibrateAsync(modelPath, config.Calibration, CancellationToken.None);
            
            // Apply quantization
            var quantizedPath = await _quantizer.QuantizeOnnxModelAsync(modelPath, outputPath, config.Quantization, CancellationToken.None);
            return quantizedPath;
        }
        else if (quantConfig.Mode == PrecisionMode.Fp16)
        {
            // FP16 conversion
            return await ConvertToFp16Async(modelPath, outputPath, CancellationToken.None);
        }

        return modelPath;
    }

    private async Task<string> ConvertToFp16Async(string modelPath, string outputPath, CancellationToken ct)
    {
        // Convert ONNX model to FP16
        // In real implementation, use ONNX Runtime's float16 converter
        File.Copy(modelPath, outputPath, true);
        return outputPath;
    }

    private async Task RunCalibrationAsync(string modelPath, OptimizationConfig config, CancellationToken ct)
    {
        var calibrationConfig = config.Calibration;
        if (!calibrationConfig.Enabled) return;

        var result = await _quantizer.CalibrateAsync(modelPath, calibrationConfig, ct);
        // Save calibration cache
        var cachePath = Path.ChangeExtension(modelPath, ".calibration.cache");
        File.WriteAllText(cachePath, JsonSerializer.Serialize(result));
    }

    private async Task<string> RunKernelSelectionAsync(string modelPath, OptimizationConfig config, CancellationToken ct)
    {
        // TensorRT kernel selection / ONNX Runtime execution provider selection
        // For now, just return the model path
        return modelPath;
    }

    private async Task<string> RunMemoryPlanningAsync(string modelPath, OptimizationConfig config, CancellationToken ct)
    {
        // Memory planning optimization
        return modelPath;
    }

    private async Task<string> RunCodeGenerationAsync(string modelPath, OptimizationConfig config, CancellationToken ct)
    {
        // Code generation for target platform (TensorRT engine, CoreML, TFLite)
        var targetPlatform = config.TargetPlatform;
        
        switch (targetPlatform)
        {
            case TargetPlatform.NvidiaGpu:
            case TargetPlatform.NvidiaJetson:
                return await ConvertToTensorRTAsync(modelPath, config, CancellationToken.None);
            case TargetPlatform.AppleNeuralEngine:
            case TargetPlatform.GenericArm64:
                return await ConvertToCoreMLAsync(modelPath, config, CancellationToken.None);
            case TargetPlatform.RaspberryPi:
            case TargetPlatform.CoralTpu:
                return await ConvertToTFLiteAsync(modelPath, config, CancellationToken.None);
            default:
                return modelPath;
        }
    }

    private async Task<string> ConvertToTensorRTAsync(string modelPath, OptimizationConfig config, CancellationToken ct)
    {
        var options = new TensorRTConversionOptions
        {
            Precision = config.Quantization.Mode == PrecisionMode.Int8 ? PrecisionMode.Int8 : 
                       config.Quantization.Mode == PrecisionMode.Fp16 ? PrecisionMode.Fp16 : PrecisionMode.Fp32,
            MaxWorkspaceSizeMB = config.MaxWorkspaceSizeMB,
            EnableFp16 = config.Quantization.Mode == PrecisionMode.Fp16 || config.Quantization.Mode == PrecisionMode.Int8,
            EnableInt8 = config.Quantization.Mode == PrecisionMode.Int8,
            EnableProfiling = config.EnableProfiling
        };

        var outputPath = Path.ChangeExtension(modelPath, ".trt");
        return await _modelConverter.ConvertToTensorRTAsync(modelPath, outputPath, 
            new TensorRTConversionOptions
            {
                Precision = options.Precision,
                MaxWorkspaceSizeMB = options.MaxWorkspaceSizeMB,
                EnableFp16 = options.EnableFp16,
                EnableInt8 = options.EnableInt8,
                EnableProfiling = options.EnableProfiling
            }, CancellationToken.None);
    }

    private async Task<string> ConvertToCoreMLAsync(string modelPath, OptimizationConfig config, CancellationToken ct)
    {
        var outputPath = Path.ChangeExtension(modelPath, ".mlmodel");
        return await _modelConverter.ConvertToCoreMLAsync(modelPath, outputPath, 
            new CoreMLConversionOptions
            {
                Precision = config.Quantization.Mode == PrecisionMode.Int8 ? PrecisionMode.Int8 : 
                           config.Quantization.Mode == PrecisionMode.Fp16 ? PrecisionMode.Fp16 : PrecisionMode.Fp32,
                MinimumDeploymentTarget = 14,
                EnableQuantization = config.Quantization.Mode == PrecisionMode.Int8
            }, CancellationToken.None);
    }

    private async Task<string> ConvertToTFLiteAsync(string modelPath, OptimizationConfig config, CancellationToken ct)
    {
        var outputPath = Path.ChangeExtension(modelPath, ".tflite");
        return await _modelConverter.ConvertToTFLiteAsync(modelPath, outputPath,
            new TFLiteConversionOptions
            {
                Precision = config.Quantization.Mode,
                EnableQuantization = config.Quantization.Mode == PrecisionMode.Int8
            }, CancellationToken.None);
    }

    private async Task RunVerificationAsync(string modelPath, OptimizationConfig config, CancellationToken ct)
    {
        // Verify model correctness
        var spec = await _modelConverter.GetModelSpecAsync(modelPath, ct);
        if (spec == null)
            throw new InvalidOperationException("Model verification failed: could not load model");
    }

    private async Task RunBenchmarkingAsync(string modelPath, OptimizationConfig config, CancellationToken ct)
    {
        if (!config.Benchmark.Enabled) return;

        var result = await _modelBenchmark.BenchmarkModelAsync(modelPath, config.Benchmark, ct);
        // Store benchmark results
    }

    private async Task<string> CreateDeploymentPackageAsync(string modelPath, CancellationToken ct)
    {
        var packageDir = Path.Combine(Path.GetDirectoryName(modelPath)!, "deployment");
        Directory.CreateDirectory(packageDir);
        
        var packagePath = Path.Combine(packageDir, "model_package.zip");
        // Create deployment package with model, config, runtime
        return packageDir;
    }

    private async Task<ModelSpec> LoadModelSpecAsync(string modelPath, CancellationToken ct)
    {
        return await _modelConverter.GetModelSpecAsync(modelPath, ct) ?? new ModelSpec();
    }

    private long GetModelSize(string modelPath)
    {
        if (File.Exists(modelPath))
        {
            return new FileInfo(modelPath).Length;
        }
        return 0;
    }

    private double GetModelSizeMB(string modelPath)
    {
        return GetModelSize(modelPath) / (1024.0 * 1024.0);
    }

    private async Task<double> BenchmarkOriginalAsync(string modelId, OptimizationConfig config, CancellationToken ct)
    {
        var result = await _benchmark.BenchmarkModelAsync(modelId, new BenchmarkConfig
        {
            Enabled = true,
            WarmupRuns = 10,
            BenchmarkRuns = 50,
            BatchSize = config.Benchmark.BatchSize
        }, ct);
        return result.MeanLatencyMs;
    }

    private async Task<double> BenchmarkOptimizedAsync(string modelPath, OptimizationConfig config, CancellationToken ct)
    {
        var result = await _benchmark.BenchmarkModelAsync(modelPath, config.Benchmark, ct);
        return result.MeanLatencyMs;
    }

    private void CalculateFinalMetrics(OptimizationResult result)
    {
        result.Metrics.SpeedupRatio = result.Metrics.OriginalLatencyMs / Math.Max(result.Metrics.OptimizedLatencyMs, 0.001);
        result.Metrics.CompressionRatio = result.Metrics.OriginalModelSizeMB / Math.Max(result.Metrics.OptimizedModelSizeMB, 0.001);
        result.Metrics.ThroughputImprovement = result.Metrics.SpeedupRatio;
        result.Metrics.MemoryReductionPercent = (1 - result.Metrics.OptimizedModelSizeMB / Math.Max(result.Metrics.OriginalModelSizeMB, 0.001)) * 100;
    }

    private async Task<QuantizationReport> ValidateAccuracyAsync(string originalModelPath, string optimizedModelPath, CancellationToken ct)
    {
        var originalModelId = Path.GetFileNameWithoutExtension(originalModelPath);
        var optimizedModelId = Path.GetFileNameWithoutExtension(optimizedModelPath);
        
        var comparison = await _benchmark.CompareModelsAsync(originalModelPath, optimizedModelPath, 
            new BenchmarkConfig { Enabled = true, BenchmarkRuns = 50 }, CancellationToken.None);
        
        return new QuantizationReport
        {
            OverallAccuracyDrop = comparison.AccuracyDrop,
            ModelSizeReduction = comparison.SizeReductionRatio,
            LatencyImprovement = comparison.SpeedupRatio,
            PassedValidation = comparison.PassedAccuracyThreshold && comparison.PassedLatencyThreshold
        };
    }

    public async Task<OptimizationResult?> GetOptimizationResultAsync(string optimizationId, CancellationToken ct = default)
    {
        _runningOptimizations.TryGetValue(optimizationId, out var result);
        return result;
    }

    public async Task<List<OptimizationResult>> GetOptimizationHistoryAsync(string modelId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        return new List<OptimizationResult>();
    }

    public async Task<bool> CancelOptimizationAsync(string optimizationId, CancellationToken ct = default)
    {
        if (_cancellationTokens.TryRemove(optimizationId, out var cts))
        {
            cts.Cancel();
            return true;
        }
        return false;
    }
}

public sealed class OptimizationOptions
{
    public int DefaultNodeTimeoutSeconds { get; set; } = 300;
    public int MaxConcurrentOptimizations { get; set; } = 4;
    public bool FailOnAccuracyDrop { get; set; } = true;
    public double MaxAccuracyDropThreshold { get; set; } = 0.02; // 2%
    public double MaxLatencyIncreaseThreshold { get; set; } = 1.1; // 10%
}