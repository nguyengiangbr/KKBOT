// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KKBOT.Sim2Real.Abstractions;

/// <summary>
/// Core abstractions for Sim2Real training pipeline.
/// </summary>
public static class Sim2RealConstants
{
    public const string SimulatorUnity = "unity";
    public const string SimulatorIsaacSim = "isaac_sim";
    public const string SimulatorAirSim = "airsim";
    public const string SimulatorCustom = "custom";
}

/// <summary>
/// Domain randomization configuration.
/// </summary>
public sealed record DomainRandomizationConfig
{
    public string SimulatorType { get; init; } = Sim2RealConstants.SimulatorUnity;
    public RandomizationSpace Space { get; init; } = new();
    public int NumEnvironments { get; init; } = 64;
    public int StepsPerEpisode { get; init; } = 1000;
    public int TotalTrainingSteps { get; init; } = 10_000_000;
    public string OutputPath { get; init; } = "./sim2real_output";
    public bool EnableVisualRandomization { get; init; } = true;
    public bool EnablePhysicsRandomization { get; init; } = true;
    public bool EnableLightingRandomization { get; init; } = true;
    public bool EnableTextureRandomization { get; init; } = true;
    public bool EnableCameraRandomization { get; init; } = true;
    public RandomizationCurriculum Curriculum { get; init; } = new();
}

public sealed record RandomizationSpace
{
    // Visual parameters
    public ParameterRange Brightness { get; init; } = new(0.5, 1.5);
    public ParameterRange Contrast { get; init; } = new(0.5, 1.5);
    public ParameterRange Saturation { get; init; } = new(0.5, 1.5);
    public ParameterRange Hue { get; init; } = new(-0.1, 0.1);
    public ParameterRange GaussianNoise { get; init; } = new(0, 0.05);
    public ParameterRange MotionBlur { get; init; } = new(0, 0.02);
    
    // Texture parameters
    public ParameterRange TextureScale { get; init; } = new(0.8, 1.2);
    public ParameterRange TextureRotation { get; init; } = new(-180, 180);
    public string[] TextureAssets { get; init; } = Array.Empty<string>();
    
    // Lighting parameters
    public ParameterRange LightIntensity { get; init; } = new(0.3, 3.0);
    public ParameterRange LightColorTemperature { get; init; } = new(2000, 10000);
    public ParameterRange ShadowStrength { get; init; } = new(0, 1);
    public ParameterRange AmbientIntensity { get; init; } = new(0.1, 1.0);
    
    // Camera parameters
    public ParameterRange FOV { get; init; } = new(60, 120);
    public ParameterRange CameraHeight { get; init; } = new(0.5, 3.0);
    public ParameterRange CameraTilt { get; init; } = new(-30, 30);
    public ParameterRange CameraNoise { get; init; } = new(0, 0.01);
    
    // Physics parameters
    public ParameterRange Gravity { get; init; } = new(9.5, 10.5);
    public ParameterRange Mass { get; init; } = new(0.5, 2.0);
    public ParameterRange Friction { get; init; } = new(0.1, 2.0);
    public ParameterRange Restitution { get; init; } = new(0, 1.0);
    public ParameterRange JointDamping { get; init; } = new(0, 0.5);
    public ParameterRange JointStiffness { get; init; } = new(10, 1000);
    
    // Environment parameters
    public ParameterRange WindForce { get; init; } = new(0, 5.0);
    public ParameterRange WindDirection { get; init; } = new(0, 360);
    public ParameterRange GroundFriction { get; init; } = new(0.1, 2.0);
}

public sealed record ParameterRange
{
    public double Min { get; init; }
    public double Max { get; init; }
    public ParameterDistribution Distribution { get; init; } = ParameterDistribution.Uniform;
    
    public ParameterRange(double min, double max)
    {
        Min = min;
        Max = max;
    }
}

public enum ParameterDistribution
{
    Uniform,
    Normal,
    LogUniform,
    Beta
}

public sealed record RandomizationCurriculum
{
    public bool Enabled { get; init; } = true;
    public int WarmupSteps { get; init; } = 100000;
    public int CurriculumSteps { get; init; } = 1000000;
    public CurriculumStage[] Stages { get; init; } = Array.Empty<CurriculumStage>();
}

public sealed record CurriculumStage
{
    public string Name { get; init; } = string.Empty;
    public int StartStep { get; init; }
    public int EndStep { get; init; }
    public RandomizationSpace Space { get; init; } = new();
    public double Difficulty { get; init; } = 1.0; // 0-1
}

