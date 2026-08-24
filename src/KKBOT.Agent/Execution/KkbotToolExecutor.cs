// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KKBOT.Agent.Abstractions;
using KKBOT.AI.Abstractions;
using KKBOT.Perception.Abstractions;
using KKBOT.Device.Abstractions;
using KKBOT.Plugins.Abstractions;
using KKBOT.Orchestration;
using KKBOT.Observability;
using KKBOT.Observability.Tracing;

namespace KKBOT.Agent.Execution;

/// <summary>
/// Executes agent tools by integrating with KKBOT subsystems.
/// </summary>
public sealed class KkbotToolExecutor : IToolExecutor
{
    private readonly ILogger<KkbotToolExecutor> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ToolExecutorOptions _options;
    private readonly ConcurrentDictionary<string, ToolDefinition> _toolRegistry = new();
    private readonly IMetricsRegistry _metrics;

    public KkbotToolExecutor(
        ILogger<KkbotToolExecutor> logger,
        IServiceProvider serviceProvider,
        IOptions<ToolExecutorOptions> options,
        IMetricsRegistry metrics)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _metrics = metrics;
        
        RegisterBuiltInTools();
    }

    public async Task<ToolResult> ExecuteAsync(string toolName, Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        using var activity = KkbotActivitySources.AI.StartActivity($"tool.{toolName}", ActivityKind.Internal);
        activity?.SetTag(KkbotTags.ModuleId, "agent-tool-executor");
        
        var startTime = DateTime.UtcNow;
        
        try
        {
            if (!_toolRegistry.TryGetValue(toolName, out var toolDef))
            {
                return ToolResult.Failed($"Tool '{toolName}' not found");
            }

            _logger.LogDebug("Executing tool: {ToolName} with parameters: {Parameters}", toolName, JsonSerializer.Serialize(parameters));

            // Validate permissions (in real implementation, check against agent/user permissions)
            if (!await ValidatePermissionsAsync(toolDef.RequiredPermissions, cancellationToken))
            {
                return ToolResult.Failed($"Insufficient permissions for tool '{toolName}'");
            }

            // Execute tool
            var result = await ExecuteToolInternalAsync(toolName, parameters, cancellationToken);
            
            var duration = DateTime.UtcNow - startTime;
            _metrics.RecordAiInferenceLatency(duration.TotalMilliseconds); // Reuse metric
            
            if (result.Success)
            {
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            else
            {
                activity?.SetStatus(ActivityStatusCode.Error, result.Error);
            }

            return result with { Duration = duration };
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "Tool execution failed: {ToolName}", toolName);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return ToolResult.Failed(ex.Message) with { Duration = duration };
        }
    }

    public IReadOnlyList<ToolDefinition> GetAvailableTools()
    {
        return _toolRegistry.Values.ToList().AsReadOnly();
    }

    public Task<ToolDefinition?> GetToolDefinitionAsync(string toolName)
    {
        _toolRegistry.TryGetValue(toolName, out var tool);
        return Task.FromResult(tool);
    }

    private void RegisterBuiltInTools()
    {
        // Screen capture
        RegisterTool(new ToolDefinition
        {
            Name = "capture_screen",
            Description = "Capture current game screen",
            ParametersSchema = JsonSchemaBuilder.Object(
                ("region", JsonSchemaBuilder.OptionalObject("Screen region to capture", 
                    ("x", JsonSchemaBuilder.Integer()), ("y", JsonSchemaBuilder.Integer()),
                    ("width", JsonSchemaBuilder.Integer()), ("height", JsonSchemaBuilder.Integer())
                )),
                ("format", JsonSchemaBuilder.String("Image format", "png", new[] { "png", "jpg" })),
                ("quality", JsonSchemaBuilder.Integer("Image quality 1-100", 90))
            ),
            RequiredPermissions = new[] { "DEVICE_READ" },
            Category = "perception"
        });

        // Template detection
        RegisterTool(new ToolDefinition
        {
            Name = "detect_template",
            Description = "Find template on screen using template matching",
            ParametersSchema = JsonSchemaBuilder.Object(
                ("template_name", JsonSchemaBuilder.RequiredString("Name of template to find")),
                ("threshold", JsonSchemaBuilder.Number("Match threshold 0-1", 0.85)),
                ("region", JsonSchemaBuilder.OptionalObject("Search region",
                    ("x", JsonSchemaBuilder.Integer()), ("y", JsonSchemaBuilder.Integer()),
                    ("width", JsonSchemaBuilder.Integer()), ("height", JsonSchemaBuilder.Integer())
                ))
            ),
            RequiredPermissions = new[] { "DEVICE_READ" },
            Category = "perception"
        });

        // Input sending
        RegisterTool(new ToolDefinition
        {
            Name = "send_input",
            Description = "Send tap, swipe, text, or key input to device",
            ParametersSchema = JsonSchemaBuilder.Object(
                ("type", JsonSchemaBuilder.RequiredString("Input type", new[] { "tap", "swipe", "text", "key" })),
                ("x", JsonSchemaBuilder.Integer("X coordinate")),
                ("y", JsonSchemaBuilder.Integer("Y coordinate")),
                ("end_x", JsonSchemaBuilder.OptionalInteger("End X for swipe")),
                ("end_y", JsonSchemaBuilder.OptionalInteger("End Y for swipe")),
                ("text", JsonSchemaBuilder.OptionalString("Text to input")),
                ("duration_ms", JsonSchemaBuilder.OptionalInteger("Duration in ms for swipe/hold")),
                ("key_code", JsonSchemaBuilder.OptionalInteger("Android key code"))
            ),
            RequiredPermissions = new[] { "INPUT_WRITE" },
            RiskLevel = RiskLevel.Medium,
            Category = "device"
        });

        // OCR
        RegisterTool(new ToolDefinition
        {
            Name = "run_ocr",
            Description = "Extract text from screen region using OCR",
            ParametersSchema = JsonSchemaBuilder.Object(
                ("region", JsonSchemaBuilder.OptionalObject("Screen region",
                    ("x", JsonSchemaBuilder.Integer()), ("y", JsonSchemaBuilder.Integer()),
                    ("width", JsonSchemaBuilder.Integer()), ("height", JsonSchemaBuilder.Integer())
                )),
                ("language", JsonSchemaBuilder.String("OCR language", "eng")),
                ("whitelist", JsonSchemaBuilder.OptionalString("Character whitelist"))
            ),
            RequiredPermissions = new[] { "DEVICE_READ" },
            Category = "perception"
        });

        // AI Vision
        RegisterTool(new ToolDefinition
        {
            Name = "ai_vision",
            Description = "Ask vision model about screen content",
            ParametersSchema = JsonSchemaBuilder.Object(
                ("prompt", JsonSchemaBuilder.RequiredString("Question or task for vision model")),
                ("image_base64", JsonSchemaBuilder.OptionalString("Base64 image (optional, will capture if not provided)")),
                ("model", JsonSchemaBuilder.String("Vision model", "llava:7b"))
            ),
            RequiredPermissions = new[] { "DEVICE_READ", "AI_USE" },
            Category = "ai"
        });

        // Execute skill
        RegisterTool(new ToolDefinition
        {
            Name = "execute_skill",
            Description = "Execute a learned or built-in skill",
            ParametersSchema = JsonSchemaBuilder.Object(
                ("skill_id", JsonSchemaBuilder.RequiredString("Skill identifier")),
                ("parameters", JsonSchemaBuilder.Object("Skill parameters"))
            ),
            RequiredPermissions = new[] { "PLUGIN_EXECUTE" },
            Category = "skill"
        });

        // Wait for condition
        RegisterTool(new ToolDefinition
        {
            Name = "wait_for",
            Description = "Wait for a perception condition to be met",
            ParametersSchema = JsonSchemaBuilder.Object(
                ("condition", JsonSchemaBuilder.RequiredString("Condition expression (CEL)")),
                ("timeout_ms", JsonSchemaBuilder.Integer("Max wait time in ms", 30000)),
                ("poll_interval_ms", JsonSchemaBuilder.Integer("Poll interval in ms", 500)),
                ("template_name", JsonSchemaBuilder.OptionalString("Template to wait for")),
                ("threshold", JsonSchemaBuilder.Number("Match threshold", 0.85))
            ),
            RequiredPermissions = new[] { "DEVICE_READ" },
            Category = "control"
        });

        // Get game state
        RegisterTool(new ToolDefinition
        {
            Name = "get_game_state",
            Description = "Get current game state from memory/perception",
            ParametersSchema = JsonSchemaBuilder.Object(
                ("keys", JsonSchemaBuilder.OptionalArray("Specific state keys to retrieve", JsonSchemaBuilder.String()))
            ),
            RequiredPermissions = new[] { "DEVICE_READ" },
            Category = "state"
        });

        // Set variable
        RegisterTool(new ToolDefinition
        {
            Name = "set_variable",
            Description = "Set a variable in agent working memory",
            ParametersSchema = JsonSchemaBuilder.Object(
                ("name", JsonSchemaBuilder.RequiredString("Variable name")),
                ("value", JsonSchemaBuilder.OptionalAny("Variable value"))
            ),
            RequiredPermissions = Array.Empty<string>(),
            Category = "memory"
        });

        // Get variable
        RegisterTool(new ToolDefinition
        {
            Name = "get_variable",
            Description = "Get a variable from agent working memory",
            ParametersSchema = JsonSchemaBuilder.Object(
                ("name", JsonSchemaBuilder.RequiredString("Variable name"))
            ),
            RequiredPermissions = Array.Empty<string>(),
            Category = "memory"
        });
    }

    private void RegisterTool(ToolDefinition tool)
    {
        _toolRegistry[tool.Name] = tool;
    }

    private async Task<ToolResult> ExecuteToolInternalAsync(string toolName, Dictionary<string, object> parameters, CancellationToken ct)
    {
        return toolName switch
        {
            "capture_screen" => await ExecuteCaptureScreenAsync(parameters, ct),
            "detect_template" => await ExecuteDetectTemplateAsync(parameters, ct),
            "send_input" => await ExecuteSendInputAsync(parameters, ct),
            "run_ocr" => await ExecuteRunOcrAsync(parameters, ct),
            "ai_vision" => await ExecuteAiVisionAsync(parameters, ct),
            "execute_skill" => await ExecuteSkillAsync(parameters, ct),
            "wait_for" => await ExecuteWaitForAsync(parameters, ct),
            "get_game_state" => await ExecuteGetGameStateAsync(parameters, ct),
            "set_variable" => await ExecuteSetVariableAsync(parameters, ct),
            "get_variable" => await ExecuteGetVariableAsync(parameters, ct),
            _ => ToolResult.Failed($"Tool '{toolName}' not implemented")
        };
    }

    private async Task<ToolResult> ExecuteCaptureScreenAsync(Dictionary<string, object> parameters, CancellationToken ct)
    {
        var session = _serviceProvider.GetRequiredService<IDeviceSessionFactory>();
        // In real implementation, get session from context
        // For now, return mock
        return ToolResult.SuccessResult(new { frameId = Guid.NewGuid(), width = 1920, height = 1080, captured = true });
    }

    private async Task<ToolResult> ExecuteDetectTemplateAsync(Dictionary<string, object> parameters, CancellationToken ct)
    {
        var perception = _serviceProvider.GetRequiredService<IPerceptionPipeline>();
        // In real implementation, get frame from context
        return ToolResult.SuccessResult(new { template = "health_bar", confidence = 0.92, matched = true });
    }

    private async Task<ToolResult> ExecuteSendInputAsync(Dictionary<string, object> parameters, CancellationToken ct)
    {
        var session = _serviceProvider.GetRequiredService<IDeviceSessionFactory>();
        // In real implementation, get session from context
        return ToolResult.SuccessResult(new { success = true, inputType = parameters.GetValueOrDefault("type") });
    }

    private async Task<ToolResult> ExecuteRunOcrAsync(Dictionary<string, object> parameters, CancellationToken ct)
    {
        // In real implementation, use IOcrEngine
        return ToolResult.SuccessResult(new { text = "HP: 100/100", confidence = 0.95 });
    }

    private async Task<ToolResult> ExecuteAiVisionAsync(Dictionary<string, object> parameters, CancellationToken ct)
    {
        var visionService = _serviceProvider.GetService<IAiVisionService>();
        if (visionService == null)
            return ToolResult.Failed("Vision service not available");

        var prompt = parameters.GetValueOrDefault("prompt")?.ToString() ?? "";
        var imageBase64 = parameters.GetValueOrDefault("image_base64")?.ToString();
        
        byte[]? imageData = null;
        if (!string.IsNullOrEmpty(imageBase64))
        {
            imageData = Convert.FromBase64String(imageBase64);
        }

        var response = await visionService.GenerateAsync(imageData ?? Array.Empty<byte>(), prompt, ct);
        return ToolResult.SuccessResult(new { response = response.Content });
    }

    private async Task<ToolResult> ExecuteSkillAsync(Dictionary<string, object> parameters, CancellationToken ct)
    {
        var skillId = parameters.GetValueOrDefault("skill_id")?.ToString();
        if (string.IsNullOrEmpty(skillId))
            return ToolResult.Failed("skill_id is required");

        // In real implementation, look up skill from memory and execute its plan
        return ToolResult.SuccessResult(new { skillId, executed = true, result = "completed" });
    }

    private async Task<ToolResult> ExecuteWaitForAsync(Dictionary<string, object> parameters, CancellationToken ct)
    {
        var timeoutMs = Convert.ToInt32(parameters.GetValueOrDefault("timeout_ms") ?? 30000);
        var pollIntervalMs = Convert.ToInt32(parameters.GetValueOrDefault("poll_interval_ms") ?? 500);
        var condition = parameters.GetValueOrDefault("condition")?.ToString() ?? "";
        
        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromMilliseconds(timeoutMs);
        var pollInterval = TimeSpan.FromMilliseconds(pollIntervalMs);

        while (DateTime.UtcNow - startTime < timeout)
        {
            ct.ThrowIfCancellationRequested();
            
            // In real implementation, evaluate CEL condition against perception/game state
            // For now, simulate
            await Task.Delay(pollInterval, ct);
            
            // Mock: condition met after 2 polls
            if ((DateTime.UtcNow - startTime).TotalMilliseconds > pollIntervalMs * 2)
            {
                return ToolResult.SuccessResult(new { conditionMet = true, waitTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds });
            }
        }

        return ToolResult.Failed("Condition not met within timeout");
    }

    private async Task<ToolResult> ExecuteGetGameStateAsync(Dictionary<string, object> parameters, CancellationToken ct)
    {
        // In real implementation, get from game state provider
        return ToolResult.SuccessResult(new { state = new { hp = 100, mana = 50, level = 25 } });
    }

    private async Task<ToolResult> ExecuteSetVariableAsync(Dictionary<string, object> parameters, CancellationToken ct)
    {
        var name = parameters.GetValueOrDefault("name")?.ToString();
        var value = parameters.GetValueOrDefault("value");
        
        if (string.IsNullOrEmpty(name))
            return ToolResult.Failed("Variable name is required");

        // In real implementation, store in agent working memory
        return ToolResult.SuccessResult(new { name, value, stored = true });
    }

    private async Task<ToolResult> ExecuteGetVariableAsync(Dictionary<string, object> parameters, CancellationToken ct)
    {
        var name = parameters.GetValueOrDefault("name")?.ToString();
        if (string.IsNullOrEmpty(name))
            return ToolResult.Failed("Variable name is required");

        // In real implementation, retrieve from agent working memory
        return ToolResult.SuccessResult(new { name, value = "mock_value", found = true });
    }

    private async Task<bool> ValidatePermissionsAsync(string[] requiredPermissions, CancellationToken ct)
    {
        // In real implementation, check against current user/agent permissions
        return true;
    }
}

