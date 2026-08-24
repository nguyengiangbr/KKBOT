// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KKBOT.Sim2Real.Abstractions;
using KKBOT.Observability;

namespace KKBOT.Sim2Real.DomainRandomization;

/// <summary>
/// Domain randomization engine for generating diverse simulation environments.
/// Supports: Visual, physics, lighting, camera, and texture randomization with curriculum learning.
/// </summary>
public sealed class DomainRandomizer : IDomainRandomizer, IHostedService, IDisposable
{
    private readonly ILogger<DomainRandomizer> _logger;
    private readonly DomainRandomizationOptions _options;
    private readonly IMetricsRegistry _metrics;
    private readonly ConcurrentDictionary<string, RandomizationEnvironment> _environments = new();
    private readonly Random _random = new();
    private readonly Timer? _curriculumTimer;
    private readonly CancellationTokenSource _shutdownCts = new();
    private long _totalSamples;
    private bool _disposed;

    public DomainRandomizer(
        ILogger<DomainRandomizer> logger,
        IOptions<DomainRandomizationOptions> options,
        IMetricsRegistry metrics)
    {
        _logger = logger;
        _options = options.Value;
        _metrics = metrics;

        if (_options.EnableCurriculum)
        {
            _curriculumTimer = new Timer(
                UpdateCurriculumAsync, 
                null, 
                TimeSpan.FromMinutes(_options.CurriculumUpdateIntervalMinutes),
                TimeSpan.FromMinutes(_options.CurriculumUpdateIntervalMinutes));
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Domain Randomizer started");
        
        // Initialize environments
        for (int i = 0; i < _options.DefaultNumEnvironments; i++)
        {
            var envId = $"env_{i}";
            await InitializeEnvironmentAsync(envId, cancellationToken);
        }

        _logger.LogInformation("Initialized {Count} environments", _environments.Count);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Domain Randomizer stopping");
        _shutdownCts.Cancel();
        _curriculumTimer?.Dispose();
        
        foreach (var env in _environments.Values)
        {
            env.Dispose();
        }
        _environments.Clear();
    }

    public async Task<RandomizationResult> RandomizeAsync(DomainRandomizationConfig config, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var environmentIds = new List<string>();
            
            // Create or reuse environments
            for (int i = 0; i < config.NumEnvironments; i++)
            {
                var envId = $"dr_env_{Guid.NewGuid().ToString("N")[..8]}";
                var env = await InitializeEnvironmentAsync(envId, ct);
                
                var sample = GenerateRandomizationSample(config.Space, config.Curriculum);
                await ApplyRandomizationAsync(envId, sample, ct);
                
                environmentIds.Add(envId);
            }

            stopwatch.Stop();
            _metrics.RecordHttpRequestDuration("domain_randomization", stopwatch.Elapsed.TotalMilliseconds);
            Interlocked.Add(ref _totalSamples, config.NumEnvironments);

            return new RandomizationResult
            {
                Success = true,
                EnvironmentIds = environmentIds.ToArray(),
                AppliedSpace = config.Space
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Domain randomization failed");
            return new RandomizationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<RandomizationSample> SampleAsync(string environmentId, CancellationToken ct = default)
    {
        if (!_environments.TryGetValue(environmentId, out var env))
        {
            throw new InvalidOperationException($"Environment {environmentId} not found");
        }

        var config = env.CurrentConfig;
        var sample = GenerateRandomizationSample(config.Space, config.Curriculum);
        
        env.LastSample = sample;
        env.SampleCount++;
        
        return sample;
    }

    public async Task ApplyRandomizationAsync(string environmentId, RandomizationSample sample, CancellationToken ct = default)
    {
        if (!_environments.TryGetValue(environmentId, out var env))
        {
            throw new InvalidOperationException($"Environment {environmentId} not found");
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Apply to simulator (Unity, Isaac Sim, AirSim, etc.)
            await ApplyToSimulatorAsync(env.SimulatorType, sample, ct);

            env.LastApplied = sample;
            env.LastAppliedAt = DateTimeOffset.UtcNow;

            stopwatch.Stop();
            _metrics.RecordHttpRequestDuration("apply_randomization", stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply randomization to {EnvironmentId}", environmentId);
            throw;
        }
    }

    private async Task<RandomizationEnvironment> InitializeEnvironmentAsync(string environmentId, CancellationToken ct)
    {
        if (_environments.TryGetValue(environmentId, out var existing))
        {
            return existing;
        }

        var env = new RandomizationEnvironment
        {
            EnvironmentId = environmentId,
            SimulatorType = _options.DefaultSimulatorType,
            CurrentConfig = _options.DefaultConfig ?? new DomainRandomizationConfig(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _environments[environmentId] = env;
        
        // Initialize simulator connection
        await ConnectToSimulatorAsync(env, ct);
        
        return env;
    }

    private async Task ConnectToSimulatorAsync(RandomizationEnvironment env, CancellationToken ct)
    {
        // Connect to simulator (Unity, Isaac Sim, AirSim, etc.)
        // This would establish gRPC/HTTP/WebSocket connection
        _logger.LogDebug("Connecting to {Simulator} for environment {EnvId}", 
            env.SimulatorType, env.EnvironmentId);
        
        await Task.Delay(100, ct); // Simulate connection
        env.IsConnected = true;
    }

    private async Task ApplyToSimulatorAsync(string simulatorType, RandomizationSample sample, CancellationToken ct)
    {
        // Apply randomization parameters to simulator
        switch (simulatorType.ToLower())
        {
            case Sim2RealConstants.SimulatorUnity:
                await ApplyToUnityAsync(sample, ct);
                break;
            case Sim2RealConstants.SimulatorIsaacSim:
                await ApplyToIsaacSimAsync(sample, ct);
                break;
            case Sim2RealConstants.SimulatorAirSim:
                await ApplyToAirSimAsync(sample, ct);
                break;
            default:
                await ApplyGenericAsync(sample, ct);
                break;
        }
    }

    private async Task ApplyToUnityAsync(RandomizationSample sample, CancellationToken ct)
    {
        // Send randomization commands to Unity via gRPC/HTTP
        // Example: Modify lighting, materials, camera, physics
        var commands = new List<object>();
        
        if (sample.VisualParams.Any())
        {
            commands.Add(new { type = "visual", parameters = sample.VisualParams });
        }
        if (sample.PhysicsParams.Any())
        {
            commands.Add(new { type = "physics", parameters = sample.PhysicsParams });
        }
        if (sample.LightingParams.Any())
        {
            commands.Add(new { type = "lighting", parameters = sample.LightingParams });
        }
        if (sample.CameraParams.Any())
        {
            commands.Add(new { type = "camera", parameters = sample.CameraParams });
        }

        // In real implementation, send via gRPC
        var json = JsonSerializer.Serialize(commands);
        _logger.LogTrace("Unity randomization: {Json}", json);
        
        await Task.Delay(10, ct); // Simulate network call
    }

    private async Task ApplyToIsaacSimAsync(RandomizationSample sample, CancellationToken ct)
    {
        // Apply to NVIDIA Isaac Sim via Omniverse Kit
        await Task.Delay(10, ct);
    }

    private async Task ApplyToAirSimAsync(RandomizationSample sample, CancellationToken ct)
    {
        // Apply to Microsoft AirSim
        await Task.Delay(10, ct);
    }

    private async Task ApplyGenericAsync(RandomizationSample sample, CancellationToken ct)
    {
        // Generic simulator interface
        await Task.Delay(10, ct);
    }

    private RandomizationSample GenerateRandomizationSample(RandomizationSpace space, RandomizationCurriculum curriculum)
    {
        var sample = new RandomizationSample();
        var currentStep = Interlocked.Read(ref _totalSamples);
        var difficulty = CalculateCurriculumDifficulty(currentStep, curriculum);

        // Visual randomization
        if (_options.EnableVisualRandomization)
        {
            sample.VisualParams = new Dictionary<string, object>
            {
                ["brightness"] = SampleParameter(space.Brightness, difficulty),
                ["contrast"] = SampleParameter(space.Contrast, difficulty),
                ["saturation"] = SampleParameter(space.Saturation, difficulty),
                ["hue"] = SampleParameter(space.Hue, difficulty),
                ["gaussian_noise"] = SampleParameter(space.GaussianNoise, difficulty),
                ["motion_blur"] = SampleParameter(space.MotionBlur, difficulty)
            };
        }

        // Texture randomization
        if (_options.EnableTextureRandomization && space.TextureAssets.Length > 0)
        {
            sample.VisualParams["texture_asset"] = space.TextureAssets[_random.Next(space.TextureAssets.Length)];
            sample.VisualParams["texture_scale"] = SampleParameter(space.TextureScale, difficulty);
            sample.VisualParams["texture_rotation"] = SampleParameter(space.TextureRotation, difficulty);
        }

        // Lighting randomization
        if (_options.EnableLightingRandomization)
        {
            sample.LightingParams = new Dictionary<string, object>
            {
                ["light_intensity"] = SampleParameter(space.LightIntensity, difficulty),
                ["light_color_temp"] = SampleParameter(space.LightColorTemperature, difficulty),
                ["shadow_strength"] = SampleParameter(space.ShadowStrength, difficulty),
                ["ambient_intensity"] = SampleParameter(space.AmbientIntensity, difficulty)
            };
        }

        // Camera randomization
        if (_options.EnableCameraRandomization)
        {
            sample.CameraParams = new Dictionary<string, object>
            {
                ["fov"] = SampleParameter(space.FOV, difficulty),
                ["camera_height"] = SampleParameter(space.CameraHeight, difficulty),
                ["camera_tilt"] = SampleParameter(space.CameraTilt, difficulty),
                ["camera_noise"] = SampleParameter(space.CameraNoise, difficulty)
            };
        }

        // Physics randomization
        if (_options.EnablePhysicsRandomization)
        {
            sample.PhysicsParams = new Dictionary<string, object>
            {
                ["gravity"] = SampleParameter(space.Gravity, difficulty),
                ["mass"] = SampleParameter(space.Mass, difficulty),
                ["friction"] = SampleParameter(space.Friction, difficulty),
                ["restitution"] = SampleParameter(space.Restitution, difficulty),
                ["joint_damping"] = SampleParameter(space.JointDamping, difficulty),
                ["joint_stiffness"] = SampleParameter(space.JointStiffness, difficulty)
            };
        }

        // Environment randomization
        sample.EnvironmentParams = new Dictionary<string, object>
        {
            ["wind_force"] = SampleParameter(space.WindForce, difficulty),
            ["wind_direction"] = SampleParameter(space.WindDirection, difficulty),
            ["ground_friction"] = SampleParameter(space.GroundFriction, difficulty)
        };

        return sample;
    }

    private double SampleParameter(ParameterRange range, double difficulty)
    {
        var scaledRange = ScaleRangeByDifficulty(range, difficulty);
        
        return range.Distribution switch
        {
            ParameterDistribution.Uniform => _random.NextDouble() * (scaledRange.Max - scaledRange.Min) + scaledRange.Min,
            ParameterDistribution.Normal => SampleNormal(scaledRange.Min, scaledRange.Max),
            ParameterDistribution.LogUniform => Math.Exp(_random.NextDouble() * (Math.Log(scaledRange.Max) - Math.Log(scaledRange.Min)) + Math.Log(scaledRange.Min)),
            ParameterDistribution.Beta => SampleBeta(scaledRange.Min, scaledRange.Max),
            _ => scaledRange.Min
        };
    }

    private ParameterRange ScaleRangeByDifficulty(ParameterRange range, double difficulty)
    {
        // Scale range based on curriculum difficulty (0-1)
        // At difficulty 0: narrow range around center
        // At difficulty 1: full range
        var center = (range.Min + range.Max) / 2;
        var halfWidth = (range.Max - range.Min) / 2;
        var scaledHalfWidth = halfWidth * Math.Max(0.1, difficulty); // Min 10% of range
        
        return new ParameterRange
        {
            Min = center - scaledHalfWidth,
            Max = center + scaledHalfWidth,
            Distribution = range.Distribution
        };
    }

    private double CalculateCurriculumDifficulty(long currentStep, RandomizationCurriculum curriculum)
    {
        if (!curriculum.Enabled) return 1.0;
        
        if (currentStep < curriculum.WarmupSteps)
        {
            return 0.1 + 0.3 * (currentStep / (double)curriculum.WarmupSteps);
        }
        
        if (currentStep < curriculum.WarmupSteps + curriculum.CurriculumSteps)
        {
            var progress = (currentStep - curriculum.WarmupSteps) / (double)curriculum.CurriculumSteps;
            return 0.4 + 0.6 * progress;
        }
        
        return 1.0;
    }

    private double SampleNormal(double min, double max)
    {
        var mean = (min + max) / 2;
        var std = (max - min) / 6; // 99.7% within range
        var u1 = 1.0 - _random.NextDouble();
        var u2 = 1.0 - _random.NextDouble();
        var z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return Math.Clamp(mean + z * std, min, max);
    }

    private double SampleBeta(double min, double max)
    {
        // Beta(2, 2) - peaked at center
        var a = 2.0;
        var b = 2.0;
        var u1 = _random.NextDouble();
        var u2 = _random.NextDouble();
        var x = Math.Pow(u1, 1.0 / a);
        var y = Math.Pow(u2, 1.0 / b);
        var beta = x / (x + y);
        return min + beta * (max - min);
    }

    private async void UpdateCurriculumAsync(object? state)
    {
        if (_shutdownCts.Token.IsCancellationRequested) return;

        try
        {
            var currentStep = Interlocked.Read(ref _totalSamples);
            _logger.LogDebug("Curriculum update at step {Step}", currentStep);
            
            // Update curriculum for all environments
            foreach (var env in _environments.Values)
            {
                var difficulty = CalculateCurriculumDifficulty(currentStep, env.CurrentConfig.Curriculum);
                // Adjust environment difficulty
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Curriculum update failed");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _shutdownCts.Cancel();
        
        foreach (var env in _environments.Values)
        {
            env.Dispose();
        }
        _environments.Clear();
    }
}

internal sealed class RandomizationEnvironment : IDisposable
{
    public string EnvironmentId { get; init; } = string.Empty;
    public string SimulatorType { get; init; } = string.Empty;
    public DomainRandomizationConfig CurrentConfig { get; set; } = new();
    public bool IsConnected { get; set; }
    public RandomizationSample? LastSample { get; set; }
    public RandomizationSample? LastApplied { get; set; }
    public DateTimeOffset LastAppliedAt { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public long SampleCount { get; set; }

    public void Dispose()
    {
        // Cleanup simulator connection
    }
}

/// <summary>
/// Domain randomization configuration.
/// </summary>
public sealed class DomainRandomizationOptions
{
    public string DefaultSimulatorType { get; set; } = Sim2RealConstants.SimulatorUnity;
    public DomainRandomizationConfig DefaultConfig { get; set; } = new();
    public int DefaultNumEnvironments { get; set; } = 64;
    public bool EnableVisualRandomization { get; set; } = true;
    public bool EnablePhysicsRandomization { get; set; } = true;
    public bool EnableLightingRandomization { get; set; } = true;
    public bool EnableTextureRandomization { get; set; } = true;
    public bool EnableCameraRandomization { get; set; } = true;
    public bool EnableCurriculum { get; set; } = true;
    public int CurriculumUpdateIntervalMinutes { get; set; } = 30;
}

/// <summary>
/// DI Extensions.
/// </summary>
public static class DomainRandomizationExtensions
{
    public static IServiceCollection AddKkbotDomainRandomization(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DomainRandomizationOptions>(configuration.GetSection("DomainRandomization"));
        services.AddSingleton<IDomainRandomizer, DomainRandomizer>();
        services.AddHostedService(sp => sp.GetRequiredService<IDomainRandomizer>());
        return services;
    }
}
