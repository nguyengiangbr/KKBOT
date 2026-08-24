// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Wasmtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using KKBOT.Plugins.Abstractions;
using KKBOT.Device.Abstractions;
using KKBOT.Perception.Abstractions;
using KKBOT.AI.Abstractions;
using KKBOT.Observability;

namespace KKBOT.Plugins.Wasm.HostFunctions;

/// <summary>
/// Registers all KKBOT host functions for WASM plugins.
/// </summary>
public static class KkbotHostFunctions
{
    /// <summary>
    /// Registers all built-in host functions with the registry.
    /// </summary>
    public static void RegisterAll(HostFunctionRegistry registry, IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("KKBOT.HostFunctions");
        
        // Device functions
        registry.RegisterMany(CreateDeviceFunctions(serviceProvider, logger));
        
        // Perception functions
        registry.RegisterMany(CreatePerceptionFunctions(serviceProvider, logger));
        
        // AI functions
        registry.RegisterMany(CreateAiFunctions(serviceProvider, logger));
        
        // Storage functions
        registry.RegisterMany(CreateStorageFunctions(serviceProvider, logger));
        
        // Network functions
        registry.RegisterMany(CreateNetworkFunctions(serviceProvider, logger));
        
        // System functions
        registry.RegisterMany(CreateSystemFunctions(serviceProvider, logger));
        
        // Logging functions
        registry.RegisterMany(CreateLoggingFunctions(serviceProvider, logger));
        
        logger.LogInformation("Registered all KKBOT host functions");
    }

    private static IEnumerable<HostFunction> CreateDeviceFunctions(IServiceProvider sp, ILogger logger)
    {
        yield return new HostFunction
        {
            Namespace = "kkbot",
            Name = "device_capture",
            Description = "Capture screen from device",
            ParameterTypes = new[] { ValType.I32, ValType.I32 }, // input_ptr, input_len
            ReturnTypes = new[] { ValType.I32, ValType.I32 },   // output_ptr, output_len
            RequiredCapability = PluginCapability.DeviceRead,
            Implementation = async (caller, args) =>
            {
                var inputPtr = (int)args[0].GetI32();
                var inputLen = (int)args[1].GetI32();
                var inputJson = ReadString(caller, inputPtr, inputLen);
                var input = JsonSerializer.Deserialize<DeviceCaptureInput>(inputJson);

                var factory = sp.GetRequiredService<IDeviceSessionFactory>();
                var session = await factory.CreateAsync(input.DeviceId, CancellationToken.None);
                
                var frame = await session.CaptureAsync(new CaptureOptions
                {
                    RegionOfInterest = input.Region != null ? new Rectangle(input.Region.X, input.Region.Y, input.Region.Width, input.Region.Height) : null,
                    Format = input.Format ?? "png",
                    Quality = input.Quality ?? 90
                }, CancellationToken.None);

                var output = new DeviceCaptureOutput
                {
                    FrameId = frame.FrameId.ToString(),
                    Width = frame.Width,
                    Height = frame.Height,
                    Format = frame.Format,
                    DataBase64 = Convert.ToBase64String(frame.Data),
                    CaptureLatencyMs = frame.CaptureLatencyMs
                };

                return WriteJson(caller, output);
            }
        };

        yield return new HostFunction
        {
            Namespace = "kkbot",
            Name = "device_input",
            Description = "Send input to device",
            ParameterTypes = new[] { ValType.I32, ValType.I32 },
            ReturnTypes = new[] { ValType.I32, ValType.I32 },
            RequiredCapability = PluginCapability.DeviceWrite,
            Implementation = async (caller, args) =>
            {
                var inputPtr = (int)args[0].GetI32();
                var inputLen = (int)args[1].GetI32();
                var inputJson = ReadString(caller, inputPtr, inputLen);
                var input = JsonSerializer.Deserialize<DeviceInputInput>(inputJson);

                var factory = sp.GetRequiredService<IDeviceSessionFactory>();
                var session = await factory.CreateAsync(input.DeviceId, CancellationToken.None);
                
                var command = new InputCommand
                {
                    Type = Enum.Parse<InputType>(input.Type, true),
                    X = input.X,
                    Y = input.Y,
                    EndX = input.EndX,
                    EndY = input.EndY,
                    Text = input.Text,
                    Duration = input.DurationMs.HasValue ? TimeSpan.FromMilliseconds(input.DurationMs.Value) : null,
                    KeyCode = input.KeyCode
                };

                var result = await session.SendInputAsync(command, CancellationToken.None);

                var output = new DeviceInputOutput
                {
                    Success = result.IsSuccess,
                    ErrorMessage = result.ErrorMessage,
                    DurationMs = result.Duration.TotalMilliseconds
                };

                return WriteJson(caller, output);
            }
        };

        yield return new HostFunction
        {
            Namespace = "kkbot",
            Name = "device_info",
            Description = "Get device information",
            ParameterTypes = new[] { ValType.I32, ValType.I32 },
            ReturnTypes = new[] { ValType.I32, ValType.I32 },
            RequiredCapability = PluginCapability.DeviceRead,
            Implementation = async (caller, args) =>
            {
                var inputPtr = (int)args[0].GetI32();
                var inputLen = (int)args[1].GetI32();
                var inputJson = ReadString(caller, inputPtr, inputLen);
                var input = JsonSerializer.Deserialize<DeviceInfoInput>(inputJson);

                var factory = sp.GetRequiredService<IDeviceSessionFactory>();
                var session = await factory.CreateAsync(input.DeviceId, CancellationToken.None);
                var info = await session.GetInfoAsync(CancellationToken.None);

                var output = new DeviceInfoOutput
                {
                    DeviceId = info.DeviceId,
                    Model = info.Model,
                    Manufacturer = info.Manufacturer,
                    AndroidVersion = info.AndroidVersion,
                    ScreenWidth = info.ScreenWidth,
                    ScreenHeight = info.ScreenHeight,
                    ScreenDensity = info.ScreenDensity,
                    BatteryLevel = info.BatteryLevel,
                    IsCharging = info.IsCharging
                };

                return WriteJson(caller, output);
            }
        };
    }