/// <summary>
/// Tool executor options.
/// </summary>
public sealed class ToolExecutorOptions
{
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxConcurrentTools { get; set; } = 3;
    public bool EnableMetrics { get; set; } = true;
}

/// <summary>
/// Helper for building JSON schemas.
/// </summary>
public static class JsonSchemaBuilder
{
    public static JsonSchema Object(params (string name, JsonSchema schema)[] properties)
    {
        return new JsonSchema
        {
            Type = "object",
            Properties = properties.ToDictionary(p => p.name, p => p.schema),
            Required = properties.Where(p => p.schema.Properties?.Values.Any(v => v.Type != "optional") ?? false).Select(p => p.name).ToArray()
        };
    }

    public static JsonSchema String(string description = "", string? defaultValue = null, string[]? enumValues = null)
    {
        return new JsonSchema
        {
            Type = "string",
            Description = description,
            Default = defaultValue,
            Properties = enumValues != null ? new Dictionary<string, JsonSchema> { ["enum"] = new JsonSchema { Type = "array", Properties = enumValues.Select((v, i) => (i.ToString(), new JsonSchema { Type = "string", Default = v })).ToDictionary() } } : null
        };
    }

    public static JsonSchema Integer(string description = "", int? defaultValue = null)
    {
        return new JsonSchema { Type = "integer", Description = description, Default = defaultValue };
    }

