// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KKBOT.AI.Optimization.Models;

/// <summary>
/// Target hardware platforms for optimization
/// </summary>
public enum TargetPlatform
{
    Cpu,
    NvidiaGpu,
    AmdGpu,
    IntelGpu,
    NvidiaJetson,
    RaspberryPi,
    CoralTpu,
    AppleNeuralEngine,
    QualcommHexagon,
    GenericArm64,
    GenericX64,
    WebAssembly,
    Custom
}

/// <summary>
/// Precision modes for quantization
/// </summary>
public enum PrecisionMode
{
    Fp32,
    Fp16,
    Int8,
    Int4,
    Mixed,
    Dynamic,
    Auto
}

/// <summary>
/// Optimization pipeline stages
/// </summary>
public enum OptimizationStage
{
    ModelLoading,
    GraphOptimization,
    ConstantFolding,
    DeadCodeElimination,
    OperatorFusion,
    LayoutOptimization,
    Quantization,
    Calibration,
    KernelSelection,
    MemoryPlanning,
    CodeGeneration,
    Verification,
    Benchmarking,
    Packaging
}

/// <summary>
/// Model input/output specification
/// </summary>
public sealed class ModelTensorSpec
{
    public string Name { get; set; } = string.Empty;
    public int[] Shape { get; set; } = Array.Empty<int>();
    public string DataType { get; set; } = "float32";
    public string Layout { get; set; } = "NCHW"; // NCHW, NHWC, etc.
    public bool IsDynamic { get; set; } = false;
    public int[]? MinShape { get; set; }
    public int[]? MaxShape { get; set; }
    public int[]? OptShape { get; set; }
}