    private static IEnumerable<HostFunction> CreatePerceptionFunctions(IServiceProvider sp, ILogger logger)
    {
        yield return new HostFunction
        {
            Namespace = "kkbot",
            Name = "perception_detect",
            Description = "Run template detection on frame",
            ParameterTypes = new[] { ValType.I32, ValType.I32 },
            ReturnTypes = new[] { ValType.I32, ValType.I32 },
            RequiredCapability = PluginCapability.PerceptionBasic,
            Implementation = async (caller, args) =>
            {
                var inputPtr = (int)args[0].GetI32();
                var inputLen = (int)args[1].GetI32();
                var inputJson = ReadString(caller, inputPtr, inputLen);
                var input = JsonSerializer.Deserialize<PerceptionDetectInput>(inputJson);

                var pipeline = sp.GetRequiredService<IPerceptionPipeline>();
                
                // Reconstruct frame from base64
                var frameData = Convert.FromBase64String(input.FrameBase64);
                var frame = new Frame
                {
                    Width = input.Width,
                    Height = input.Height,
                    Data = frameData,
                    Format = "png"
                };

                var results = await pipeline.ProcessAsync(frame, CancellationToken.None);
                var detections = results.Select(r => new DetectionOutput
                {
                    DetectorId = r.DetectorId,
                    DetectorName = r.DetectorName,
                    Confidence = r.Confidence,
                    IsMatch = r.IsMatch,
                    DetectedValue = r.DetectedValue,
                    BoundingBox = r.BoundingBox != null ? new BoundingBoxOutput
                    {
                        X = r.BoundingBox.Value.X,
                        Y = r.BoundingBox.Value.Y,
                        Width = r.BoundingBox.Value.Width,
                        Height = r.BoundingBox.Value.Height
                    } : null
                }).ToArray();

                var output = new PerceptionDetectOutput { Detections = detections };
                return WriteJson(caller, output);
            }
        };

        yield return new HostFunction
        {
            Namespace = "kkbot",
            Name = "perception_ocr",
            Description = "Run OCR on frame region",
            ParameterTypes = new[] { ValType.I32, ValType.I32 },
            ReturnTypes = new[] { ValType.I32, ValType.I32 },
            RequiredCapability = PluginCapability.PerceptionAdvanced,
            Implementation = async (caller, args) =>
            {
                var inputPtr = (int)args[0].GetI32();
                var inputLen = (int)args[1].GetI32();
                var inputJson = ReadString(caller, inputPtr, inputLen);
                var input = JsonSerializer.Deserialize<PerceptionOcrInput>(inputJson);

                var ocrEngine = sp.GetService<IOcrEngine>();
                if (ocrEngine == null)
                {
                    var output = new PerceptionOcrOutput { Text = "", Confidence = 0, Error = "OCR engine not available" };
                    return WriteJson(caller, output);
                }

                var frameData = Convert.FromBase64String(input.FrameBase64);
                var result = await ocrEngine.RecognizeAsync(frameData, CancellationToken.None);

                var output = new PerceptionOcrOutput
                {
                    Text = result.Text,
                    Confidence = result.Confidence
                };
                return WriteJson(caller, output);
            }
        };
    }

