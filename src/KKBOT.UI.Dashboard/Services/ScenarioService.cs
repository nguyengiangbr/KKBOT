// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using KKBOT.Device.Simulator;

namespace KKBOT.UI.Dashboard.Services;

/// <summary>
/// Service for managing simulation scenarios in the dashboard.
/// </summary>
public interface IScenarioService
{
    Task<SimulationScenario?> GetAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<SimulationScenario>> GetAllAsync(CancellationToken ct = default);
    Task<SimulationScenario> SaveAsync(SimulationScenario scenario, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
    Task<ScenarioValidationResult> ValidateAsync(SimulationScenario scenario, CancellationToken ct = default);
    Task<ScenarioExecutionResult> ExecuteAsync(SimulationScenario scenario, CancellationToken ct = default);
    Task<string> ExportAsync(string id, CancellationToken ct = default);
    Task<SimulationScenario> ImportAsync(string json, CancellationToken ct = default);
}

public sealed record ScenarioValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);

public sealed record ScenarioExecutionResult(
    bool IsSuccess,
    string ScenarioId,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    TimeSpan Duration,
    string? ErrorMessage,
    IReadOnlyList<StepExecutionResult> StepResults);

public sealed record StepExecutionResult(
    int StepNumber,
    string StepType,
    bool IsSuccess,
    TimeSpan Duration,
    IReadOnlyDictionary<string, object> Output,
    string? ErrorMessage);

/// <summary>
/// Implementation of scenario service using file-based storage.
/// </summary>
public sealed class ScenarioService : IScenarioService
{
    private readonly ILogger<ScenarioService> _logger;
    private readonly ScenarioParser _parser;
    private readonly ScenarioRunner _runner;
    private readonly IDeviceSessionFactory _deviceFactory;
    private readonly IPerceptionPipeline? _perceptionPipeline;
    private readonly ConcurrentDictionary<string, SimulationScenario> _scenarios = new();
    private readonly string _scenariosDirectory;

    public ScenarioService(
        ILogger<ScenarioService> logger,
        ScenarioParser parser,
        ScenarioRunner runner,
        IDeviceSessionFactory deviceFactory,
        IPerceptionPipeline? perceptionPipeline = null)
    {
        _logger = logger;
        _parser = parser;
        _runner = runner;
        _deviceFactory = deviceFactory;
        _perceptionPipeline = perceptionPipeline;
        
        _scenariosDirectory = Path.Combine(AppContext.BaseDirectory, "scenarios");
        Directory.CreateDirectory(_scenariosDirectory);
        
        LoadScenarios();
    }

    public async Task<SimulationScenario?> GetAsync(string id, CancellationToken ct = default)
    {
        if (_scenarios.TryGetValue(id, out var scenario))
        {
            return scenario;
        }
        
        // Try loading from file
        var filePath = Path.Combine(_scenariosDirectory, $"{id}.json");
        if (File.Exists(filePath))
        {
            var loaded = await _parser.LoadAsync(filePath, ct);
            if (loaded != null)
            {
                _scenarios[loaded.Id] = loaded;
                return loaded;
            }
        }
        
        return null;
    }

    public async Task<IReadOnlyList<SimulationScenario>> GetAllAsync(CancellationToken ct = default)
    {
        var result = new List<SimulationScenario>();
        
        foreach (var file in Directory.GetFiles(_scenariosDirectory, "*.json"))
        {
            try
            {
                var scenario = await _parser.LoadAsync(file, ct);
                if (scenario != null)
                {
                    _scenarios[scenario.Id] = scenario;
                    result.Add(scenario);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load scenario from {File}", file);
            }
        }
        
        return result.OrderBy(s => s.Name).ToList().AsReadOnly();
    }

    public async Task<SimulationScenario> SaveAsync(SimulationScenario scenario, CancellationToken ct = default)
    {
        if (scenario == null) throw new ArgumentNullException(nameof(scenario));
        if (string.IsNullOrWhiteSpace(scenario.Id))
        {
            scenario = scenario with { Id = Guid.NewGuid().ToString("N") };
        }
        
        scenario = scenario with { UpdatedAt = DateTimeOffset.UtcNow };
        
        // Validate before saving
        var validation = await ValidateAsync(scenario, ct);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Scenario validation failed: {string.Join(", ", validation.Errors)}");
        }
        
        var filePath = Path.Combine(_scenariosDirectory, $"{scenario.Id}.json");
        var json = JsonSerializer.Serialize(scenario, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        await File.WriteAllTextAsync(filePath, json, ct);
        _scenarios[scenario.Id] = scenario;
        
        _logger.LogInformation("Saved scenario {ScenarioId} ({Name})", scenario.Id, scenario.Name);
        return scenario;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_scenariosDirectory, $"{id}.json");
        if (File.Exists(filePath))
        {
            await Task.Run(() => File.Delete(filePath), ct);
            _scenarios.TryRemove(id, out _);
            _logger.LogInformation("Deleted scenario {ScenarioId}", id);
            return true;
        }
        return false;
    }

    public async Task<ScenarioValidationResult> ValidateAsync(SimulationScenario scenario, CancellationToken ct = default)
    {
        return _parser.ValidateScenario(scenario);
    }

    public async Task<ScenarioExecutionResult> ExecuteAsync(SimulationScenario scenario, CancellationToken ct = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        
        try
        {
            // Get or create device session
            IDeviceSession? session = null;
            if (!string.IsNullOrEmpty(scenario.Config.DeviceOverrides?.GetValueOrDefault("deviceId")?.ToString()))
            {
                var deviceId = scenario.Config.DeviceOverrides["deviceId"].ToString();
                session = await _deviceFactory.CreateAsync(deviceId, ct);
            }
            
            var variables = new Dictionary<string, object>(scenario.Variables ?? new());
            
            var result = await _runner.RunAsync(scenario, variables, ct);
            
            return new ScenarioExecutionResult(
                result.IsSuccess,
                scenario.Id,
                startedAt,
                DateTimeOffset.UtcNow,
                result.Duration,
                result.ErrorMessage,
                result.StepResults.Select(s => new StepExecutionResult(
                    s.StepNumber ?? 0,
                    s.StepType,
                    s.IsSuccess,
                    s.Duration,
                    s.Output,
                    s.ErrorMessage
                )).ToList().AsReadOnly()
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scenario execution failed");
            return new ScenarioExecutionResult(
                false,
                scenario.Id,
                startedAt,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow - startedAt,
                ex.Message,
                Array.Empty<StepExecutionResult>().AsReadOnly()
            );
        }
    }

    public async Task<string> ExportAsync(string id, CancellationToken ct = default)
    {
        var scenario = await GetAsync(id, ct);
        if (scenario == null) throw new FileNotFoundException($"Scenario {id} not found");
        
        return JsonSerializer.Serialize(scenario, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    public async Task<SimulationScenario> ImportAsync(string json, CancellationToken ct = default)
    {
        var scenario = _parser.Parse(json);
        if (scenario == null) throw new InvalidOperationException("Invalid scenario JSON");
        
        scenario = scenario with { Id = Guid.NewGuid().ToString("N") };
        return await SaveAsync(scenario, ct);
    }

    private void LoadScenarios()
    {
        if (!Directory.Exists(_scenariosDirectory)) return;
        
        foreach (var file in Directory.GetFiles(_scenariosDirectory, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var scenario = _parser.Parse(json);
                if (scenario != null)
                {
                    _scenarios[scenario.Id] = scenario;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load scenario from {File}", file);
            }
        }
    }
}