/// <summary>
/// Model metadata and specification
/// </summary>
public sealed class ModelSpec
{
    public string ModelId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Framework { get; set; } = string.Empty; // ONNX, TensorFlow, PyTorch, TensorRT, CoreML
    public string SourcePath { get; set; } = string.Empty;
    public List<ModelTensorSpec> Inputs { get; set; } = new();
    public List<ModelTensorSpec> Outputs { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public long FileSizeBytes { get; set; }
    public string Checksum { get; set; } = string.Empty;
}

/// <summary>
/// Optimization configuration
/// </summary>
public sealed class OptimizationConfig
{
    public string OptimizationId { get; set; } = Guid.NewGuid().ToString();
    public string ModelId { get; set; } = string.Empty;
    public TargetPlatform TargetPlatform { get; set; } = TargetPlatform.Cpu;
    public PrecisionMode PrecisionMode { get; set; } = PrecisionMode.Fp16;
    public List<OptimizationStage> EnabledStages { get; set; } = Enum.GetValues<OptimizationStage>().Cast<OptimizationStage>().ToList();
    public Dictionary<string, object> StageOptions { get; set; } = new();
    public QuantizationConfig Quantization { get; set; } = new();
    public CalibrationConfig Calibration { get; set; } = new();
    public BenchmarkConfig Benchmark { get; set; } = new();
    public bool EnableProfiling { get; set; } = true;
    public bool SaveIntermediateArtifacts { get; set; } = false;
    public int MaxWorkspaceSizeMB { get; set; } = 1024;
    public int TimeoutMinutes { get; set; } = 60;
}

/// <summary>
/// Quantization configuration
/// </summary>
public sealed class QuantizationConfig
{
    public bool Enabled { get; set; } = true;
    public PrecisionMode Mode { get; set; } = PrecisionMode.Int8;
    public QuantizationGranularity Granularity { get; set; } = QuantizationGranularity.PerTensor;
    public QuantizationAlgorithm Algorithm { get; set; } = QuantizationAlgorithm.MinMax;
    public bool PerChannel { get; set; } = false;
    public bool Symmetric { get; set; } = true;
    public int CalibrationBatchSize { get; set; } = 32;
    public int CalibrationSteps { get; set; } = 100;
    public List<string> ExcludedOperators { get; set; } = new();
    public List<string> ExcludedNodes { get; set; } = new();
    public bool UseCustomCalibrationData { get; set; } = false;
    public string? CalibrationDataPath { get; set; }
    public int CalibrationDataSamples { get; set; } = 1000;
}

public enum QuantizationGranularity
{
    PerTensor,
    PerChannel,
    PerGroup,
    PerBlock
}

public enum QuantizationAlgorithm
{
    MinMax,
    Percentile,
    Entropy,
    KL,
    PercentileMovingAverage,
    LearnedGrid,
    AWQ,
    GPTQ
}

/// <summary>
/// Calibration configuration for post-training quantization
/// </summary>
public sealed class CalibrationConfig
{
    public bool Enabled { get; set; } = true;
    public string CalibrationDataPath { get; set; } = string.Empty;
    public int BatchSize { get; set; } = 32;
    public int NumBatches { get; set; } = 100;
    public string PreprocessingPipeline { get; set; } = string.Empty;
    public Dictionary<string, object> AugmentationConfig { get; set; } = new();
    public bool UseEntropyCalibration { get; set; } = true;
    public double Percentile { get; set; } = 99.99;
}

/// <summary>
/// Benchmark configuration
/// </summary>
public sealed class BenchmarkConfig
{
    public bool Enabled { get; set; } = true;
    public int WarmupRuns { get; set; } = 10;
    public int BenchmarkRuns { get; set; } = 100;
    public int BatchSize { get; set; } = 1;
    public List<int> TestBatchSizes { get; set; } = new() { 1, 2, 4, 8, 16, 32 };
    public bool MeasureMemory { get; set; } = true;
    public bool MeasurePower { get; set; } = false;
    public int WarmupIterations { get; set; } = 10;
    public int MinDurationSeconds { get; set; } = 10;
    public string? OutputFormat { get; set; } = "json"; // json, csv, markdown
    public string? OutputPath { get; set; }
}

/// <summary>
/// Optimization result
/// </summary>
public sealed class OptimizationResult
{
    public string OptimizationId { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public OptimizationStatus Status { get; set; } = OptimizationStatus.Pending;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public TimeSpan Duration { get; set; }
    public string? OptimizedModelPath { get; set; }
    public ModelSpec? OptimizedModelSpec { get; set; }
    public OptimizationMetrics Metrics { get; set; } = new();
    public List<StageResult> StageResults { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public List<string> Warnings { get; set; } = new();
    public Dictionary<string, object> Artifacts { get; set; } = new();
}

public enum OptimizationStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
    PartialSuccess
}

public sealed class OptimizationMetrics
{
    public double OriginalLatencyMs { get; set; }
    public double OptimizedLatencyMs { get; set; }
    public double SpeedupRatio { get; set; }
    public double OriginalModelSizeMB { get; set; }
    public double OptimizedModelSizeMB { get; set; }
    public double CompressionRatio { get; set; }
    public double AccuracyDrop { get; set; }
    public double ThroughputImprovement { get; set; }
    public double MemoryReductionPercent { get; set; }
    public double PowerReductionPercent { get; set; }
    public Dictionary<string, double> PerOperatorSpeedup { get; set; } = new();
    public Dictionary<string, double> MemoryUsageMB { get; set; } = new();
    public Dictionary<string, double> PowerConsumptionWatts { get; set; } = new();
}

public sealed class StageResult
{
    public OptimizationStage Stage { get; set; }
    public StageStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan Duration { get; set; }
    public string? OutputPath { get; set; }
    public Dictionary<string, object> Metrics { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public enum StageStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Skipped
}

/// <summary>
/// Deployment package for edge devices
/// </summary>
public sealed class DeploymentPackage
{
    public string PackageId { get; set; } = Guid.NewGuid().ToString();
    public string ModelId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public TargetPlatform TargetPlatform { get; set; }
    public PrecisionMode Precision { get; set; }
    public string OptimizedModelPath { get; set; } = string.Empty;
    public string RuntimePath { get; set; } = string.Empty;
    public string ConfigPath { get; set; } = string.Empty;
    public string LicensePath { get; set; } = string.Empty;
    public ModelSpec ModelSpec { get; set; } = new();
    public OptimizationMetrics Metrics { get; set; } = new();
    public List<string> Dependencies { get; set; } = new();
    public Dictionary<string, string> RuntimeConfig { get; set; } = new();
    public string Checksum { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Edge device specification
/// </summary>
public sealed class EdgeDeviceSpec
{
    public string DeviceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TargetPlatform Platform { get; set; }
    public string Architecture { get; set; } = string.Empty; // arm64, x86_64, etc.
    public string OS { get; set; } = string.Empty; // Linux, Windows, Android, iOS
    public string OSVersion { get; set; } = string.Empty;
    public int CpuCores { get; set; }
    public string CpuModel { get; set; } = string.Empty;
    public long TotalMemoryMB { get; set; }
    public long AvailableMemoryMB { get; set; }
    public bool HasGpu { get; set; }
    public string? GpuModel { get; set; }
    public long GpuMemoryMB { get; set; }
    public int ComputeCapability { get; set; } // For NVIDIA GPUs
    public bool HasNpu { get; set; }
    public string? NpuModel { get; set; }
    public List<string> SupportedPrecisions { get; set; } = new();
    public List<string> SupportedFrameworks { get; set; } = new();
    public Dictionary<string, object> Capabilities { get; set; } = new();
}

/// <summary>
/// Edge deployment configuration
/// </summary>
public sealed class EdgeDeploymentConfig
{
    public string DeploymentId { get; set; } = Guid.NewGuid().ToString();
    public string PackageId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public bool AutoStart { get; set; } = true;
    public bool EnableMonitoring { get; set; } = true;
    public int HealthCheckIntervalSeconds { get; set; } = 30;
    public int MaxRestartAttempts { get; set; } = 3;
    public Dictionary<string, object> RuntimeOptions { get; set; } = new();
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
    public int LogRetentionDays { get; set; } = 7;
    public bool EnableTelemetry { get; set; } = true;
}

/// <summary>
/// Optimization pipeline definition
/// </summary>
public sealed class OptimizationPipeline
{
    public string PipelineId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<PipelineStep> Steps { get; set; } = new();
    public Dictionary<string, object> GlobalOptions { get; set; } = new();
}

public sealed class PipelineStep
{
    public string StepId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public OptimizationStage Stage { get; set; }
    public Dictionary<string, object> Options { get; set; } = new();
    public List<string> DependsOn { get; set; } = new();
    public bool IsOptional { get; set; } = false;
    public RetryPolicy RetryPolicy { get; set; } = new();
    public TimeoutConfig Timeout { get; set; } = new();
}

public sealed class RetryPolicy
{
    public int MaxRetries { get; set; } = 3;
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(30);
    public double BackoffMultiplier { get; set; } = 2.0;
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(10);
}

public sealed class TimeoutConfig
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);
    public bool KillOnTimeout { get; set; } = true;
}