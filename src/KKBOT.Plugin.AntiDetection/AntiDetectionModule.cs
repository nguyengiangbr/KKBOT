// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KKBOT.Plugins.Abstractions;
using KKBOT.Device.Abstractions;
using KKBOT.Observability;
using KKBOT.Observability.Tracing;

namespace KKBOT.Plugin.AntiDetection;

/// <summary>
/// Configuration for Anti-Detection module.
/// </summary>
public sealed class AntiDetectionOptions
{
    // Humanizer settings
    public int MinClickDelayMs { get; set; } = 100;
    public int MaxClickDelayMs { get; set; } = 350;
    public int CoordinateJitterMinPx { get; set; } = 2;
    public int CoordinateJitterMaxPx { get; set; } = 5;
    public bool EnableBezierMouseMovement { get; set; } = true;
    public int BezierControlPoints { get; set; } = 3;
    public double BezierVariance { get; set; } = 0.3;
    
    // Behavior patterns
    public int MinActionIntervalMs { get; set; } = 500;
    public int MaxActionIntervalMs { get; set; } = 2000;
    public double PauseProbability { get; set; } = 0.15;
    public int MinPauseDurationMs { get; set; } = 1000;
    public int MaxPauseDurationMs { get; set; } = 5000;
    
    // Telegram Bot settings
    public bool EnableTelegramBot { get; set; } = false;
    public string TelegramBotToken { get; set; } = string.Empty;
    public string[] AllowedChatIds { get; set; } = Array.Empty<string>();
    public string TelegramApiUrl { get; set; } = "https://api.telegram.org/bot";
    
    // Alert settings
    public bool AlertOnDeath { get; set; } = true;
    public bool AlertOnVipDrop { get; set; } = true;
    public bool AlertOnDisconnect { get; set; } = true;
    public bool AlertOnLowHp { get; set; } = false;
    public int LowHpThresholdPercent { get; set; } = 20;
    
    // Remote control
    public bool EnableRemoteControl { get; set; } = false;
    public string CommandPrefix { get; set; } = "/";
}

/// <summary>
/// Humanized input command with randomized parameters.
/// </summary>
public sealed record HumanizedInputCommand
{
    public InputType Type { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int EndX { get; init; }
    public int EndY { get; init; }
    public string? Text { get; init; }
    public TimeSpan? Duration { get; init; }
    public int KeyCode { get; init; }
    public int DelayBeforeMs { get; init; }
    public Point[]? BezierPath { get; init; }
}

/// <summary>
/// Point for Bezier curve.
/// </summary>
public sealed record Point(int X, int Y);

/// <summary>
/// Telegram message for alerts.
/// </summary>
public sealed record TelegramAlert
{
    public string Message { get; init; } = string.Empty;
    public AlertType Type { get; init; }
    public string AccountId { get; init; } = string.Empty;
    public string CharacterName { get; init; } = string.Empty;
    public Dictionary<string, object> Metadata { get; init; } = new();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

public enum AlertType
{
    Info,
    Warning,
    Critical,
    VipDrop,
    Death,
    Disconnect,
    LowHp,
    VipItemDrop,
    LevelUp,
    QuestComplete
}

/// <summary>
/// Telegram command for remote control.
/// </summary>
public sealed record TelegramCommand
{
    public string Command { get; init; } = string.Empty;
    public string[] Arguments { get; init; } = Array.Empty<string>();
    public string ChatId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Anti-Detection module for humanizing behavior and Telegram alerts.
/// </summary>
public sealed class AntiDetectionModule : IGameModule
{
    private readonly ILogger<AntiDetectionModule> _logger;
    private readonly AntiDetectionOptions _options;
    private readonly HttpClient _httpClient;
    private readonly Random _random = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastActionTimes = new();
    private readonly ConcurrentDictionary<string, Point> _lastMousePositions = new();
    private Timer? _telegramPollTimer;
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly IMetricsRegistry _metrics;
    private bool _disposed;

    public string Id => "anti-detection";
    public string Version => "1.0.0";