/// <summary>
/// Domain adaptation configuration.
/// </summary>
public sealed record DomainAdaptationConfig
{
    public string Method { get; init; } = "cyclegan"; // cyclegan, dr_cyclegan, simopt, adat, dacs
    public string SourceDomain { get; init; } = "simulation";
    public string TargetDomain { get; init; } = "real";
    public string ModelArchitecture { get; init; } = "resnet50"; // resnet50, efficientnet, vit
    public int ImageSize { get; init; } = 256;
    public int BatchSize { get; init; } = 16;
    public double LearningRate { get; init; } = 2e-4;
    public int Epochs { get; init; } = 100;
    public double LambdaCycle { get; init; } = 10.0;
    public double LambdaIdentity { get; init; } = 0.5;
    public double LambdaPerceptual { get; init; } = 1.0;
    public bool UseMixedPrecision { get; init; } = true;
    public string[] SourceDataPaths { get; init; } = Array.Empty<string>();
    public string[] TargetDataPaths { get; init; } = Array.Empty<string>();
    public string OutputPath { get; init; } = "./domain_adaptation";
    public bool EnableTensorboard { get; init; } = true;
}

/// <summary>
/// Reward modeling configuration.
/// </summary>
public sealed record RewardModelingConfig
{
    public string Method { get; init; } = "gail"; // gail, sqil, cql, iql, offline_rl
    public string ExpertDataPath { get; init; } = string.Empty;
    public string OfflineDataPath { get; init; } = string.Empty;
    public int StateDim { get; init; } = 256;
    public int ActionDim { get; init; } = 10;
    public int HiddenDim { get; init; } = 512;
    public double LearningRate { get; init; } = 3e-4;
    public int BatchSize { get; init; } = 256;
    public int Epochs { get; init; } = 100;
    public double DiscountFactor { get; init; } = 0.99;
    public double GAELambda { get; init; } = 0.95;
    public double ClipRatio { get; init; } = 0.2;
    public double EntropyCoeff { get; init; } = 0.01;
    public double ValueLossCoeff { get; init; } = 0.5;
    public int NumEnvs { get; init; } = 16;
    public int RolloutSteps { get; init; } = 2048;
    public int MinibatchSize { get; init; } = 64;
    public int PpoEpochs { get; init; } = 10;
    public string OutputPath { get; init; } = "./reward_modeling";
    public bool EnableWandb { get; init; } = false;
}

/// <summary>
/// Policy deployment configuration.
/// </summary>
public sealed record PolicyDeploymentConfig
{
    public string PolicyPath { get; init; } = string.Empty;
    public string ExportFormat { get; init; } = "onnx"; // onnx, torchscript, tensorrt, wasm
    public string TargetDevice { get; init; } = "cpu"; // cpu, gpu, jetson, rpi
    public bool EnableQuantization { get; init; } = true;
    public string QuantizationMode { get; init; } = "INT8"; // INT8, INT4, FP16
    public int CalibrationBatchSize { get; init; } = 32;
    public int CalibrationSteps { get; init; } = 100;
    public bool EnableOptimization { get; init; } = true;
    public OptimizationLevel OptimizationLevel { get; init; } = OptimizationLevel.O2;
    public string OutputPath { get; init; } = "./deployed_policy";
    public bool GenerateBenchmark { get; init; } = true;
    public BenchmarkConfig Benchmark { get; init; } = new();
}

public enum OptimizationLevel
{
    O0, // No optimization
    O1, // Basic optimization
    O2, // Full optimization
    O3 // Aggressive optimization
}

public sealed record BenchmarkConfig
{
    public int WarmupRuns { get; init; } = 10;
    public int BenchmarkRuns { get; init; } = 1000;
    public int BatchSizes { get; init; } = 1; // 1, 4, 8, 16, 32
    public bool MeasureLatency { get; init; } = true;
    public bool MeasureThroughput { get; init; } = true;
    public bool MeasureMemory { get; init; } = true;
    public bool MeasurePower { get; init; } = false;
}

/// <summary>
/// Evaluation configuration.
/// </summary>
public sealed record EvaluationConfig
{
    public string PolicyPath { get; init; } = string.Empty;
    public string SimulatorConfig { get; init; } = string.Empty;
    public string RealWorldConfig { get; init; } = string.Empty;
    public int NumEpisodes { get; init; } = 100;
    public int MaxStepsPerEpisode { get; init; } = 1000;
    public EvaluationMetrics[] Metrics { get; init; } = 
    {
        EvaluationMetrics.SuccessRate,
        EvaluationMetrics.AverageReward,
        EvaluationMetrics.EpisodeLength,
        EvaluationMetrics.SafetyViolations,
        EvaluationMetrics.Sim2RealGap
    };
    public string OutputPath { get; init; } = "./evaluation";
    public bool RecordVideo { get; init; } = true;
    public bool RecordTrajectories { get; init; } = true;
    public StatisticalTestConfig StatisticalTest { get; init; } = new();
}

