// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using KKBOT.AI.Abstractions;
using KKBOT.AI.Ollama.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KKBOT.AI.Ollama;

/// <summary>
/// Service that calls the Ollama HTTP API for vision (multimodal) generation.
/// </summary>
public sealed class OllamaVisionService : IAiVisionService
{
    private readonly HttpClient _client;
    private readonly ILogger<OllamaVisionService> _logger;
    private readonly OllamaOptions _options;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public OllamaVisionService(HttpClient client, IOptions<OllamaOptions> options, ILogger<OllamaVisionService> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AiResponse> GenerateAsync(byte[] imageData, string prompt, CancellationToken cancellationToken)
    {
        _logger.LogInformation("OllamaVisionService generating vision response for prompt: {Prompt}", prompt);
        
        var base64Image = Convert.ToBase64String(imageData);
        var request = new OllamaVisionRequest
        {
            Model = _options.VisionModel,
            Prompt = prompt,
            ImageBase64 = base64Image,
            Options = _options.DefaultParams,
            Stream = false
        };

        var content = new StringContent(JsonSerializer.Serialize(request, _jsonOptions), Encoding.UTF8, "application/json");
        var url = _options.BaseUrl.TrimEnd('/') + "/api/generate";
        var response = await _client.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(json, _jsonOptions) ?? throw new InvalidOperationException("Failed to deserialize Ollama vision response.");

        var usage = new AiUsage(ollamaResponse.EvalCount + ollamaResponse.PromptEvalCount, (int)ollamaResponse.TotalDuration);
        return new AiResponse(ollamaResponse.Response, usage);
    }

    public async IAsyncEnumerable<AiStreamChunk> GenerateStreamAsync(byte[] imageData, string prompt, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogInformation("OllamaVisionService generating streaming vision response for prompt: {Prompt}", prompt);
        
        var base64Image = Convert.ToBase64String(imageData);
        var request = new OllamaVisionRequest
        {
            Model = _options.VisionModel,
            Prompt = prompt,
            ImageBase64 = base64Image,
            Options = _options.DefaultParams,
            Stream = true
        };

        var content = new StringContent(JsonSerializer.Serialize(request, _jsonOptions), Encoding.UTF8, "application/json");
        var url = _options.BaseUrl.TrimEnd('/') + "/api/generate";
        
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        using var response = await _client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new System.IO.StreamReader(stream);
        
        string? fullContent = null;
        AiUsage? finalUsage = null;
        string? finishReason = null;

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var streamResponse = JsonSerializer.Deserialize<OllamaStreamResponse>(line, _jsonOptions);
                if (streamResponse == null) continue;

                if (!string.IsNullOrEmpty(streamResponse.Response))
                {
                    fullContent = (fullContent ?? "") + streamResponse.Response;
                    yield return new AiStreamChunk(streamResponse.Response, false);
                }

                if (streamResponse.Done)
                {
                    finalUsage = new AiUsage(streamResponse.EvalCount + streamResponse.PromptEvalCount, (int)streamResponse.TotalDuration);
                    finishReason = "stop";
                    yield return AiStreamChunk.Final(fullContent ?? "", finalUsage, finishReason);
                    break;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse Ollama vision stream response: {Line}", line);
            }
        }
    }
}

/// <summary>
/// Vision request for Ollama API.
/// </summary>
internal sealed class OllamaVisionRequest
{
    public string Model { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string ImageBase64 { get; set; } = string.Empty;
    public OllamaRequestParameters Options { get; set; } = new();
    public bool Stream { get; set; } = false;
}

/// <summary>
/// Streaming response from Ollama API.
/// </summary>
internal sealed class OllamaStreamResponse
{
    public string Model { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public bool Done { get; set; }
    public int EvalCount { get; set; }
    public int PromptEvalCount { get; set; }
    public long TotalDuration { get; set; }
}