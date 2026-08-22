// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using KKBOT.AI.Abstractions;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Threading;

namespace KKBOT.AI.Ollama;

public sealed class OllamaVisionService : IAiVisionService
{
    private readonly ILogger<OllamaVisionService> _logger;
    public OllamaVisionService(ILogger<OllamaVisionService> logger) => _logger = logger;
    public Task<AiResponse> GenerateAsync(byte[] imageData, string prompt, CancellationToken cancellationToken)
    {
        _logger.LogInformation("OllamaVisionService received prompt: {Prompt}", prompt);
        var response = new AiResponse($"Vision Echo: {prompt}", new AiUsage(0,0));
        return Task.FromResult(response);
    }
}