    public AntiDetectionModule(
        ILogger<AntiDetectionModule> logger,
        IOptions<AntiDetectionOptions> options,
        HttpClient httpClient,
        IMetricsRegistry metrics)
    {
        _logger = logger;
        _options = options.Value;
        _httpClient = httpClient;
        _metrics = metrics;

        if (_options.EnableTelegramBot && !string.IsNullOrEmpty(_options.TelegramBotToken))
        {
            _telegramPollTimer = new Timer(PollTelegramUpdatesAsync, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
        }
    }

    public ModuleManifest GetManifest()
    {
        return new ModuleManifest
        {
            Id = Id,
            Name = "Anti-Detection & Humanizer Engine",
            Version = Version,
            Author = "KKBOT Team",
            Description = "Humanizes input behavior, prevents detection, and provides Telegram alerts/remote control",
            SupportedResolutions = Array.Empty<Resolution>(),
            RequiredPermissions = new[] { "INPUT_WRITE", "NETWORK_ACCESS" },
            ConfigSchema = JsonSerializer.Serialize(new AntiDetectionOptions()),
            DefaultConfig = JsonSerializer.Serialize(new AntiDetectionOptions()),
            EntryAssembly = "KKBOT.Plugin.AntiDetection.dll",
            Assets = Array.Empty<string>()
        };
    }

    public IReadOnlyCollection<IGameDetector> GetDetectors() => Array.Empty<IGameDetector>();
    public IReadOnlyCollection<IGameTaskDefinition> GetTasks() => new[] { new HumanizeInputTask(), new SendAlertTask() };
    public IReadOnlyCollection<IStateDefinition> GetStates() => Array.Empty<IStateDefinition>();

    public async Task<ModuleValidationResult> ValidateAsync(ModuleContext context, CancellationToken ct)
    {
        var errors = new List<ModuleValidationError>();

        if (_options.EnableTelegramBot && string.IsNullOrEmpty(_options.TelegramBotToken))
        {
            errors.Add(new ModuleValidationError("TELEGRAM_TOKEN_MISSING", "Telegram bot token is required when Telegram bot is enabled"));
        }

        return new ModuleValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// Humanizes an input command by adding random delays, coordinate jitter, and Bezier curves.
    /// </summary>
    public HumanizedInputCommand HumanizeInput(InputCommand command, string accountId = "")
    {
        var humanized = new HumanizedInputCommand
        {
            Type = command.Type,
            X = command.X,
            Y = command.Y,
            EndX = command.EndX,
            EndY = command.EndY,
            Text = command.Text,
            Duration = command.Duration,
            KeyCode = command.KeyCode,
            DelayBeforeMs = GenerateRandomDelay(_options.MinClickDelayMs, _options.MaxClickDelayMs)
        };

        // Add coordinate jitter
        if (command.Type == InputType.Tap || command.Type == InputType.Swipe)
        {
            humanized = humanized with
            {
                X = command.X + _random.Next(-_options.CoordinateJitterMaxPx, _options.CoordinateJitterMaxPx + 1),
                Y = command.Y + _random.Next(-_options.CoordinateJitterMaxPx, _options.CoordinateJitterMaxPx + 1)
            };

            if (command.Type == InputType.Swipe)
            {
                humanized = humanized with
                {
                    EndX = command.EndX + _random.Next(-_options.CoordinateJitterMaxPx, _options.CoordinateJitterMaxPx + 1),
                    EndY = command.EndY + _random.Next(-_options.CoordinateJitterMaxPx, _options.CoordinateJitterMaxPx + 1)
                };
            }
        }

        // Generate Bezier path for mouse movement
        if (_options.EnableBezierMouseMovement && (command.Type == InputType.Tap || command.Type == InputType.Swipe))
        {
            var startPoint = GetLastMousePosition(accountId);
            var endPoint = new Point(humanized.X, humanized.Y);
            
            if (command.Type == InputType.Swipe)
            {
                endPoint = new Point(humanized.EndX, humanized.EndY);
            }

            var path = GenerateBezierCurve(startPoint, endPoint, _options.BezierControlPoints, _options.BezierVariance);
            humanized = humanized with { BezierPath = path };
            
            _lastMousePositions[accountId] = endPoint;
        }

        // Add random delay before action
        var minInterval = _lastActionTimes.TryGetValue(accountId, out var lastTime)
            ? (DateTimeOffset.UtcNow - lastTime).TotalMilliseconds
            : _options.MaxActionIntervalMs;

        if (minInterval < _options.MinActionIntervalMs)
        {
            var additionalDelay = (int)(_options.MinActionIntervalMs - minInterval);
            humanized = humanized with { DelayBeforeMs = humanized.DelayBeforeMs + additionalDelay };
        }

        // Random pause simulation
        if (_random.NextDouble() < _options.PauseProbability)
        {
            var pauseDuration = _random.Next(_options.MinPauseDurationMs, _options.MaxPauseDurationMs);
            humanized = humanized with { DelayBeforeMs = humanized.DelayBeforeMs + pauseDuration };
        }

        _lastActionTimes[accountId] = DateTimeOffset.UtcNow;

        return humanized;
    }

    /// <summary>
    /// Sends an alert via Telegram.
    /// </summary>
    public async Task SendTelegramAlertAsync(TelegramAlert alert, CancellationToken ct = default)
    {
        if (!_options.EnableTelegramBot || string.IsNullOrEmpty(_options.TelegramBotToken))
            return;

        try
        {
            var message = FormatAlertMessage(alert);
            
            foreach (var chatId in _options.AllowedChatIds)
            {
                var url = $"{_options.TelegramApiUrl}{_options.TelegramBotToken}/sendMessage";
                var payload = new
                {
                    chat_id = chatId,
                    text = message,
                    parse_mode = "HTML",
                    disable_web_page_preview = true
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync(url, content, ct);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to send Telegram alert to {ChatId}: {StatusCode}", chatId, response.StatusCode);
                }
            }

            _metrics.IncrementHttpRequests(); // Reuse metric
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram alert");
        }
    }

    /// <summary>
    /// Polls for Telegram updates (commands).
    /// </summary>
    private async void PollTelegramUpdatesAsync(object? state)
    {
        if (!_options.EnableRemoteControl || !_options.EnableTelegramBot)
            return;

        try
        {
            var url = $"{_options.TelegramApiUrl}{_options.TelegramBotToken}/getUpdates";
            var params_ = new Dictionary<string, string>
            {
                ["timeout"] = "10",
                ["allowed_updates"] = "["message"]"
            };

            var query = string.Join("&", params_.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
            var response = await _httpClient.GetAsync($"{url}?{query}", _shutdownCts.Token);
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(_shutdownCts.Token);
                var updates = JsonSerializer.Deserialize<TelegramUpdatesResponse>(json);
                
                if (updates?.Result != null)
                {
                    foreach (var update in updates.Result)
                    {
                        if (update.Message?.Text?.StartsWith(_options.CommandPrefix) == true)
                        {
                            await ProcessTelegramCommandAsync(update.Message, _shutdownCts.Token);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error polling Telegram updates");
        }
    }

    /// <summary>
    /// Processes a Telegram command.
    /// </summary>
    private async Task ProcessTelegramCommandAsync(TelegramMessage message, CancellationToken ct)
    {
        if (!_options.AllowedChatIds.Contains(message.Chat.Id.ToString()))
        {
            await SendTelegramMessageAsync(message.Chat.Id, "❌ Unauthorized: Chat ID not allowed.", ct);
            return;
        }

        var commandText = message.Text[_options.CommandPrefix.Length..].Trim();
        var parts = commandText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLowerInvariant();
        var args = parts.Skip(1).ToArray();

        string response = command switch
        {
            "status" => await GetStatusAsync(args, ct),
            "pause_all" => await PauseAllAsync(ct),
            "resume_all" => await ResumeAllAsync(ct),
            "restart_game" => await RestartGameAsync(args, ct),
            "pause" => await PauseAccountAsync(args, ct),
            "resume" => await ResumeAccountAsync(args, ct),
            "stop" => await StopAccountAsync(args, ct),
            "screenshot" => await TakeScreenshotAsync(args, ct),
            "help" => GetHelpText(),
            _ => $"❓ Unknown command: {command}. Use /help for available commands."
        };

        await SendTelegramMessageAsync(message.Chat.Id, response, ct);
    }

    private async Task<string> GetStatusAsync(string[] args, CancellationToken ct)
    {
        // In real implementation, query orchestrator for account statuses
        return "📊 <b>KKBOT Status</b>
🟢 Running
📱 5 accounts active
⚡ CPU: 45%
💾 RAM: 2.3 GB";
    }

    private async Task<string> PauseAllAsync(CancellationToken ct)
    {
        // In real implementation, pause all instances
        return "⏸️ All accounts paused";
    }

    private async Task<string> ResumeAllAsync(CancellationToken ct)
    {
        return "▶️ All accounts resumed";
    }

    private async Task<string> RestartGameAsync(string[] args, CancellationToken ct)
    {
        var accountId = args.Length > 0 ? args[0] : "all";
        return $"🔄 Restarting game for {accountId}";
    }

    private async Task<string> PauseAccountAsync(string[] args, CancellationToken ct)
    {
        var accountId = args.Length > 0 ? args[0] : "all";
        return $"⏸️ Paused account: {accountId}";
    }

    private async Task<string> ResumeAccountAsync(string[] args, CancellationToken ct)
    {
        var accountId = args.Length > 0 ? args[0] : "all";
        return $"▶️ Resumed account: {accountId}";
    }

    private async Task<string> StopAccountAsync(string[] args, CancellationToken ct)
    {
        var accountId = args.Length > 0 ? args[0] : "all";
        return $"⏹️ Stopped account: {accountId}";
    }

    private async Task<string> TakeScreenshotAsync(string[] args, CancellationToken ct)
    {
        var accountId = args.Length > 0 ? args[0] : "all";
        return $"📸 Screenshot requested for {accountId}";
    }

    private string GetHelpText()
    {
        return @"🤖 <b>KKBOT Commands</b>
/status - Show all accounts status
/pause_all - Pause all accounts
/resume_all - Resume all accounts
/restart_game [account_id] - Restart game for account
/pause [account_id] - Pause specific account
/resume [account_id] - Resume specific account
/stop [account_id] - Stop specific account
/screenshot [account_id] - Take screenshot
/help - Show this help";
    }

    private async Task SendTelegramMessageAsync(long chatId, string text, CancellationToken ct)
    {
        if (!_options.EnableTelegramBot || string.IsNullOrEmpty(_options.TelegramBotToken))
            return;

        try
        {
            var url = $"{_options.TelegramApiUrl}{_options.TelegramBotToken}/sendMessage";
            var payload = new
            {
                chat_id = chatId,
                text = text,
                parse_mode = "HTML"
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            await _httpClient.PostAsync(url, content, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram message");
        }
    }

    private Point GetLastMousePosition(string accountId)
    {
        if (_lastMousePositions.TryGetValue(accountId, out var pos))
            return pos;
        return new Point(0, 0);
    }

    private Point[] GenerateBezierCurve(Point start, Point end, int controlPoints, double variance)
    {
        var points = new List<Point> { start };
        
        for (int i = 1; i < controlPoints; i++)
        {
            var t = (double)i / controlPoints;
            var cpX = (int)(start.X + (end.X - start.X) * t + (_random.NextDouble() - 0.5) * variance * Math.Abs(end.X - start.X));
            var cpY = (int)(start.Y + (end.Y - start.Y) * t + (_random.NextDouble() - 0.5) * variance * Math.Abs(end.Y - start.Y));
            points.Add(new Point(cpX, cpY));
        }
        
        points.Add(end);
        
        // Generate smooth curve points using De Casteljau's algorithm
        var curvePoints = new List<Point>();
        for (int i = 0; i <= 20; i++)
        {
            var t = i / 20.0;
            var point = CalculateBezierPoint(points, t);
            curvePoints.Add(point);
        }
        
        return curvePoints.ToArray();
    }

    private Point CalculateBezierPoint(List<Point> controlPoints, double t)
    {
        var points = new List<Point>(controlPoints);
        int n = points.Count - 1;
        
        while (n > 0)
        {
            for (int i = 0; i < n; i++)
            {
                var x = (int)(points[i].X + (points[i + 1].X - points[i].X) * t);
                var y = (int)(points[i].Y + (points[i + 1].Y - points[i].Y) * t);
                points[i] = new Point(x, y);
            }
            n--;
        }
        
        return points[0];
    }

    private string FormatAlertMessage(TelegramAlert alert)
    {
        var emoji = alert.Type switch
        {
            AlertType.Death => "💀",
            AlertType.VipDrop => "💎",
            AlertType.Disconnect => "🔌",
            AlertType.LowHp => "❤️",
            AlertType.VipItemDrop => "💎",
            AlertType.LevelUp => "⬆️",
            AlertType.QuestComplete => "✅",
            AlertType.Critical => "🚨",
            AlertType.Warning => "⚠️",
            _ => "ℹ️"
        };

        return $"{emoji} <b>{alert.Type}</b>
" +
               $"👤 Account: <code>{alert.AccountId}</code>
" +
               $"🎮 Character: {alert.CharacterName}
" +
               $"📝 {alert.Message}
" +
               $"🕐 {alert.Timestamp:HH:mm:ss}";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _telegramPollTimer?.Dispose();
        _shutdownCts.Cancel();
        _shutdownCts.Dispose();
        _httpClient.Dispose();
    }
}

/// <summary>
/// Humanize input task.
/// </summary>
public sealed class HumanizeInputTask : IGameTaskDefinition
{
    public string Id => "humanize-input";
    public string Name => "Humanize Input";
    public string? Description => "Applies humanization to input commands";
    public TaskType Type => TaskType.Repeating;
    public TimeSpan DefaultTimeout => TimeSpan.FromSeconds(30);
    public RetryPolicy DefaultRetryPolicy => new(1, 1000);
    public bool RequiresApproval => false;

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        // In real implementation, would apply humanization to pending inputs
        return new IdleState();
    }
}

/// <summary>
/// Send alert task.
/// </summary>
public sealed class SendAlertTask : IGameTaskDefinition
{
    public string Id => "send-alert";
    public string Name => "Send Alert";
    public string? Description => "Sends alert via Telegram";
    public TaskType Type => TaskType.OneShot;
    public TimeSpan DefaultTimeout => TimeSpan.FromSeconds(10);
    public RetryPolicy DefaultRetryPolicy => new(3, 2000);
    public bool RequiresApproval => false;

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        // In real implementation, would send alert via Telegram
        return new IdleState();
    }
}

/// <summary>
/// Idle state for anti-detection tasks.
/// </summary>
public sealed class IdleState : IStateDefinition
{
    public string Id => "idle";
    public string Name => "Idle";
    public string Category => "anti-detection";
    public Func<PerceptionContext?, bool> EntryCondition => _ => true;
    public IStateAction? Action => null;
    public IReadOnlyList<string> NextStateIds => Array.Empty<string>();
    public TimeSpan Timeout => TimeSpan.FromSeconds(1);
    public RetryPolicy RetryPolicy => new(0, 0);
    public EvidencePolicy EvidencePolicy => new(false, false);
}

/// <summary>
/// Telegram API response models.
/// </summary>
internal sealed record TelegramUpdatesResponse
{
    public bool Ok { get; init; }
    public TelegramUpdate[]? Result { get; init; }
}

internal sealed record TelegramUpdate
{
    public long UpdateId { get; init; }
    public TelegramMessage? Message { get; init; }
}

internal sealed record TelegramMessage
{
    public long MessageId { get; init; }
    public TelegramChat Chat { get; init; } = new();
    public long Date { get; init; }
    public string Text { get; init; } = string.Empty;
    public TelegramUser From { get; init; } = new();
}

internal sealed record TelegramChat
{
    public long Id { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
}

internal sealed record TelegramUser
{
    public long Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
}