    private static IEnumerable<HostFunction> CreateAiFunctions(IServiceProvider sp, ILogger logger)
    {
        yield return new HostFunction
        {
            Namespace = "kkbot",
            Name = "ai_generate",
            Description = "Generate text using AI model",
            ParameterTypes = new[] { ValType.I32, ValType.I32 },
            ReturnTypes = new[] { ValType.I32, ValType.I32 },
            RequiredCapability = PluginCapability.AiInference,
            Implementation = async (caller, args) =>
            {
                var inputPtr = (int)args[0].GetI32();
                var inputLen = (int)args[1].GetI32();
                var inputJson = ReadString(caller, inputPtr, inputLen);
                var input = JsonSerializer.Deserialize<AiGenerateInput>(inputJson);

                var aiService = sp.GetRequiredService<IAiService>();
                var response = await aiService.GenerateAsync(input.Prompt, CancellationToken.None);

                var output = new AiGenerateOutput
                {
                    Content = response.Content,
                    Tokens = response.Usage.Tokens,
                    DurationMs = response.Usage.DurationMs
                };
                return WriteJson(caller, output);
            }
        };

        yield return new HostFunction
        {
            Namespace = "kkbot",
            Name = "ai_vision",
            Description = "Analyze image with vision model",
            ParameterTypes = new[] { ValType.I32, ValType.I32 },
            ReturnTypes = new[] { ValType.I32, ValType.I32 },
            RequiredCapability = PluginCapability.AiVision,
            Implementation = async (caller, args) =>
            {
                var inputPtr = (int)args[0].GetI32();
                var inputLen = (int)args[1].GetI32();
                var inputJson = ReadString(caller, inputPtr, inputLen);
                var input = JsonSerializer.Deserialize<AiVisionInput>(inputJson);

                var visionService = sp.GetService<IAiVisionService>();
                if (visionService == null)
                {
                    var output = new AiVisionOutput { Content = "", Error = "Vision service not available" };
                    return WriteJson(caller, output);
                }

                var imageData = Convert.FromBase64String(input.ImageBase64);
                var response = await visionService.GenerateAsync(imageData, input.Prompt, CancellationToken.None);

                var output = new AiVisionOutput
                {
                    Content = response.Content,
                    Tokens = response.Usage.Tokens,
                    DurationMs = response.Usage.DurationMs
                };
                return WriteJson(caller, output);
            }
        };
    }

