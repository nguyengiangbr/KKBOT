// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.AI.Optimization.Models;

namespace KKBOT.AI.Optimization.Abstractions;

public interface IModelOptimizer
{
    Task<OptimizationResult> OptimizeAsync(OptimizationConfig config, CancellationToken ct = default);
    Task<OptimizationResult?> GetOptimizationResultAsync(string optimizationId, CancellationToken ct = default);
    Task<List<OptimizationResult>> GetOptimizationHistoryAsync(string modelId, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<bool> CancelOptimizationAsync(string optimizationId, CancellationToken ct = default);
}

public interface IModelConverter
{
    Task<ModelSpec> LoadModelAsync(string modelPath, CancellationToken ct = default);
    Task<string> ConvertToOnnxAsync(string modelPath, string outputPath, ConversionOptions options, CancellationToken ct = default);
    Task<string> ConvertToTensorRTAsync(string onnxPath, string outputPath, TensorRTConversionOptions options, CancellationToken ct = default);
    Task<string> ConvertToCoreMLAsync(string modelPath, string outputPath, CoreMLConversionOptions options, CancellationToken ct = default);
    Task<string> ConvertToTFLiteAsync(string modelPath, string outputPath, TFLiteConversionOptions options, CancellationToken ct = default);
    Task<ModelSpec> GetModelSpecAsync(string modelPath, CancellationToken ct = default);
}

public sealed class ConversionOptions
{
    public TargetPlatform TargetPlatform { get; set; } = TargetPlatform.Cpu;
    public PrecisionMode TargetPrecision { get; set; } = PrecisionMode.Fp32;
    public bool Optimize { get; set; } = true;
    public int OpsetVersion { get; set; } = 17;
    public bool EnableDynamicShapes { get; set; } = true;
    public Dictionary<string, int[]>? InputShapes { get; set; }
}

public sealed class TensorRTConversionOptions
{
    public PrecisionMode Precision { get; set; } = PrecisionMode.Fp16;
    public int MaxWorkspaceSizeMB { get; set; } = 1024;
    public int MaxBatchSize { get; set; } = 1;
    public bool EnableFp16 { get; set; } = true;
    public bool EnableInt8 { get; set; } = false;
    public bool EnableSparsity { get; set; } = false;
    public int TacticSources { get; set; } = 0; // Default
    public int AvgTimingIterations { get; set; } = 1;
    public int MinTimingIterations { get; set; } = 1;
    public bool EnableProfiling { get; set; } = false;
    public string? CalibrationCachePath { get; set; }
    public List<int>? OptProfileMinShapes { get; set; }
    public List<int>? OptProfileOptShapes { get; set; }
    public List<int>? OptProfileMaxShapes { get; set; }
}

public sealed class CoreMLConversionOptions
{
    public PrecisionMode Precision { get; set; } = PrecisionMode.Fp16;
    public int MinimumDeploymentTarget { get; set; } = 14; // iOS 14+
    public bool ComputeUnits { get; set; } = true; // CPU, GPU, NeuralEngine
    public int MinimumiOSVersion { get; set; } = 14;
    public int MinimummacOSVersion { get; set; } = 11;
    public bool EnableQuantization { get; set; } = false;
}

public sealed class TFLiteConversionOptions
{
    public PrecisionMode Precision { get; set; } = PrecisionMode.Fp32;
    public bool EnableQuantization { get; set; } = false;
    public bool EnableSelectTfOps { get; set; } = false;
    public int[]? SupportedOps { get; set; }
    public bool EnableSelectTfOpsForQuantization { get; set; } = false;
    public bool AllowCustomOps { get; set; } = false;
}

public interface IQuantizationService
{
    Task<string> QuantizeOnnxModelAsync(string onnxPath, string outputPath, QuantizationConfig config, CancellationToken ct = default);
    Task<string> QuantizeTensorRTModelAsync(string tensorRTPath, string outputPath, QuantizationConfig config, CancellationToken ct = default);
    Task<CalibrationResult> CalibrateAsync(string modelPath, CalibrationConfig config, CancellationToken ct = default);
    Task<QuantizationReport> AnalyzeQuantizationImpactAsync(string originalPath, string quantizedPath, CalibrationConfig config, CancellationToken ct = default);
}

public sealed class CalibrationResult
{
    public string CalibrationCachePath { get; set; } = string.Empty;
    public Dictionary<string, (float Min, float Max)> ScaleMap { get; set; } = new();
    public Dictionary<string, (float Min, float Max)> ZeroPointMap { get; set; } = new();
    public Dictionary<string, object> Statistics { get; set; } = new();
    public TimeSpan Duration { get; set; }
}

public sealed class QuantizationReport
{
    public double OverallAccuracyDrop { get; set; }
    public double ModelSizeReduction { get; set; }
    public double LatencyImprovement { get; set; }
    public Dictionary<string, OperatorImpact> OperatorImpacts { get; set; } = new();
    public List<QuantizationIssue> Issues { get; set; } = new();
    public bool PassedValidation { get; set; }
}

public sealed class OperatorImpact
{
    public string OperatorName { get; set; } = string.Empty;
    public string OperatorType { get; set; } = string.Empty;
    public double AccuracyDrop { get; set; }
    public double LatencyChange { get; set; }
    public double MemoryChange { get; set; }
    public bool WasQuantized { get; set; }
    public string? ReasonNotQuantized { get; set; }
}

public sealed class QuantizationIssue
{
    public IssueSeverity Severity { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public string OperatorType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}

public enum IssueSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

public interface IModelBenchmarkService
{
    Task<BenchmarkResult> BenchmarkModelAsync(string modelPath, BenchmarkConfig config, CancellationToken ct = default);
    Task<ComparisonResult> CompareModelsAsync(string originalPath, string optimizedPath, BenchmarkConfig config, CancellationToken ct = default);
    Task<ProfileResult> ProfileModelAsync(string modelPath, ProfileConfig config, CancellationToken ct = default);
}

public sealed class BenchmarkResult
{
    public string ModelPath { get; set; } = string.Empty;
    public int BatchSize { get; set; }
    public int WarmupRuns { get; set; }
    public int BenchmarkRuns { get; set; }
    public double MeanLatencyMs { get; set; }
    public double MedianLatencyMs { get; set; }
    public double P95LatencyMs { get; set; }
    public double P99LatencyMs { get; set; }
    public double StdDevMs { get; set; }
    public double ThroughputFPS { get; set; }
    public double PeakMemoryMB { get; set; }
    public double AverageMemoryMB { get; set; }
    public double PeakPowerWatts { get; set; }
    public double AveragePowerWatts { get; set; }
    public double EnergyPerInferenceMJ { get; set; }
    public int BatchSize { get; set; }
    public int Iterations { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, double> PerOperatorLatency { get; set; } = new();
    public Dictionary<string, double> OperatorMemoryUsage { get; set; } = new();
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class ComparisonResult
{
    public string OriginalModelPath { get; set; } = string.Empty;
    public string OptimizedModelPath { get; set; } = string.Empty;
    public BenchmarkResult OriginalBenchmark { get; set; } = new();
    public BenchmarkResult OptimizedBenchmark { get; set; } = new();
    public double SpeedupRatio { get; set; }
    public double SizeReductionRatio { get; set; }
    public double AccuracyDrop { get; set; }
    public double MemoryReduction { get; set; }
    public double PowerReduction { get; set; }
    public bool PassedAccuracyThreshold { get; set; }
    public bool PassedLatencyThreshold { get; set; }
    public Dictionary<string, double> PerOperatorSpeedup { get; set; } = new();
    public bool Recommended { get; set; }
}

public sealed class ProfileConfig
{
    public bool ProfileCPU { get; set; } = true;
    public bool ProfileGPU { get; set; } = true;
    public bool ProfileMemory { get; set; } = true;
    public bool ProfileOperators { get; set; } = true;
    public int SampleRateHz { get; set; } = 1000;
    public int DurationSeconds { get; set; } = 60;
    public string OutputPath { get; set; } = string.Empty;
}

public sealed class ProfileResult
{
    public string ModelPath { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public List<OperatorProfile> OperatorProfiles { get; set; } = new();
    public MemoryProfile MemoryProfile { get; set; } = new();
    public GpuProfile? GpuProfile { get; set; }
    public PowerProfile? PowerProfile { get; set; }
}

public sealed class OperatorProfile
{
    public string OperatorName { get; set; } = string.Empty;
    public string OperatorType { get; set; } = string.Empty;
    public double SelfTimeMs { get; set; }
    public double TotalTimeMs { get; set; }
    public int CallCount { get; set; }
    public double PeakMemoryMB { get; set; }
    public double AvgMemoryMB { get; set; }
    public int InputCount { get; set; }
    public int OutputCount { get; set; }
    public long FLOPs { get; set; }
    public double ArithmeticIntensity { get; set; }
}

public sealed class MemoryProfile
{
    public double PeakMemoryMB { get; set; }
    public double AverageMemoryMB { get; set; }
    public Dictionary<string, double> MemoryByOperator { get; set; } = new();
    public Dictionary<string, double> MemoryByTensor { get; set; } = new();
    public long TotalAllocations { get; set; }
    public long PeakAllocationBytes { get; set; }
}

public sealed class GpuProfile
{
    public double UtilizationPercent { get; set; }
    public double MemoryUtilizationPercent { get; set; }
    public double PeakPowerWatts { get; set; }
    public double AveragePowerWatts { get; set; }
    public double TemperatureCelsius { get; set; }
    public double SmUtilizationPercent { get; set; }
    public double TensorCoreUtilizationPercent { get; set; }
}

public sealed class PowerProfile
{
    public double AveragePowerWatts { get; set; }
    public double PeakPowerWatts { get; set; }
    public double EnergyPerInferenceMJ { get; set; }
    public Dictionary<string, double> PowerByComponent { get; set; } = new();
}

public interface IEdgeDeploymentService
{
    Task<DeploymentPackage> CreateDeploymentPackageAsync(string optimizationId, EdgeDeviceSpec deviceSpec, CancellationToken ct = default);
    Task<DeploymentResult> DeployAsync(string packageId, string deviceId, EdgeDeploymentConfig config, CancellationToken ct = default);
    Task<DeploymentStatus> GetDeploymentStatusAsync(string deploymentId, CancellationToken ct = default);
    Task<bool> UpdateDeploymentAsync(string deploymentId, EdgeDeploymentConfig config, CancellationToken ct = default);
    Task<bool> RollbackDeploymentAsync(string deploymentId, CancellationToken ct = default);
    Task<DeviceHealth> GetDeviceHealthAsync(string deviceId, CancellationToken ct = default);
    Task<List<DeploymentPackage>> ListDeploymentsAsync(string deviceId, CancellationToken ct = default);
}

public sealed class DeploymentResult
{
    public string DeploymentId { get; set; } = Guid.NewGuid().ToString();
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime DeployedAt { get; set; } = DateTime.UtcNow;
    public string? EndpointUrl { get; set; }
    public Dictionary<string, object> RuntimeInfo { get; set; } = new();
}

public enum DeploymentStatus
{
    Pending,
    Downloading,
    Installing,
    Configuring,
    Starting,
    Running,
    Stopped,
    Failed,
    RollingBack
}

public sealed class DeviceHealth
{
    public string DeviceId { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public double CpuUsagePercent { get; set; }
    public double MemoryUsagePercent { get; set; }
    public double GpuUsagePercent { get; set; }
    public double TemperatureCelsius { get; set; }
    public double PowerWatts { get; set; }
    public long AvailableMemoryMB { get; set; }
    public long TotalMemoryMB { get; set; }
    public double DiskUsagePercent { get; set; }
    public List<DeploymentStatus> ActiveDeployments { get; set; } = new();
    public List<string> Alerts { get; set; } = new();
}

public interface IModelRegistry
{
    Task<ModelSpec> RegisterModelAsync(ModelSpec spec, CancellationToken ct = default);
    Task<ModelSpec?> GetModelAsync(string modelId, CancellationToken ct = default);
    Task<List<ModelSpec>> ListModelsAsync(ModelListFilter filter, CancellationToken ct = default);
    Task<bool> UpdateModelAsync(ModelSpec spec, CancellationToken ct = default);
    Task<bool> DeleteModelAsync(string modelId, CancellationToken ct = default);
    Task<ModelSpec> PromoteModelAsync(string modelId, string version, ModelStage stage, CancellationToken ct = default);
}

public sealed class ModelListFilter
{
    public string? Framework { get; set; }
    public TargetPlatform? TargetPlatform { get; set; }
    public PrecisionMode? Precision { get; set; }
    public ModelStage? Stage { get; set; }
    public string? Tag { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public enum ModelStage
{
    Development,
    Staging,
    Production,
    Deprecated,
    Archived
}

public interface IEdgeDeviceManager
{
    Task<EdgeDeviceSpec> RegisterDeviceAsync(EdgeDeviceSpec device, CancellationToken ct = default);
    Task<EdgeDeviceSpec?> GetDeviceAsync(string deviceId, CancellationToken ct = default);
    Task<List<EdgeDeviceSpec>> ListDevicesAsync(DeviceFilter filter, CancellationToken ct = default);
    Task<EdgeDeviceSpec> UpdateDeviceAsync(string deviceId, EdgeDeviceSpec device, CancellationToken ct = default);
    Task<bool> DeleteDeviceAsync(string deviceId, CancellationToken ct = default);
    Task<DeviceHealth> GetDeviceHealthAsync(string deviceId, CancellationToken ct = default);
    Task<bool> SendCommandAsync(string deviceId, string command, Dictionary<string, object> parameters, CancellationToken ct = default);
}

public sealed class DeviceFilter
{
    public TargetPlatform? Platform { get; set; }
    public string? Architecture { get; set; }
    public string? OS { get; set; }
    public bool? IsOnline { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}