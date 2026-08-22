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
/// Service that calls the Ollama HTTP API to generate text.
/// </summary>
public sealed class OllamaService : IAiService
{
    private readonly HttpClient _client;
    private readonly ILogger<OllamaService> _logger;
    private readonly OllamaOptions _options;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public OllamaService(HttpClient client, IOptions<OllamaOptions> options, ILogger<OllamaService> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AiResponse> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        _logger.LogInformation("OllamaService generating text for prompt: {Prompt}", prompt);
        var request = new OllamaRequest
        {
            Model = _options.Model,
            Prompt = prompt,
            Options = _options.DefaultParams
        };

        var content = new StringContent(JsonSerializer.Serialize(request, _jsonOptions), Encoding.UTF8, "application/json");
        var url = _options.BaseUrl.TrimEnd('/') + "/api/generate";
        var response = await _client.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(json, _jsonOptions) ?? throw new InvalidOperationException("Failed to deserialize Ollama response.");

        var usage = new AiUsage(ollamaResponse.EvalCount + ollamaResponse.PromptEvalCount, (int)ollamaResponse.TotalDuration);
        return new AiResponse(ollamaResponse.Response, usage);
    }
}