    private static IEnumerable<HostFunction> CreateStorageFunctions(IServiceProvider sp, ILogger logger)
    {
        yield return new HostFunction
        {
            Namespace = "kkbot",
            Name = "storage_read",
            Description = "Read file from plugin storage",
            ParameterTypes = new[] { ValType.I32, ValType.I32 },
            ReturnTypes = new[] { ValType.I32, ValType.I32 },
            RequiredCapability = PluginCapability.StorageRead,
            Implementation = async (caller, args) =>
            {
                var inputPtr = (int)args[0].GetI32();
                var inputLen = (int)args[1].GetI32();
                var inputJson = ReadString(caller, inputPtr, inputLen);
                var input = JsonSerializer.Deserialize<StorageReadInput>(inputJson);

                var pluginId = caller.GetContextData<string>("plugin_id") ?? "";
                var basePath = Path.Combine("plugins", "wasm", "storage", pluginId);
                var filePath = Path.Combine(basePath, input.Path);
                
                // Security: prevent path traversal
                var fullPath = Path.GetFullPath(filePath);
                var allowedPath = Path.GetFullPath(basePath);
                if (!fullPath.StartsWith(allowedPath))
                {
                    var output = new StorageReadOutput { Success = false, Error = "Path traversal not allowed" };
                    return WriteJson(caller, output);
                }

                if (!File.Exists(fullPath))
                {
                    var output = new StorageReadOutput { Success = false, Error = "File not found" };
                    return WriteJson(caller, output);
                }

                var data = await File.ReadAllBytesAsync(filePath);
                var output = new StorageReadOutput
                {
                    Success = true,
                    DataBase64 = Convert.ToBase64String(data),
                    Size = data.Length
                };
                return WriteJson(caller, output);
            }
        };

        yield return new HostFunction
        {
            Namespace = "kkbot",
            Name = "storage_write",
            Description = "Write file to plugin storage",
            ParameterTypes = new[] { ValType.I32, ValType.I32 },
            ReturnTypes = new[] { ValType.I32, ValType.I32 },
            RequiredCapability = PluginCapability.StorageWrite,
            Implementation = async (caller, args) =>
            {
                var inputPtr = (int)args[0].GetI32();
                var inputLen = (int)args[1].GetI32();
                var inputJson = ReadString(caller, inputPtr, inputLen);
                var input = JsonSerializer.Deserialize<StorageWriteInput>(inputJson);

                var pluginId = caller.GetContextData<string>("plugin_id") ?? "";
                var basePath = Path.Combine("plugins", "wasm", "storage", pluginId);
                var filePath = Path.Combine(basePath, input.Path);
                
                var fullPath = Path.GetFullPath(filePath);
                var allowedPath = Path.GetFullPath(basePath);
                if (!fullPath.StartsWith(allowedPath))
                {
                    var output = new StorageWriteOutput { Success = false, Error = "Path traversal not allowed" };
                    return WriteJson(caller, output);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                var data = Convert.FromBase64String(input.DataBase64);
                await File.WriteAllBytesAsync(fullPath, data);

                var output = new StorageWriteOutput { Success = true, Size = data.Length };
                return WriteJson(caller, output);
            }
        };

        yield return new HostFunction
        {
            Namespace = "kkbot",
            Name = "storage_list",
            Description = "List files in plugin storage",
            ParameterTypes = new[] { ValType.I32, ValType.I32 },
            ReturnTypes = new[] { ValType.I32, ValType.I32 },
            RequiredCapability = PluginCapability.StorageRead,
            Implementation = async (caller, args) =>
            {
                var inputPtr = (int)args[0].GetI32();
                var inputLen = (int)args[1].GetI32();
                var inputJson = ReadString(caller, inputPtr, inputLen);
                var input = JsonSerializer.Deserialize<StorageListInput>(inputJson);

                var pluginId = caller.GetContextData<string>("plugin_id") ?? "";
                var basePath = Path.Combine("plugins", "wasm", "storage", pluginId, input.Prefix ?? "");
                
                var fullPath = Path.GetFullPath(basePath);
                var allowedPath = Path.GetFullPath(Path.Combine("plugins", "wasm", "storage", pluginId));
                if (!fullPath.StartsWith(allowedPath))
                {
                    var output = new StorageListOutput { Success = false, Error = "Path traversal not allowed" };
                    return WriteJson(caller, output);
                }

                if (!Directory.Exists(fullPath))
                {
                    var output = new StorageListOutput { Success = true, Files = Array.Empty<StorageFileInfo>() };
                    return WriteJson(caller, output);
                }

                var files = Directory.GetFiles(fullPath, "*", input.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                    .Select(f => new StorageFileInfo
                    {
                        Path = Path.GetRelativePath(Path.Combine("plugins", "wasm", "storage", pluginId), f),
                        Size = new FileInfo(f).Length,
                        ModifiedAt = File.GetLastWriteTimeUtc(f)
                    }).ToArray();

                var output = new StorageListOutput { Success = true, Files = files };
                return WriteJson(caller, output);
            }
        };
    }

    private static IEnumerable<HostFunction> CreateNetworkFunctions(IServiceProvider sp, ILogger logger)
    {
        yield return new HostFunction
        {
            Namespace = "kkbot",
            Name = "network_http",
            Description = "Make HTTP request",
            ParameterTypes = new[] { ValType.I32, ValType.I32 },
            ReturnTypes = new[] { ValType.I32, ValType.I32 },
            RequiredCapability = PluginCapability.NetworkHttp,
            Implementation = async (caller, args) =>
            {
                var inputPtr = (int)args[0].GetI32();
                var inputLen = (int)args[1].GetI32();
                var inputJson = ReadString(caller, inputPtr, inputLen);
                var input = JsonSerializer.Deserialize<NetworkHttpInput>(inputJson);

                // Only allow localhost if NetworkLocal capability
                var hasLocalOnly = ((PluginCapability)caller.GetContextData<ulong>("capabilities") & PluginCapability.NetworkLocal) != 0;
                if (hasLocalOnly && !IsLocalhost(input.Url))
                {
                    var output = new NetworkHttpOutput { Success = false, Error = "Only localhost allowed" };
                    return WriteJson(caller, output);
                }

                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(input.TimeoutSeconds ?? 30) };
                
                using var request = new HttpRequestMessage(HttpMethod.Parse(input.Method), input.Url);
                
                if (input.Headers != null)
                {
                    foreach (var header in input.Headers)
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                if (input.BodyBase64 != null)
                {
                    request.Content = new ByteArrayContent(Convert.FromBase64String(input.BodyBase64));
                    if (!string.IsNullOrEmpty(input.ContentType))
                        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(input.ContentType);
                }

                var response = await httpClient.SendAsync(request, CancellationToken.None);
                var responseBody = await response.Content.ReadAsByteArrayAsync();

                var output = new NetworkHttpOutput
                {
                    Success = response.IsSuccessStatusCode,
                    StatusCode = (int)response.StatusCode,
                    Headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)),
                    BodyBase64 = Convert.ToBase64String(responseBody)
                };
                return WriteJson(caller, output);
            }
        };
    }