public enum EvaluationMetrics
{
    SuccessRate,
    AverageReward,
    EpisodeLength,
    SafetyViolations,
    Sim2RealGap,
    SampleEfficiency,
    Robustness
}

public sealed record StatisticalTestConfig
{
    public bool Enabled { get; init; } = true;
    public double SignificanceLevel { get; init; } = 0.05;
    public int BootstrapSamples { get; init; } = 10000;
    public string[] MetricsToTest { get; init; } = 
    {
        "success_rate", "average_reward", "episode_length"
    };
}

/// <summary>
/// Training job specification.
/// </summary>
public sealed record Sim2RealTrainingJob
{
    public string JobId { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = string.Empty;
    public Sim2RealStage Stage { get; init; }
    public DomainRandomizationConfig DomainRandomization { get; init; } = new();
    public DomainAdaptationConfig DomainAdaptation { get; init; } = new();
    public RewardModelingConfig RewardModeling { get; init; } = new();
    public PolicyDeploymentConfig PolicyDeployment { get; init; } = new();
    public EvaluationConfig Evaluation { get; init; } = new();
    public TrainingStatus Status { get; init; } = TrainingStatus.Pending;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public Sim2RealMetrics Metrics { get; init; } = new();
    public string ErrorMessage { get; init; } = string.Empty;
}

public enum Sim2RealStage
{
    DomainRandomization,
    DomainAdaptation,
    RewardModeling,
    PolicyTraining,
    PolicyDeployment,
    Evaluation,
    Complete
}

public enum TrainingStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

public sealed record Sim2RealMetrics
{
    // Domain Randomization
    public double DR_TrainingReward { get; init; }
    public double DR_SuccessRate { get; init; }
    public int DR_TotalSteps { get; init; }
    
    // Domain Adaptation
    public double DA_FID { get; init; } // Fréchet Inception Distance
    public double DA_LPIPS { get; init; } // Learned Perceptual Image Patch Similarity
    public double DA_SSIM { get; init; } // Structural Similarity Index
    
    // Reward Modeling
    public double RM_RewardAccuracy { get; init; }
    public double RM_PolicyPerformance { get; init; }
    
    // Policy Deployment
    public double Deploy_LatencyMs { get; init; }
    public double Deploy_ThroughputFPS { get; init; }
    public long Deploy_MemoryMB { get; init; }
    public double Deploy_ModelSizeMB { get; init; }
    
    // Evaluation
    public double Eval_SimSuccessRate { get; init; }
    public double Eval_RealSuccessRate { get; init; }
    public double Eval_Sim2RealGap { get; init; }
    public double Eval_AverageReward { get; init; }
    public int Eval_SafetyViolations { get; init; }
    
