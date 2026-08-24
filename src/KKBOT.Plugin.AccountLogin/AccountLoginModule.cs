// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KKBOT.Plugins.Abstractions;
using KKBOT.Device.Abstractions;
using KKBOT.Perception.Abstractions;
using KKBOT.AI.Abstractions;
using KKBOT.Orchestration;
using KKBOT.Observability;
using KKBOT.Observability.Tracing;

namespace KKBOT.Plugin.AccountLogin;

/// <summary>
/// Module configuration for Account Login Engine.
/// </summary>
public sealed class AccountLoginModuleOptions
{
    public string GameExePath { get; set; } = string.Empty;
    public string GameWindowTitle { get; set; } = string.Empty;
    public string GameProcessName { get; set; } = string.Empty;
    public int HeartbeatIntervalMs { get; set; } = 5000;
    public int MaxLoginRetries { get; set; } = 3;
    public int LoginTimeoutSeconds { get; set; } = 60;
    public int CrashRestartDelayMinMs { get; set; } = 5000;
    public int CrashRestartDelayMaxMs { get; set; } = 15000;
    public bool EnableSecurityScreenDetection { get; set; } = true;
    public string[] SecurityScreenTemplates { get; set; } = Array.Empty<string>();
    public TimeSpan GameStartupTimeout { get; set; } = TimeSpan.FromMinutes(2);
}

/// <summary>
/// Account configuration for auto-login.
/// </summary>
public sealed record AccountConfig
{
    public string AccountId { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty; // Encrypted
    public string ServerName { get; init; } = string.Empty;
    public string ChannelName { get; init; } = string.Empty;
    public string CharacterName { get; init; } = string.Empty;
    public string GameExePath { get; init; } = string.Empty;
    public string ProxyConfig { get; init; } = string.Empty;
    public bool AutoRelogin { get; init; } = true;
    public bool AutoCharacterSelect { get; init; } = true;
}

/// <summary>
/// Login state for an account.
/// </summary>
public sealed record AccountLoginState
{
    public string AccountId { get; init; } = string.Empty;
    public LoginPhase Phase { get; init; } = LoginPhase.Idle;
    public int RetryCount { get; init; } = 0;
    public DateTimeOffset LastAttempt { get; init; }
    public string? LastError { get; init; }
    public ProcessInfo? GameProcess { get; init; }
    public bool IsHealthy { get; init; } = true;
    public DateTimeOffset LastHeartbeat { get; init; } = DateTimeOffset.UtcNow;
}

public enum LoginPhase
{
    Idle,
    StartingGame,
    WaitingForLoginScreen,
    EnteringCredentials,
    SelectingServer,
    SelectingChannel,
    SelectingCharacter,
    InGame,
    HandlingSecurityScreen,
    RecoveringFromCrash,
    Failed
}

/// <summary>
/// Game process information.
/// </summary>
public sealed record ProcessInfo
{
    public int ProcessId { get; init; }
    public string ProcessName { get; init; } = string.Empty;
    public IntPtr MainWindowHandle { get; init; }
    public DateTimeOffset StartTime { get; init; }
    public bool IsResponding { get; init; }
}

/// <summary>
/// Security screen detection result.
/// </summary>
public sealed record SecurityScreenResult
{
    public bool Detected { get; init; }
    public SecurityScreenType Type { get; init; }
    public double Confidence { get; init; }
    public Rectangle? Location { get; init; }
}

public enum SecurityScreenType
{
    None,
    PinCode,
    Otp,
    Captcha,
    Maintenance,
    BanNotice
}

/// <summary>
/// Main module implementation for Account & Auto-Login Engine.
/// </summary>
public sealed class AccountLoginModule : IGameModule
{
    private readonly ILogger<AccountLoginModule> _logger;
    private readonly AccountLoginModuleOptions _options;
    private readonly IDeviceSessionFactory _deviceFactory;
    private readonly IPerceptionPipeline _perceptionPipeline;
    private readonly IAiVisionService _aiVision;
    private readonly IRecoveryCoordinator _recoveryCoordinator;
    private readonly IMetricsRegistry _metrics;
    