    private static IEnumerable<HostFunction> CreateSystemFunctions(IServiceProvider sp, ILogger logger)
    {
        yield return new HostFunction
        {
            Namespace = "kkbot",
            Name = "system_time",
            Description = "Get current UTC timestamp",
            ParameterTypes = Array.Empty<ValType>(),
            ReturnTypes = new[] { ValType.I32, ValType.I32 },
            RequiredCapability = PluginCapability.SystemTime,
            Implementation = (caller, args) =>
            {
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var output = new SystemTimeOutput { TimestampMs = timestamp };
                return WriteJson(caller, output);
            }
        };

        yield return new HostFunction
        {
            Namespace = "kkbot",
            Name = "system_random",
            Description = "Generate cryptographically secure random bytes",
            ParameterTypes = new[] { ValType.I32 }, // length
            ReturnTypes = new[] { ValType.I32, ValType.I32 },
            RequiredCapability = PluginCapability.SystemRandom,
            Implementation = (caller, args) =>
            {
                var length = (int)args[0].GetI32();
                if (length <= 0 || length > 1024) length = 32;
                
                var bytes = new byte[length];
                using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
                rng.GetBytes(bytes);
                
                var output = new SystemRandomOutput { DataBase64 = Convert.ToBase64String(bytes) };
                return WriteJson(caller, output);
            }
        };

        yield return new HostFunction
        {
            Namespace = "kkbot",
            Name = "system_metrics",
            Description = "Record custom metric",
            ParameterTypes = new[] { ValType.I32, ValType.I32 },
            ReturnTypes = new[] { ValType.I32, ValType.I32 },
            RequiredCapability = PluginCapability.SystemLogging,
            Implementation = (caller, args) =>
            {
                var inputPtr = (int)args[0].GetI32();
                var inputLen = (int)args[1].GetI32();
                var inputJson = ReadString(caller, inputPtr, inputLen);
                var input = JsonSerializer.Deserialize<SystemMetricsInput>(inputJson);

                var metrics = sp.GetService<IMetricsRegistry>();
                if (metrics != null && double.TryParse(input.Value.ToString(), out var val))
                {
                    // Record custom metric - in real implementation, use metrics API
                    logger.LogDebug("Plugin metric: {Name} = {Value} ({Tags})", input.Name, val, JsonSerializer.Serialize(input.Tags));
                }

                var output = new SystemMetricsOutput { Success = true };
                return WriteJson(caller, output);
            }
        };
    }

