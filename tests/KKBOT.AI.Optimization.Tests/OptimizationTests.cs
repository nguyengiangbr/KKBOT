using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KKBOT.AI.Optimization.Abstractions;
using KKBOT.AI.Optimization.Models;
using KKBOT.AI.Optimization.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KKBOT.AI.Optimization.Tests;

public class OptimizationTests
{
    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());
        services.Configure<OptimizationOptions>(o => 
        {
            o.DefaultTimeoutMinutes = 10;
            o.MaxConcurrentOptimizations = 2;
            o.FailOnAccuracyDrop = false; // For testing
        });
        services.AddSingleton<MLContext>();
        
        var provider = services.BuildServiceProvider();
        return provider;
    }

    [Fact]
    public void OptimizationConfig_Defaults()
    {
        var config = new OptimizationConfig();
        
        Assert.NotEqual(Guid.Empty, Guid.Parse(config.OptimizationId));
        Assert.Equal(PrecisionMode.Fp16, config.PrecisionMode);
        Assert.Equal(TargetPlatform.Cpu, config.TargetPlatform);
        Assert.True(config.EnabledStages.Contains(OptimizationStage.GraphOptimization));
        Assert.True(config.Quantization.Enabled);
        Assert.Equal(PrecisionMode.Int8, config.Quantization.Mode);
    }

    [Fact]
    public void QuantizationConfig_Defaults()
    {
        var config = new QuantizationConfig();
        
        Assert.True(config.Enabled);
        Assert.Equal(PrecisionMode.Int8, config.Mode);
        Assert.Equal(QuantizationGranularity.PerTensor, config.Granularity);
        Assert.Equal(QuantizationAlgorithm.MinMax, config.Algorithm);
        Assert.False(config.PerChannel);
        Assert.True(config.Symmetric);
    }

    [Fact]
    public void CalibrationConfig_Defaults()
    {
        var config = new CalibrationConfig();
        
        Assert.True(config.Enabled);
        Assert.Equal(32, config.BatchSize);
        Assert.Equal(100, config.NumBatches);
        Assert.True(config.UseEntropyCalibration);
        Assert.Equal(99.99, config.Percentile);
    }

    [Fact]
    public void BenchmarkConfig_Defaults()
    {
        var config = new BenchmarkConfig();
        
        Assert.True(config.Enabled);
        Assert.Equal(10, config.WarmupRuns);
        Assert.Equal(100, config.BenchmarkRuns);
        Assert.Equal(1, config.BatchSize);
        Assert.Equal(10, config.MinDurationSeconds);
    }

    [Fact]
    public void OptimizationPipeline_Validates()
    {
        var pipeline = new OptimizationPipeline
        {
            PipelineId = "test-pipeline",
            Name = "Test Pipeline",
            Steps = new List<PipelineStep>
            {
                new PipelineStep { StepId = "step1", Name = "Step 1", Stage = OptimizationStage.GraphOptimization },
                new PipelineStep { StepId = "step2", Name = "Step 2", Stage = OptimizationStage.Quantization, DependsOn = new() { "step1" } }
            }
        };

        Assert.Equal(2, pipeline.Steps.Count);
        Assert.Contains(pipeline.Steps, s => s.Stage == OptimizationStage.GraphOptimization);
        Assert.Contains(pipeline.Steps, s => s.Stage == OptimizationStage.Quantization);
    }

    [Fact]
    public void ModelSpec_Properties()
    {
        var spec = new ModelSpec
        {
            ModelId = "test-model",
            Name = "Test Model",
            Version = "1.0.0",
            Framework = "ONNX",
            Inputs = new List<ModelTensorSpec>
            {
                new ModelTensorSpec { Name = "input", Shape = new[] { 1, 3, 224, 224 }, DataType = "float32" }
            },
            Outputs = new List<ModelTensorSpec>
            {
                new ModelTensorSpec { Name = "output", Shape = new[] { 1, 1000 }, DataType = "float32" }
            }
        };

        Assert.Equal("test-model", spec.ModelId);
        Assert.Single(spec.Inputs);
        Assert.Single(spec.Outputs);
        Assert.Equal(4, spec.Inputs[0].Shape.Length);
    }

    [Fact]
    public void QuantizationConfig_Modes()
    {
        var int8Config = new QuantizationConfig { Mode = PrecisionMode.Int8 };
        var fp16Config = new QuantizationConfig { Mode = PrecisionMode.Fp16 };
        var int4Config = new QuantizationConfig { Mode = PrecisionMode.Int4 };

        Assert.Equal(PrecisionMode.Int8, int8Config.Mode);
        Assert.Equal(PrecisionMode.Fp16, fp16Config.Mode);
        Assert.Equal(PrecisionMode.Int4, int4Config.Mode);
    }

    [Fact]
    public void DeploymentPackage_Properties()
    {
        var pkg = new DeploymentPackage
        {
            PackageId = "pkg-123",
            ModelId = "model-1",
            Name = "Test Deployment",
            Version = "1.0.0",
            TargetPlatform = TargetPlatform.NvidiaJetson,
            Precision = PrecisionMode.Int8
        };

        Assert.Equal("pkg-123", pkg.PackageId);
        Assert.Equal(TargetPlatform.NvidiaJetson, pkg.TargetPlatform);
        Assert.Equal(PrecisionMode.Int8, pkg.Precision);
    }

    [Fact]
    public void EdgeDeviceSpec_Defaults()
    {
        var device = new EdgeDeviceSpec
        {
            DeviceId = "device-1",
            Name = "Jetson Orin",
            Platform = TargetPlatform.NvidiaJetson,
            Architecture = "arm64",
            OS = "Linux",
            OSVersion = "Ubuntu 22.04",
            CpuCores = 12,
            TotalMemoryMB = 32768,
            HasGpu = true,
            GpuModel = "Orin",
            GpuMemoryMB = 32768,
            ComputeCapability = 87
        };

        Assert.Equal("device-1", device.DeviceId);
        Assert.Equal(TargetPlatform.NvidiaJetson, device.Platform);
        Assert.True(device.HasGpu);
        Assert.Equal(87, device.ComputeCapability);
    }

    [Fact]
    public void OptimizationPipeline_StageOrdering()
    {
        var pipeline = new OptimizationPipeline
        {
            Steps = new List<PipelineStep>
            {
                new PipelineStep { StepId = "a", Name = "A", Stage = OptimizationStage.GraphOptimization, DependsOn = new() },
                new PipelineStep { StepId = "b", Name = "B", Stage = OptimizationStage.Quantization, DependsOn = new() { "a" } },
                new PipelineStep { StepId = "c", Name = "C", Stage = OptimizationStage.CodeGeneration, DependsOn = new() { "b" } }
            };

        // Verify dependencies are set correctly
        var stepA = pipeline.Steps.First(s => s.StepId == "a");
        var stepB = pipeline.Steps.First(s => s.StepId == "b");
        var stepC = pipeline.Steps.First(s => s.StepId == "c");

        Assert.Empty(stepA.DependsOn);
        Assert.Contains("a", stepB.DependsOn);
        Assert.Contains("b", stepC.DependsOn);
    }

    [Fact]
    public void QuantizationConfig_Algorithms()
    {
        var algorithms = Enum.GetValues<QuantizationAlgorithm>();
        
        Assert.Contains(QuantizationAlgorithm.MinMax, algorithms);
        Assert.Contains(QuantizationAlgorithm.Percentile, algorithms);
        Assert.Contains(QuantizationAlgorithm.Entropy, algorithms);
        Assert.Contains(QuantizationAlgorithm.KL, algorithms);
        Assert.Contains(QuantizationAlgorithm.AWQ, algorithms);
        Assert.Contains(QuantizationAlgorithm.GPTQ, algorithms);
    }

    [Fact]
    public void PrecisionMode_Values()
    {
        var modes = Enum.GetValues<PrecisionMode>();
        
        Assert.Contains(PrecisionMode.Fp32, modes);
        Assert.Contains(PrecisionMode.Fp16, modes);
        Assert.Contains(PrecisionMode.Int8, modes);
        Assert.Contains(PrecisionMode.Int4, modes);
        Assert.Contains(PrecisionMode.Mixed, modes);
        Assert.Contains(PrecisionMode.Dynamic, modes);
        Assert.Contains(PrecisionMode.Auto, modes);
    }

    [Fact]
    public void TargetPlatform_Values()
    {
        var platforms = Enum.GetValues<TargetPlatform>();
        
        Assert.Contains(TargetPlatform.Cpu, platforms);
        Assert.Contains(TargetPlatform.NvidiaGpu, platforms);
        Assert.Contains(TargetPlatform.NvidiaJetson, platforms);
        Assert.Contains(TargetPlatform.AppleNeuralEngine, platforms);
        Assert.Contains(TargetPlatform.RaspberryPi, platforms);
        Assert.Contains(TargetPlatform.CoralTpu, platforms);
    }

    [Fact]
    public void OptimizationStage_AllStages()
    {
        var stages = Enum.GetValues<OptimizationStage>();
        
        Assert.Contains(OptimizationStage.GraphOptimization, stages);
        Assert.Contains(OptimizationStage.ConstantFolding, stages);
        Assert.Contains(OptimizationStage.DeadCodeElimination, stages);
        Assert.Contains(OptimizationStage.OperatorFusion, stages);
        Assert.Contains(OptimizationStage.LayoutOptimization, stages);
        Assert.Contains(OptimizationStage.Quantization, stages);
        Assert.Contains(OptimizationStage.Calibration, stages);
        Assert.Contains(OptimizationStage.KernelSelection, stages);
        Assert.Contains(OptimizationStage.MemoryPlanning, stages);
        Assert.Contains(OptimizationStage.CodeGeneration, stages);
        Assert.Contains(OptimizationStage.Verification, stages);
        Assert.Contains(OptimizationStage.Benchmarking, stages);
        Assert.Contains(OptimizationStage.Packaging, stages);
    }

    [Fact]
    public void TensorRTConversionOptions_Defaults()
    {
        var options = new TensorRTConversionOptions();
        
        Assert.Equal(PrecisionMode.Fp16, options.Precision);
        Assert.Equal(1024, options.MaxWorkspaceSizeMB);
        Assert.Equal(1, options.MaxBatchSize);
        Assert.True(options.EnableFp16);
        Assert.False(options.EnableInt8);
    }

    [Fact]
    public void OptimizationStatus_Values()
    {
        var statuses = Enum.GetValues<OptimizationStatus>();
        
        Assert.Contains(OptimizationStatus.Pending, statuses);
        Assert.Contains(OptimizationStatus.Running, statuses);
        Assert.Contains(OptimizationStatus.Completed, statuses);
        Assert.Contains(OptimizationStatus.Failed, statuses);
        Assert.Contains(OptimizationStatus.Cancelled, statuses);
        Assert.Contains(OptimizationStatus.PartialSuccess, statuses);
    }
}