    public static JsonSchema Number(string description = "", double? defaultValue = null)
    {
        return new JsonSchema { Type = "number", Description = description, Default = defaultValue };
    }

    public static JsonSchema RequiredString(string description = "", string[]? enumValues = null)
    {
        var schema = String(description, enumValues: enumValues);
        // Mark as required via parent's Required array
        return schema;
    }

    public static JsonSchema OptionalString(string description = "", string? defaultValue = null)
    {
        return String(description, defaultValue);
    }

    public static JsonSchema OptionalInteger(string description = "", int? defaultValue = null)
    {
        return Integer(description, defaultValue);
    }

    public static JsonSchema OptionalObject(string description = "", params (string name, JsonSchema schema)[] properties)
    {
        return Object(properties);
    }

    public static JsonSchema OptionalArray(string description = "", JsonSchema? items = null)
    {
        return new JsonSchema { Type = "array", Description = description, Items = items };
    }

    public static JsonSchema OptionalAny(string description = "")
    {
        return new JsonSchema { Type = "any", Description = description };
    }
}

/// <summary>
/// Extension for ToolResult.
/// </summary>
public static class ToolResultExtensions
{
    public static ToolResult SuccessResult(this ToolResult _, object result)
    {
        return new ToolResult { Success = true, Result = result };
    }

    public static ToolResult Failed(this ToolResult _, string error)
    {
        return new ToolResult { Success = false, Error = error };
    }
}