    private static IEnumerable<HostFunction> CreateLoggingFunctions(IServiceProvider sp, ILogger logger)
    {
        yield return new HostFunction
        {
            Namespace = "kkbot",
            Name = "log_info",
            Description = "Log info message",
            ParameterTypes = new[] { ValType.I32, ValType.I32 },
            ReturnTypes = new[] { ValType.I32, ValType.I32 },
            RequiredCapability = PluginCapability.SystemLogging,
            Implementation = (caller, args) =>
            {
                var inputPtr = (int)args[0].GetI32();
                var inputLen = (int)args[1].GetI32();
                var inputJson = ReadString(caller, inputPtr, inputLen);
                var input = JsonSerializer.Deserialize<LogInput>(inputJson);

                logger.LogInformation("[Plugin:{PluginId}] {Message}", 
                    caller.GetContextData<string>("plugin_id"), input.Message);
                
                return WriteJson(caller, new LogOutput { Success = true });
            }
        };

        yield return new HostFunction
        {
            Namespace = "kkbot",
            Name = "log_warn",
            Description = "Log warning message",
            ParameterTypes = new[] { ValType.I32, ValType.I32 },
            ReturnTypes = new[] { ValType.I32, ValType.I32 },
            RequiredCapability = PluginCapability.SystemLogging,
            Implementation = (caller, args) =>
            {
                var inputPtr = (int)args[0].GetI32();
                var inputLen = (int)args[1].GetI32();
                var inputJson = ReadString(caller, inputPtr, inputLen);
                var input = JsonSerializer.Deserialize<LogInput>(inputJson);

                logger.LogWarning("[Plugin:{PluginId}] {Message}", 
                    caller.GetContextData<string>("plugin_id"), input.Message);
                
                return WriteJson(caller, new LogOutput { Success = true });
            }
        };

        yield return new HostFunction
        {
            Namespace = "kkbot",
            Name = "log_error",
            Description = "Log error message",
            ParameterTypes = new[] { ValType.I32, ValType.I32 },
            ReturnTypes = new[] { ValType.I32, ValType.I32 },
            RequiredCapability = PluginCapability.SystemLogging,
            Implementation = (caller, args) =>
            {
                var inputPtr = (int)args[0].GetI32();
                var inputLen = (int)args[1].GetI32();
                var inputJson = ReadString(caller, inputPtr, inputLen);
                var input = JsonSerializer.Deserialize<LogInput>(inputJson);

                logger.LogError("[Plugin:{PluginId}] {Message}", 
                    caller.GetContextData<string>("plugin_id"), input.Message);
                
                return WriteJson(caller, new LogOutput { Success = true });
            }
        };
    }