    // Overall
    public double OverallScore { get; init; }
}

/// <summary>
/// Service interfaces.
/// </summary>
public interface IDomainRandomizer
{
    Task<RandomizationResult> RandomizeAsync(DomainRandomizationConfig config, CancellationToken ct = default);
    Task<RandomizationSample> SampleAsync(string environmentId, CancellationToken ct = default);
    Task ApplyRandomizationAsync(string environmentId, RandomizationSample sample, CancellationToken ct = default);
}

public sealed record RandomizationResult
{
    public bool Success { get; init; }
    public string[] EnvironmentIds { get; init; } = Array.Empty<string>();
    public RandomizationSpace AppliedSpace { get; init; } = new();
    public string ErrorMessage { get; init; } = string.Empty;
}

public sealed record RandomizationSample
{
    public string SampleId { get; init; } = Guid.NewGuid().ToString("N");
    public Dictionary<string, object> VisualParams { get; init; } = new();
    public Dictionary<string, object> PhysicsParams { get; init; } = new();
    public Dictionary<string, object> LightingParams { get; init; } = new();
    public Dictionary<string, object> CameraParams { get; init; } = new();
    public Dictionary<string, object> EnvironmentParams { get; init; } = new();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

public interface IDomainAdapter
{
    Task<AdaptationResult> AdaptAsync(DomainAdaptationConfig config, CancellationToken ct = default);
    Task<AdaptationMetrics> GetMetricsAsync(string modelId, CancellationToken ct = default);
}

public sealed record AdaptationResult
{
    public bool Success { get; init; }
    public string ModelPath { get; init; } = string.Empty;
    public AdaptationMetrics Metrics { get; init; } = new();
    public string ErrorMessage { get; init; } = string.Empty;
}

public sealed record AdaptationMetrics
{
    public double FID { get; init; }
    public double LPIPS { get; init; }
    public double SSIM { get; init; }
    public double PSNR { get; init; }
    public double CycleConsistencyLoss { get; init; }
    public double IdentityLoss { get; init; }
    public int Epoch { get; init; }
    public double TrainingTimeHours { get; init; }
}

public interface IRewardModeler
{
    Task<RewardModelResult> TrainRewardModelAsync(RewardModelingConfig config, CancellationToken ct = default);
    Task<double> PredictRewardAsync(string modelId, float[] state, int action, float[] nextState, CancellationToken ct = default);
}

public sealed record RewardModelResult
{
    public bool Success { get; init; }
    public string ModelPath { get; init; } = string.Empty;
    public RewardModelMetrics Metrics { get; init; } = new();
    public string ErrorMessage { get; init; } = string.Empty;
}

public sealed record RewardModelMetrics
{
    public double Accuracy { get; init; }
    public double AUC { get; init; }
    public double RewardCorrelation { get; init; }
    public double PolicyReturn { get; init; }
    public int Epoch { get; init; }
}

public interface IPolicyDeployer
{
    Task<DeploymentResult> DeployAsync(PolicyDeploymentConfig config, CancellationToken ct = default);
    Task<BenchmarkResult> BenchmarkAsync(string modelPath, BenchmarkConfig config, CancellationToken ct = default);
}

public sealed record DeploymentResult
{
    public bool Success { get; init; }
    public string ModelPath { get; init; } = string.Empty;
    public DeploymentMetrics Metrics { get; init; } = new();
    public string ErrorMessage { get; init; } = string.Empty;
}

public sealed record DeploymentMetrics
{
    public double LatencyMs { get; init; }
    public double ThroughputFPS { get; init; }
    public long MemoryMB { get; init; }
    public double ModelSizeMB { get; init; }
    public double AccuracyDrop { get; init; } // vs original model
}

public sealed record BenchmarkResult
{
    public bool Success { get; init; }
    public double AvgLatencyMs { get; init; }
    public double P50LatencyMs { get; init; }
    public double P95LatencyMs { get; init; }
    public double P99LatencyMs { get; init; }
    public double ThroughputFPS { get; init; }
    public long PeakMemoryMB { get; init; }
    public Dictionary<int, double> LatencyByBatchSize { get; init; } = new();
    public string ErrorMessage { get; init; } = string.Empty;
}

public interface IEvaluator
{
    Task<EvaluationResult> EvaluateAsync(EvaluationConfig config, CancellationToken ct = default);
    Task<Sim2RealGapReport> ComputeSim2RealGapAsync(string simResultsPath, string realResultsPath, CancellationToken ct = default);
}

public sealed record EvaluationResult
{
    public bool Success { get; init; }
    public EvaluationMetricsResults Metrics { get; init; } = new();
    public string[] VideoPaths { get; init; } = Array.Empty<string>();
    public string[] TrajectoryPaths { get; init; } = Array.Empty<string>();
    public StatisticalTestResults StatisticalTests { get; init; } = new();
    public string ErrorMessage { get; init; } = string.Empty;
}

public sealed record EvaluationMetricsResults
{
    public double SuccessRate { get; init; }
    public double AverageReward { get; init; }
    public double AverageEpisodeLength { get; init; }
    public int SafetyViolations { get; init; }
    public double SampleEfficiency { get; init; }
    public Dictionary<string, double> PerTaskMetrics { get; init; } = new();
}

public sealed record StatisticalTestResults
{
    public Dictionary<string, StatisticalTestResult> Tests { get; init; } = new();
    public bool OverallSignificant { get; init; }
}

public sealed record StatisticalTestResult
{
    public string TestName { get; init; } = string.Empty;
    public double PValue { get; init; }
    public double EffectSize { get; init; }
    public double ConfidenceIntervalLow { get; init; }
    public double ConfidenceIntervalHigh { get; init; }
    public bool Significant { get; init; }
}

public sealed record Sim2RealGapReport
{
    public double SuccessRateGap { get; init; }
    public double RewardGap { get; init; }
    public double EpisodeLengthGap { get; init; }
    public int SafetyViolationGap { get; init; }
    public double OverallGap { get; init; }
    public Dictionary<string, double> PerTaskGaps { get; init; } = new();
    public string Analysis { get; init; } = string.Empty;
    public string[] Recommendations { get; init; } = Array.Empty<string>();
}
