// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Management;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KKBOT.Plugins.Abstractions;
using KKBOT.Device.Abstractions;
using KKBOT.Orchestration;
using KKBOT.Observability;
using KKBOT.Observability.Tracing;

namespace KKBOT.Plugin.ResourceOptimizer;

/// <summary>
/// Configuration for Resource Optimizer module.
/// </summary>
public sealed class ResourceOptimizerOptions
{
    // FPS Limiter
    public bool EnableFpsLimiter { get; set; } = true;
    public int BackgroundFpsLimit { get; set; } = 10;
    public int CombatFpsLimit { get; set; } = 30;
    public int IdleFpsLimit { get; set; } = 5;
    
    // Render Stripping
    public bool EnableRenderStripping { get; set; } = false;
    public bool Strip3DRendering { get; set; } = true;
    public bool StripParticleEffects { get; set; } = true;
    public bool StripUIRendering { get; set; } = false;
    public bool StripShadowEffects { get; set; } = true;
    
    // RAM Cleaner
    public bool EnableRamCleaner { get; set; } = true;
    public int RamCleanIntervalMinutes { get; set; } = 10;
    public long MemoryThresholdMb { get; set; } = 500;
    public bool AggressiveCleanup { get; set; } = false;
    
    // Window Layout
    public bool EnableWindowGridLayout { get; set; } = true;
    public int MinWindowWidth { get; set; } = 800;
    public int MinWindowHeight { get; set; } = 600;
    public int GridColumns { get; set; } = 4;
    public int GridRows { get; set; } = 3;
    public int WindowMargin { get; set; } = 5;
    public bool HideToSystemTray { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    
    // Monitoring
    public int MonitoringIntervalSeconds { get; set; } = 30;
    public long CpuThresholdPercent { get; set; } = 80;
    public long GpuThresholdPercent { get; set; } = 90;
}

/// <summary>
/// Game client resource state.
/// </summary>
public sealed record GameClientResourceState
{
    public string AccountId { get; init; } = string.Empty;
    public int ProcessId { get; init; }
    public string ProcessName { get; init; } = string.Empty;
    public long WorkingSetBytes { get; init; }
    public long PrivateBytes { get; init; }
    public double CpuPercent { get; init; }
    public double GpuPercent { get; init; }
    public int CurrentFps { get; init; }
    public int TargetFps { get; init; }
    public bool IsRenderStripped { get; init; }
    public bool IsMinimized { get; init; }
    public Rectangle WindowRect { get; init; }
    public DateTimeOffset LastUpdate { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Window grid position.
/// </summary>
public sealed record WindowGridPosition
{
    public int Column { get; init; }
    public int Row { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
}

/// <summary>
/// Resource Optimizer module for mass-farming optimization.
/// </summary>
public sealed class ResourceOptimizerModule : IGameModule
{
    private readonly ILogger<ResourceOptimizerModule> _logger;
    private readonly ResourceOptimizerOptions _options;
    private readonly IInstanceManager _instanceManager;
    private readonly IMetricsRegistry _metrics;
    private readonly ConcurrentDictionary<string, GameClientResourceState> _clientStates = new();
    private readonly Timer? _monitorTimer;
    private readonly Timer? _ramCleanTimer;
    private readonly Timer? _windowLayoutTimer;
    private readonly CancellationTokenSource _shutdownCts = new();
    private bool _disposed;

    public string Id => "resource-optimizer";
    public string Version => "1.0.0";

    public ResourceOptimizerModule(
        ILogger<ResourceOptimizerModule> logger,
        IOptions<ResourceOptimizerOptions> options,
        IInstanceManager instanceManager,
        IMetricsRegistry metrics)
    {
        _logger = logger;
        _options = options.Value;
        _instanceManager = instanceManager;
        _metrics = metrics;

        _monitorTimer = new Timer(MonitorResourcesAsync, null, TimeSpan.Zero, TimeSpan.FromSeconds(_options.MonitoringIntervalSeconds));
        _ramCleanTimer = new Timer(CleanRamAsync, null, TimeSpan.FromMinutes(_options.RamCleanIntervalMinutes), TimeSpan.FromMinutes(_options.RamCleanIntervalMinutes));
        _windowLayoutTimer = new Timer(ArrangeWindowsAsync, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60));
    }

    public ModuleManifest GetManifest()
    {
        return new ModuleManifest
        {
            Id = Id,
            Name = "Mass-Farm Resource Optimizer",
            Version = Version,
            Author = "KKBOT Team",
            Description = "Optimizes CPU/GPU/RAM for mass-farming: FPS limiting, render stripping, RAM cleaning, window grid layout",
            SupportedResolutions = new[] { new Resolution(1920, 1080), new Resolution(1280, 720) },
            RequiredPermissions = new[] { "DEVICE_READ", "DEVICE_WRITE", "SYSTEM_MONITOR" },
            ConfigSchema = JsonSerializer.Serialize(new ResourceOptimizerOptions()),
            DefaultConfig = JsonSerializer.Serialize(new ResourceOptimizerOptions()),
            EntryAssembly = "KKBOT.Plugin.ResourceOptimizer.dll",
            Assets = Array.Empty<string>()
        };
    }

    public IReadOnlyCollection<IGameDetector> GetDetectors() => Array.Empty<IGameDetector>();
    public IReadOnlyCollection<IGameTaskDefinition> GetTasks() => new[] { new OptimizeResourcesTask(), new CleanRamTask(), new ArrangeWindowsTask() };
    public IReadOnlyCollection<IStateDefinition> GetStates() => Array.Empty<IStateDefinition>();

    public async Task<ModuleValidationResult> ValidateAsync(ModuleContext context, CancellationToken ct)
    {
        return new ModuleValidationResult(true, Array.Empty<ModuleValidationError>());
    }

    /// <summary>
    /// Registers a game client for resource optimization.
    /// </summary>
    public void RegisterClient(string accountId, int processId, string processName)
    {
        var state = new GameClientResourceState
        {
            AccountId = accountId,
            ProcessId = processId,
            ProcessName = processName,
            TargetFps = _options.BackgroundFpsLimit
        };

        _clientStates[accountId] = state;
        _logger.LogInformation("Registered client {AccountId} (PID: {PID}) for resource optimization", accountId, processId);
    }

    /// <summary>
    /// Unregisters a game client.
    /// </summary>
    public void UnregisterClient(string accountId)
    {
        _clientStates.TryRemove(accountId, out _);
        _logger.LogInformation("Unregistered client {AccountId} from resource optimization", accountId);
    }

    /// <summary>
    /// Sets FPS limit for a client based on activity state.
    /// </summary>
    public async Task SetFpsLimitAsync(string accountId, FpsMode mode, CancellationToken ct = default)
    {
        if (!_clientStates.TryGetValue(accountId, out var state))
            return;

        var targetFps = mode switch
        {
            FpsMode.Combat => _options.CombatFpsLimit,
            FpsMode.Background => _options.BackgroundFpsLimit,
            FpsMode.Idle => _options.IdleFpsLimit,
            _ => _options.BackgroundFpsLimit
        };

        if (state.CurrentFps != targetFps)
        {
            await ApplyFpsLimitAsync(state.ProcessId, targetFps, ct);
            
            _clientStates[accountId] = state with
            {
                TargetFps = targetFps,
                CurrentFps = targetFps
            };

            _logger.LogDebug("Set FPS limit for {AccountId} to {FPS} ({Mode})", accountId, targetFps, mode);
        }
    }

    /// <summary>
    /// Enables or disables render stripping for a client.
    /// </summary>
    public async Task SetRenderStrippingAsync(string accountId, bool enable, CancellationToken ct = default)
    {
        if (!_clientStates.TryGetValue(accountId, out var state))
            return;

        if (state.IsRenderStripped != enable)
        {
            await ApplyRenderStrippingAsync(state.ProcessId, enable, ct);
            
            _clientStates[accountId] = state with { IsRenderStripped = enable };
            
            _logger.LogInformation("Render stripping {Status} for account {AccountId}", enable ? "enabled" : "disabled", accountId);
        }
    }

    /// <summary>
    /// Forces RAM cleanup for a specific client.
    /// </summary>
    public async Task<long> ForceCleanRamAsync(string accountId, CancellationToken ct = default)
    {
        if (!_clientStates.TryGetValue(accountId, out var state))
            return 0;

        var before = state.WorkingSetBytes;
        var freed = await CleanProcessMemoryAsync(state.ProcessId, ct);
        var after = before - freed;

        _clientStates[accountId] = state with { WorkingSetBytes = after };
        
        _logger.LogInformation("RAM cleanup for {AccountId}: freed {FreedMB} MB (was {BeforeMB} MB, now {AfterMB} MB)",
            accountId, freed / (1024 * 1024), before / (1024 * 1024), after / (1024 * 1024));

        return freed;
    }

    /// <summary>
    /// Arranges all game windows in a grid layout.
    /// </summary>
    public async Task ArrangeWindowsAsync(object? state = null)
    {
        try
        {
            if (!_options.EnableWindowGridLayout)
                return;

            var clients = _clientStates.Values.Where(c => c.ProcessId > 0).ToList();
            if (!clients.Any()) return;

            var screenWidth = GetScreenWidth();
            var screenHeight = GetScreenHeight();
            
            var positions = CalculateGridPositions(clients.Count, screenWidth, screenHeight);

            for (int i = 0; i < clients.Count; i++)
            {
                var client = clients[i];
                var position = positions[i];
                
                await SetWindowPositionAsync(client.ProcessId, position.X, position.Y, position.Width, position.Height);
                
                _clientStates[client.AccountId] = _clientStates[client.AccountId] with
                {
                    WindowRect = new Rectangle(position.X, position.Y, position.Width, position.Height),
                    IsMinimized = false
                };
            }

            _logger.LogDebug("Arranged {Count} game windows in {Cols}x{Rows} grid", clients.Count, _options.GridColumns, _options.GridRows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error arranging windows");
        }
    }

    /// <summary>
    /// Minimizes a client window to system tray.
    /// </summary>
    public async Task MinimizeToTrayAsync(string accountId, CancellationToken ct = default)
    {
        if (!_clientStates.TryGetValue(accountId, out var state))
            return;

        await MinimizeWindowAsync(state.ProcessId, ct);
        
        _clientStates[accountId] = state with { IsMinimized = true };
        _logger.LogDebug("Minimized window for account {AccountId}", accountId);
    }

    /// <summary>
    /// Restores a client window from tray.
    /// </summary>
    public async Task RestoreFromTrayAsync(string accountId, CancellationToken ct = default)
    {
        if (!_clientStates.TryGetValue(accountId, out var state))
            return;

        await RestoreWindowAsync(state.ProcessId, ct);
        
        _clientStates[accountId] = state with { IsMinimized = false };
        _logger.LogDebug("Restored window for account {AccountId}", accountId);
    }

    /// <summary>
    /// Periodic resource monitoring.
    /// </summary>
    private async void MonitorResourcesAsync(object? state)
    {
        try
        {
            foreach (var kvp in _clientStates)
            {
                var accountId = kvp.Key;
                var clientState = kvp.Value;

                if (clientState.ProcessId <= 0) continue;

                var updatedState = await GetProcessResourcesAsync(clientState.ProcessId);
                if (updatedState != null)
                {
                    updatedState = updatedState with { AccountId = accountId, LastUpdate = DateTimeOffset.UtcNow };
                    _clientStates[accountId] = updatedState;

                    // Record metrics
                    _metrics.RecordInstanceCpuUsage(updatedState.CpuPercent);
                    _metrics.RecordInstanceMemoryUsage(updatedState.WorkingSetBytes / (1024.0 * 1024.0));

                    // Check thresholds
                    if (updatedState.CpuPercent > _options.CpuThresholdPercent)
                    {
                        _logger.LogWarning("High CPU for account {AccountId}: {Cpu}%", accountId, updatedState.CpuPercent);
                    }

                    if (updatedState.GpuPercent > _options.GpuThresholdPercent)
                    {
                        _logger.LogWarning("High GPU for account {AccountId}: {Gpu}%", accountId, updatedState.GpuPercent);
                    }

                    // Auto FPS adjustment based on CPU
                    if (updatedState.CpuPercent > 70 && updatedState.TargetFps > _options.IdleFpsLimit)
                    {
                        await SetFpsLimitAsync(accountId, FpsMode.Background);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error monitoring resources");
        }
    }

    /// <summary>
    /// Periodic RAM cleaning.
    /// </summary>
    private async void CleanRamAsync(object? state)
    {
        try
        {
            foreach (var kvp in _clientStates)
            {
                var accountId = kvp.Key;
                var clientState = kvp.Value;

                if (clientState.ProcessId <= 0) continue;

                var workingSetMb = clientState.WorkingSetBytes / (1024 * 1024);
                if (workingSetMb > _options.MemoryThresholdMb)
                {
                    _logger.LogInformation("High memory usage for {AccountId}: {Memory} MB, cleaning...", accountId, workingSetMb);
                    await ForceCleanRamAsync(accountId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning RAM");
        }
    }

    /// <summary>
    /// Arranges windows in a grid layout.
    /// </summary>
    private async void ArrangeWindowsAsync(object? state)
    {
        await ArrangeWindowsAsync();
    }

    /// <summary>
    /// Gets process resource usage.
    /// </summary>
    private async Task<GameClientResourceState?> GetProcessResourcesAsync(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            
            var cpuPercent = GetProcessCpuPercent(process);
            var gpuPercent = await GetGpuUsagePercentAsync(processId);
            
            return new GameClientResourceState
            {
                ProcessId = processId,
                ProcessName = process.ProcessName,
                WorkingSetBytes = process.WorkingSet64,
                PrivateBytes = process.PrivateMemorySize64,
                CpuPercent = cpuPercent,
                GpuPercent = gpuPercent,
                CurrentFps = 0, // Would need game-specific detection
                TargetFps = 10
            };
        }
        catch (ArgumentException)
        {
            // Process not found
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting process resources for PID {PID}", processId);
            return null;
        }
    }

    /// <summary>
    /// Gets CPU usage percentage for a process.
    /// </summary>
    private double GetProcessCpuPercent(Process process)
    {
        try
        {
            var cpuCounter = new PerformanceCounter("Process", "% Processor Time", process.ProcessName, true);
            cpuCounter.NextValue();
            System.Threading.Thread.Sleep(100);
            return cpuCounter.NextValue() / Environment.ProcessorCount;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Gets GPU usage for a process.
    /// </summary>
    private async Task<double> GetGpuUsagePercentAsync(int processId)
    {
        // In real implementation, use NVAPI or DXGI to get GPU usage per process
        // For now, return 0
        await Task.CompletedTask;
        return 0;
    }

    /// <summary>
    /// Applies FPS limit to a process.
    /// </summary>
    private async Task ApplyFpsLimitAsync(int processId, int fps, CancellationToken ct)
    {
        // In real implementation, would inject into game or use external tool
        // For example: RivaTuner Statistics Server, NVIDIA Inspector, or game-specific config
        await Task.CompletedTask;
        
        _logger.LogDebug("Applied FPS limit {FPS} to process {PID}", fps, processId);
    }

    /// <summary>
    /// Applies render stripping to a process.
    /// </summary>
    private async Task ApplyRenderStrippingAsync(int processId, bool enable, CancellationToken ct)
    {
        // In real implementation, would inject into game to disable rendering features
        // For example: disable 3D rendering, particle effects, shadows, etc.
        await Task.CompletedTask;
        
        _logger.LogDebug("Render stripping {Status} for process {PID}", enable ? "enabled" : "disabled", processId);
    }

    /// <summary>
    /// Cleans process memory.
    /// </summary>
    private async Task<long> CleanProcessMemoryAsync(int processId, CancellationToken ct)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            var before = process.WorkingSet64;

            // Call Windows API to trim working set
            // In real implementation, use SetProcessWorkingSetSizeEx or EmptyWorkingSet
            
            // For demonstration, we'll just trigger GC
            GC.Collect();
            GC.WaitForPendingFinalizers();
            
            // Refresh process info
            process.Refresh();
            var after = process.WorkingSet64;
            
            return Math.Max(0, before - after);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error cleaning memory for process {PID}", processId);
            return 0;
        }
    }

    /// <summary>
    /// Sets window position and size.
    /// </summary>
    private async Task SetWindowPositionAsync(int processId, int x, int y, int width, int height)
    {
        // In real implementation, use Windows API (SetWindowPos)
        await Task.CompletedTask;
    }

    /// <summary>
    /// Minimizes a window.
    /// </summary>
    private async Task MinimizeWindowAsync(int processId, CancellationToken ct)
    {
        // In real implementation, use ShowWindow SW_MINIMIZE
        await Task.CompletedTask;
    }

    /// <summary>
    /// Restores a minimized window.
    /// </summary>
    private async Task RestoreWindowAsync(int processId, CancellationToken ct)
    {
        // In real implementation, use ShowWindow SW_RESTORE
        await Task.CompletedTask;
    }

    /// <summary>
    /// Calculates grid positions for window layout.
    /// </summary>
    private List<WindowGridPosition> CalculateGridPositions(int count, int screenWidth, int screenHeight)
    {
        var positions = new List<WindowGridPosition>();
        var cols = Math.Min(_options.GridColumns, count);
        var rows = (int)Math.Ceiling((double)count / cols);
        
        var availableWidth = screenWidth - (_options.WindowMargin * (cols + 1));
        var availableHeight = screenHeight - (_options.WindowMargin * (rows + 1));
        
        var windowWidth = Math.Max(_options.MinWindowWidth, availableWidth / cols);
        var windowHeight = Math.Max(_options.MinWindowHeight, availableHeight / rows);

        for (int i = 0; i < count; i++)
        {
            var col = i % cols;
            var row = i / cols;
            
            var x = _options.WindowMargin + col * (windowWidth + _options.WindowMargin);
            var y = _options.WindowMargin + row * (windowHeight + _options.WindowMargin);
            
            positions.Add(new WindowGridPosition
            {
                Column = col,
                Row = row,
                X = x,
                Y = y,
                Width = windowWidth,
                Height = windowHeight
            });
        }

        return positions;
    }

    /// <summary>
    /// Gets screen width.
    /// </summary>
    private int GetScreenWidth()
    {
        // In real implementation, use SystemParameters or screen enumeration
        return 1920;
    }

    /// <summary>
    /// Gets screen height.
    /// </summary>
    private int GetScreenHeight()
    {
        return 1080;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _monitorTimer?.Dispose();
        _ramCleanTimer?.Dispose();
        _windowLayoutTimer?.Dispose();
        _shutdownCts.Cancel();
        _shutdownCts.Dispose();
    }
}

/// <summary>
/// FPS modes for different activity states.
/// </summary>
public enum FpsMode
{
    Combat,
    Background,
    Idle
}

/// <summary>
/// Optimize resources task.
/// </summary>
public sealed class OptimizeResourcesTask : IGameTaskDefinition
{
    public string Id => "optimize-resources";
    public string Name => "Optimize Resources";
    public string? Description => "Optimizes CPU/GPU/RAM for current game client";
    public TaskType Type => TaskType.Repeating;
    public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(5);
    public RetryPolicy DefaultRetryPolicy => new(1, 30000);
    public bool RequiresApproval => false;

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        return new IdleState();
    }
}

/// <summary>
/// Clean RAM task.
/// </summary>
public sealed class CleanRamTask : IGameTaskDefinition
{
    public string Id => "clean-ram";
    public string Name => "Clean RAM";
    public string? Description => "Forces memory cleanup for game client";
    public TaskType Type => TaskType.Repeating;
    public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(2);
    public RetryPolicy DefaultRetryPolicy => new(1, 60000);
    public bool RequiresApproval => false;

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        return new IdleState();
    }
}

/// <summary>
/// Arrange windows task.
/// </summary>
public sealed class ArrangeWindowsTask : IGameTaskDefinition
{
    public string Id => "arrange-windows";
    public string Name => "Arrange Windows";
    public string? Description => "Arranges game windows in grid layout";
    public TaskType Type => TaskType.OneShot;
    public TimeSpan DefaultTimeout => TimeSpan.FromSeconds(30);
    public RetryPolicy DefaultRetryPolicy => new(1, 5000);
    public bool RequiresApproval => false;

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        return new IdleState();
    }
}

/// <summary>
/// Idle state for resource optimizer tasks.
/// </summary>
public sealed class IdleState : IStateDefinition
{
    public string Id => "idle";
    public string Name => "Idle";
    public string Category => "resource-optimizer";
    public Func<PerceptionContext?, bool> EntryCondition => _ => true;
    public IStateAction? Action => null;
    public IReadOnlyList<string> NextStateIds => Array.Empty<string>();
    public TimeSpan Timeout => TimeSpan.FromSeconds(1);
    public RetryPolicy RetryPolicy => new(0, 0);
    public EvidencePolicy EvidencePolicy => new(false, false);
}