    // Helper methods
    private static string ReadString(Caller caller, int ptr, int len)
    {
        var memory = caller.GetMemory("memory") ?? throw new InvalidOperationException("No memory export");
        var span = memory.GetData(caller);
        var bytes = span.Slice(ptr, len).ToArray();
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    private static Val[] WriteJson(Caller caller, object obj)
    {
        var json = JsonSerializer.Serialize(obj);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var ptr = caller.GetContextData<Func<int, int>>("malloc")?.Invoke(bytes.Length) 
            ?? throw new InvalidOperationException("malloc not available");
        
        var memory = caller.GetMemory("memory") ?? throw new InvalidOperationException("No memory export");
        var memSpan = memory.GetData(caller);
        bytes.CopyTo(memSpan.Slice(ptr, bytes.Length));
        
        return new Val[] { Val.I32(ptr), Val.I32(bytes.Length) };
    }

    private static bool IsLocalhost(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host == "localhost" || uri.Host == "127.0.0.1" || uri.Host == "::1";
        }
        catch { return false; }
    }
}

// Input/Output DTOs for host functions
public sealed record DeviceCaptureInput(string DeviceId, RegionInput? Region, string? Format, int? Quality);
public sealed record RegionInput(int X, int Y, int Width, int Height);
public sealed record DeviceCaptureOutput(string FrameId, int Width, int Height, string Format, string DataBase64, double CaptureLatencyMs);

public sealed record DeviceInputInput(string DeviceId, string Type, int X, int Y, int EndX, int EndY, string? Text, int? DurationMs, int KeyCode);
public sealed record DeviceInputOutput(bool Success, string? ErrorMessage, double DurationMs);

public sealed record DeviceInfoInput(string DeviceId);
public sealed record DeviceInfoOutput(string DeviceId, string Model, string Manufacturer, string AndroidVersion, int ScreenWidth, int ScreenHeight, int ScreenDensity, int? BatteryLevel, bool? IsCharging);

public sealed record PerceptionDetectInput(string FrameBase64, int Width, int Height, string[]? TemplateNames);
public sealed record PerceptionDetectOutput(DetectionOutput[] Detections);
public sealed record DetectionOutput(string DetectorId, string DetectorName, double Confidence, bool IsMatch, string? DetectedValue, BoundingBoxOutput? BoundingBox);
public sealed record BoundingBoxOutput(int X, int Y, int Width, int Height);

public sealed record PerceptionOcrInput(string FrameBase64, int Width, int Height, int? X, int? Y, int? Width, int? Height);
public sealed record PerceptionOcrOutput(string Text, double Confidence, string? Error);

public sealed record AiGenerateInput(string Prompt, string? Model, double Temperature, int MaxTokens);
public sealed record AiGenerateOutput(string Content, int Tokens, int DurationMs);

public sealed record AiVisionInput(string ImageBase64, string Prompt, string? Model);
public sealed record AiVisionOutput(string Content, int Tokens, int DurationMs, string? Error);

public sealed record StorageReadInput(string Path);
public sealed record StorageReadOutput(bool Success, string? DataBase64, long Size, string? Error);
public sealed record StorageWriteInput(string Path, string DataBase64);
public sealed record StorageWriteOutput(bool Success, long Size, string? Error);
public sealed record StorageListInput(string? Prefix, bool Recursive);
public sealed record StorageListOutput(bool Success, StorageFileInfo[] Files, string? Error);
public sealed record StorageFileInfo(string Path, long Size, DateTime ModifiedAt);

public sealed record NetworkHttpInput(string Url, string Method, Dictionary<string, string>? Headers, string? BodyBase64, string? ContentType, int? TimeoutSeconds);
public sealed record NetworkHttpOutput(bool Success, int StatusCode, Dictionary<string, string>? Headers, string? BodyBase64, string? Error);

public sealed record SystemTimeOutput(long TimestampMs);
public sealed record SystemRandomOutput(string DataBase64);
public sealed record SystemMetricsInput(string Name, object Value, Dictionary<string, string>? Tags);
public sealed record SystemMetricsOutput(bool Success);
public sealed record LogInput(string Message, Dictionary<string, object>? Fields);
public sealed record LogOutput(bool Success);