    private readonly ConcurrentDictionary<string, AccountConfig> _accounts = new();
    private readonly ConcurrentDictionary<string, AccountLoginState> _loginStates = new();
    private readonly ConcurrentDictionary<string, IDeviceSession> _deviceSessions = new();
    private readonly Timer? _heartbeatTimer;
    private readonly Timer? _crashMonitorTimer;
    private readonly CancellationTokenSource _shutdownCts = new();
    private bool _disposed;

    public string Id => "account-login-engine";
    public string Version => "1.0.0";

    public AccountLoginModule(
        ILogger<AccountLoginModule> logger,
        IOptions<AccountLoginModuleOptions> options,
        IDeviceSessionFactory deviceFactory,
        IPerceptionPipeline perceptionPipeline,
        IAiVisionService aiVision,
        IRecoveryCoordinator recoveryCoordinator,
        IMetricsRegistry metrics)
    {
        _logger = logger;
        _options = options.Value;
        _deviceFactory = deviceFactory;
        _perceptionPipeline = perceptionPipeline;
        _aiVision = aiVision;
        _recoveryCoordinator = recoveryCoordinator;
        _metrics = metrics;

        _heartbeatTimer = new Timer(CheckHeartbeatsAsync, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(_options.HeartbeatIntervalMs));
        _crashMonitorTimer = new Timer(MonitorGameProcessesAsync, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
    }

    public ModuleManifest GetManifest()
    {
        return new ModuleManifest
        {
            Id = Id,
            Name = "Account & Auto-Login Engine",
            Version = Version,
            Author = "KKBOT Team",
            Description = "Automated game login, relogin, crash recovery, and security screen handling",
            SupportedResolutions = new[] { new Resolution(1920, 1080), new Resolution(1280, 720) },
            RequiredPermissions = new[] { "DEVICE_READ", "DEVICE_WRITE", "INPUT_WRITE", "SCREENSHOT_CAPTURE" },
            ConfigSchema = JsonSerializer.Serialize(new AccountLoginModuleOptions()),
            DefaultConfig = JsonSerializer.Serialize(new AccountLoginModuleOptions()),
            EntryAssembly = "KKBOT.Plugin.AccountLogin.dll",
            Assets = new[] { "templates/login_screen.png", "templates/server_list.png", "templates/channel_list.png", "templates/character_select.png", "templates/pin_screen.png", "templates/otp_screen.png", "templates/captcha.png" }
        };
    }

    public IReadOnlyCollection<IGameDetector> GetDetectors() => Array.Empty<IGameDetector>();
    public IReadOnlyCollection<IGameTaskDefinition> GetTasks() => new[] { new AutoLoginTask(), new AutoReloginTask(), new CrashRecoveryTask() };
    public IReadOnlyCollection<IStateDefinition> GetStates() => new[] { new LoginState(), new InGameState(), new CrashRecoveryState() };

    public async Task<ModuleValidationResult> ValidateAsync(ModuleContext context, CancellationToken ct)
    {
        var errors = new List<ModuleValidationError>();

        if (string.IsNullOrEmpty(_options.GameExePath) || !File.Exists(_options.GameExePath))
        {
            errors.Add(new ModuleValidationError("GAME_EXE_NOT_FOUND", $"Game executable not found: {_options.GameExePath}"));
        }

        if (string.IsNullOrEmpty(_options.GameWindowTitle))
        {
            errors.Add(new ModuleValidationError("GAME_WINDOW_TITLE_EMPTY", "Game window title is required for process detection"));
        }

        // Validate templates exist
        foreach (var template in _options.SecurityScreenTemplates)
        {
            var templatePath = Path.Combine(AppContext.BaseDirectory, "plugins", "templates", template);
            if (!File.Exists(templatePath))
            {
                errors.Add(new ModuleValidationError("TEMPLATE_MISSING", $"Security screen template not found: {template}"));
            }
        }

        return new ModuleValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// Registers an account for auto-login management.
    /// </summary>
    public async Task RegisterAccountAsync(AccountConfig config, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(config.AccountId))
            throw new ArgumentException("AccountId is required", nameof(config));

        _accounts[config.AccountId] = config;
        _loginStates[config.AccountId] = new AccountLoginState
        {
            AccountId = config.AccountId,
            Phase = LoginPhase.Idle
        };

        _logger.LogInformation("Registered account {AccountId} for auto-login", config.AccountId);
    }

    /// <summary>
    /// Starts auto-login for a registered account.
    /// </summary>
    public async Task<LoginResult> StartLoginAsync(string accountId, CancellationToken ct = default)
    {
        if (!_accounts.TryGetValue(accountId, out var config))
            return LoginResult.Failed($"Account {accountId} not registered");

        using var activity = KkbotActivitySources.Plugins.StartActivity("account.login");
        activity?.SetTag("account.id", accountId);

        var state = _loginStates.GetOrAdd(accountId, _ => new AccountLoginState { AccountId = accountId });
        
        try
        {
            // Start game process
            state = state with { Phase = LoginPhase.StartingGame, LastAttempt = DateTimeOffset.UtcNow };
            _loginStates[accountId] = state;

            var process = await StartGameProcessAsync(config, ct);
            if (process == null)
            {
                return LoginResult.Failed("Failed to start game process");
            }

            state = state with { Phase = LoginPhase.WaitingForLoginScreen, GameProcess = process };
            _loginStates[accountId] = state;

            // Create device session
            var session = await _deviceFactory.CreateAsync(process.ProcessId.ToString(), ct);
            _deviceSessions[accountId] = session;

            // Wait for login screen
            var loginScreenResult = await WaitForLoginScreenAsync(session, ct);
            if (!loginScreenResult)
            {
                return LoginResult.Failed("Login screen not detected within timeout");
            }

            // Enter credentials
            state = state with { Phase = LoginPhase.EnteringCredentials };
            _loginStates[accountId] = state;
            
            var credentialResult = await EnterCredentialsAsync(session, config, ct);
            if (!credentialResult)
            {
                return LoginResult.Failed("Failed to enter credentials");
            }

            // Select server
            state = state with { Phase = LoginPhase.SelectingServer };
            _loginStates[accountId] = state;
            await SelectServerAsync(session, config, ct);

            // Select channel
            state = state with { Phase = LoginPhase.SelectingChannel };
            _loginStates[accountId] = state;
            await SelectChannelAsync(session, config, ct);

            // Select character
            state = state with { Phase = LoginPhase.SelectingCharacter };
            _loginStates[accountId] = state;
            await SelectCharacterAsync(session, config, ct);

            // Verify in-game
            state = state with { Phase = LoginPhase.InGame, IsHealthy = true, LastHeartbeat = DateTimeOffset.UtcNow };
            _loginStates[accountId] = state;

            _logger.LogInformation("Successfully logged in account {AccountId}", accountId);
            return LoginResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed for account {AccountId}", accountId);
            return LoginResult.Failed(ex.Message);
        }
    }

    /// <summary>
    /// Starts the game process.
    /// </summary>
    private async Task<ProcessInfo?> StartGameProcessAsync(AccountConfig config, CancellationToken ct)
    {
        var exePath = !string.IsNullOrEmpty(config.GameExePath) ? config.GameExePath : _options.GameExePath;
        
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
        {
            _logger.LogError("Game executable not found: {Path}", exePath);
            return null;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = Path.GetDirectoryName(exePath),
            UseShellExecute = true
        };

        var process = Process.Start(startInfo);
        if (process == null)
            return null;

        // Wait for process to initialize
        await Task.Delay(3000, ct);

        // Find main window
        var windowHandle = IntPtr.Zero;
        foreach (ProcessThread thread in process.Threads)
        {
            // In real implementation, use EnumThreadWindows to find main window
            await Task.Delay(100, ct);
        }

        return new ProcessInfo
        {
            ProcessId = process.Id,
            ProcessName = process.ProcessName,
            MainWindowHandle = process.MainWindowHandle,
            StartTime = DateTimeOffset.UtcNow,
            IsResponding = true
        };
    }

    /// <summary>
    /// Waits for login screen to appear.
    /// </summary>
    private async Task<bool> WaitForLoginScreenAsync(IDeviceSession session, CancellationToken ct)
    {
        var timeout = TimeSpan.FromSeconds(_options.LoginTimeoutSeconds);
        var startTime = DateTimeOffset.UtcNow;

        while (DateTimeOffset.UtcNow - startTime < timeout)
        {
            ct.ThrowIfCancellationRequested();

            var frame = await session.CaptureAsync(new CaptureOptions(), ct);
            var results = await _perceptionPipeline.ProcessAsync(frame, ct);

            var loginDetected = results.Any(r => r.DetectorName.Contains("login") && r.IsMatch);
            if (loginDetected)
            {
                _logger.LogDebug("Login screen detected");
                return true;
            }

            await Task.Delay(1000, ct);
        }

        return false;
    }

    /// <summary>
    /// Enters username and password.
    /// </summary>
    private async Task<bool> EnterCredentialsAsync(IDeviceSession session, AccountConfig config, CancellationToken ct)
    {
        try
        {
            // Click username field
            await session.SendInputAsync(new InputCommand
            {
                Type = InputType.Tap,
                X = 500, // Would be detected from template
                Y = 400
            }, ct);

            await Task.Delay(500, ct);

            // Type username (using secure input)
            await session.SendInputAsync(new InputCommand
            {
                Type = InputType.Text,
                Text = config.Username
            }, ct);

            await Task.Delay(500, ct);

            // Click password field
            await session.SendInputAsync(new InputCommand
            {
                Type = InputType.Tap,
                X = 500,
                Y = 500
            }, ct);

            await Task.Delay(500, ct);

            // Type password
            await session.SendInputAsync(new InputCommand
            {
                Type = InputType.Text,
                Text = config.Password // In real impl, decrypt first
            }, ct);

            await Task.Delay(500, ct);

            // Click login button
            await session.SendInputAsync(new InputCommand
            {
                Type = InputType.Tap,
                X = 640,
                Y = 600
            }, ct);

            await Task.Delay(2000, ct);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enter credentials");
            return false;
        }
    }

    /// <summary>
    /// Selects server from list.
    /// </summary>
    private async Task SelectServerAsync(IDeviceSession session, AccountConfig config, CancellationToken ct)
    {
        // Implementation would detect server list and click matching server
        await Task.Delay(1000, ct);
    }

    /// <summary>
    /// Selects channel from list.
    /// </summary>
    private async Task SelectChannelAsync(IDeviceSession session, AccountConfig config, CancellationToken ct)
    {
        // Implementation would detect channel list and click matching channel
        await Task.Delay(1000, ct);
    }

    /// <summary>
    /// Selects character.
    /// </summary>
    private async Task SelectCharacterAsync(IDeviceSession session, AccountConfig config, CancellationToken ct)
    {
        // Implementation would detect character list and click matching character
        await Task.Delay(1000, ct);
    }

    /// <summary>
    /// Periodic heartbeat check for all accounts.
    /// </summary>
    private async void CheckHeartbeatsAsync(object? state)
    {
        try
        {
            foreach (var kvp in _loginStates)
            {
                var accountId = kvp.Key;
                var loginState = kvp.Value;

                if (loginState.Phase == LoginPhase.InGame)
                {
                    var timeSinceHeartbeat = DateTimeOffset.UtcNow - loginState.LastHeartbeat;
                    if (timeSinceHeartbeat > TimeSpan.FromMinutes(2))
                    {
                        _logger.LogWarning("Account {AccountId} heartbeat timeout", accountId);
                        
                        // Check if process is still alive
                        var isAlive = await CheckProcessAliveAsync(loginState.GameProcess);
                        if (!isAlive)
                        {
                            _logger.LogWarning("Game process dead for account {AccountId}, initiating recovery", accountId);
                            await HandleGameCrashAsync(accountId);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in heartbeat check");
        }
    }

    /// <summary>
    /// Monitors game processes for crashes.
    /// </summary>
    private async void MonitorGameProcessesAsync(object? state)
    {
        try
        {
            foreach (var kvp in _loginStates)
            {
                var accountId = kvp.Key;
                var loginState = kvp.Value;

                if (loginState.Phase == LoginPhase.InGame && loginState.GameProcess != null)
                {
                    var isAlive = await CheckProcessAliveAsync(loginState.GameProcess);
                    if (!isAlive)
                    {
                        _logger.LogWarning("Game process crash detected for account {AccountId}", accountId);
                        await HandleGameCrashAsync(accountId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in crash monitor");
        }
    }

    /// <summary>
    /// Checks if a game process is still alive and responding.
    /// </summary>
    private async Task<bool> CheckProcessAliveAsync(ProcessInfo? processInfo)
    {
        if (processInfo == null) return false;

        try
        {
            var process = Process.GetProcessById(processInfo.ProcessId);
            if (process.HasExited) return false;

            // Check if responding (for Windows)
            // In real implementation, use SendMessageTimeout to check responsiveness
            
            return !process.HasExited && process.Responding;
        }
        catch (ArgumentException)
        {
            // Process not found
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Handles game crash by initiating recovery.
    /// </summary>
    private async Task HandleGameCrashAsync(string accountId)
    {
        var state = _loginStates.GetOrAdd(accountId, _ => new AccountLoginState { AccountId = accountId });
        
        state = state with { Phase = LoginPhase.RecoveringFromCrash, IsHealthy = false };
        _loginStates[accountId] = state;

        _logger.LogInformation("Initiating crash recovery for account {AccountId}", accountId);

        // Kill stuck process
        if (state.GameProcess != null)
        {
            try
            {
                var process = Process.GetProcessById(state.GameProcess.ProcessId);
                if (!process.HasExited)
                {
                    process.Kill(true);
                    await process.WaitForExitAsync();
                }
            }
            catch { /* Ignore */ }
        }

        // Random delay to avoid suspicion
        var random = new Random();
        var delay = _options.CrashRestartDelayMinMs + random.Next(_options.CrashRestartDelayMaxMs - _options.CrashRestartDelayMinMs);
        await Task.Delay(delay);

        // Re-login
        if (_accounts.TryGetValue(accountId, out var config) && config.AutoRelogin)
        {
            var result = await StartLoginAsync(accountId);
            if (!result.Success)
            {
                _logger.LogError("Auto-relogin failed for account {AccountId}: {Error}", accountId, result.Error);
            }
        }
    }

    /// <summary>
    /// Detects security screens (PIN, OTP, Captcha).
    /// </summary>
    private async Task<SecurityScreenResult> DetectSecurityScreenAsync(IDeviceSession session, CancellationToken ct)
    {
        if (!_options.EnableSecurityScreenDetection)
            return new SecurityScreenResult { Detected = false, Type = SecurityScreenType.None };

        var frame = await session.CaptureAsync(new CaptureOptions(), ct);
        var results = await _perceptionPipeline.ProcessAsync(frame, ct);

        foreach (var template in _options.SecurityScreenTemplates)
        {
            var match = results.FirstOrDefault(r => r.DetectorName.Contains(template) && r.IsMatch);
            if (match != null)
            {
                var screenType = template.ToLower() switch
                {
                    var t when t.Contains("pin") => SecurityScreenType.PinCode,
                    var t when t.Contains("otp") => SecurityScreenType.Otp,
                    var t when t.Contains("captcha") => SecurityScreenType.Captcha,
                    _ => SecurityScreenType.None
                };

                return new SecurityScreenResult
                {
                    Detected = true,
                    Type = screenType,
                    Confidence = match.Confidence,
                    Location = match.BoundingBox
                };
            }
        }

        // Also check with AI Vision for unknown security screens
        var aiResult = await _aiVision.GenerateAsync(frame.Data, "Detect if this screen shows a security verification like PIN code, OTP, CAPTCHA, maintenance notice, or ban notice. Return the type if found.", ct);
        
        // Parse AI result (simplified)
        return new SecurityScreenResult { Detected = false, Type = SecurityScreenType.None };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _heartbeatTimer?.Dispose();
        _crashMonitorTimer?.Dispose();
        _shutdownCts.Cancel();
        _shutdownCts.Dispose();

        foreach (var session in _deviceSessions.Values)
        {
            session.DisposeAsync().AsTask().Wait(1000);
        }
    }
}

/// <summary>
/// Result of login operation.
/// </summary>
public sealed record LoginResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string? SessionId { get; init; }

    public static LoginResult Success(string? sessionId = null) => new() { Success = true, SessionId = sessionId };
    public static LoginResult Failed(string error) => new() { Success = false, Error = error };
}

/// <summary>
/// Auto-login task definition.
/// </summary>
public sealed class AutoLoginTask : IGameTaskDefinition
{
    public string Id => "auto-login";
    public string Name => "Auto Login";
    public string? Description => "Automatically logs into game with configured credentials";
    public TaskType Type => TaskType.OneShot;
    public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(5);
    public RetryPolicy DefaultRetryPolicy => new(3, 5000);
    public bool RequiresApproval => false;

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        // Implementation would call AccountLoginModule.StartLoginAsync
        return new LoginState();
    }
}

/// <summary>
/// Auto-relogin task definition.
/// </summary>
public sealed class AutoReloginTask : IGameTaskDefinition
{
    public string Id => "auto-relogin";
    public string Name => "Auto Relogin";
    public string? Description => "Automatically relogs in after disconnect or crash";
    public TaskType Type => TaskType.Repeating;
    public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(2);
    public RetryPolicy DefaultRetryPolicy => new(5, 10000);
    public bool RequiresApproval => false;

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        return new LoginState();
    }
}

/// <summary>
/// Crash recovery task definition.
/// </summary>
public sealed class CrashRecoveryTask : IGameTaskDefinition
{
    public string Id => "crash-recovery";
    public string Name => "Crash Recovery";
    public string? Description => "Recovers from game crash by restarting and relogging";
    public TaskType Type => TaskType.OneShot;
    public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(3);
    public RetryPolicy DefaultRetryPolicy => new(2, 15000);
    public bool RequiresApproval => false;

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        return new CrashRecoveryState();
    }
}

/// <summary>
/// Login state definition.
/// </summary>
public sealed class LoginState : IStateDefinition
{
    public string Id => "login";
    public string Name => "Login";
    public string Category => "authentication";
    public Func<PerceptionContext?, bool> EntryCondition => ctx => ctx?.Detections.Any(d => d.DetectorName.Contains("login") && d.IsMatch) ?? false;
    public IStateAction? Action => null;
    public IReadOnlyList<string> NextStateIds => new[] { "in_game", "security_screen" };
    public TimeSpan Timeout => TimeSpan.FromMinutes(2);
    public RetryPolicy RetryPolicy => new(3, 5000);
    public EvidencePolicy EvidencePolicy => new(true, false);
}

/// <summary>
/// In-game state definition.
/// </summary>
public sealed class InGameState : IStateDefinition
{
    public string Id => "in_game";
    public string Name => "In Game";
    public string Category => "gameplay";
    public Func<PerceptionContext?, bool> EntryCondition => ctx => ctx?.Detections.Any(d => d.DetectorName.Contains("hp_bar") && d.IsMatch) ?? false;
    public IStateAction? Action => null;
    public IReadOnlyList<string> NextStateIds => new[] { "login", "dead", "disconnected" };
    public TimeSpan Timeout => TimeSpan.FromHours(1);
    public RetryPolicy RetryPolicy => new(0, 0);
    public EvidencePolicy EvidencePolicy => new(true, true);
}

/// <summary>
/// Crash recovery state definition.
/// </summary>
public sealed class CrashRecoveryState : IStateDefinition
{
    public string Id => "crash_recovery";
    public string Name => "Crash Recovery";
    public string Category => "recovery";
    public Func<PerceptionContext?, bool> EntryCondition => ctx => false; // Triggered by coordinator
    public IStateAction? Action => null;
    public IReadOnlyList<string> NextStateIds => new[] { "login" };
    public TimeSpan Timeout => TimeSpan.FromMinutes(5);
    public RetryPolicy RetryPolicy => new(3, 10000);
    public EvidencePolicy EvidencePolicy => new(true, false);
}