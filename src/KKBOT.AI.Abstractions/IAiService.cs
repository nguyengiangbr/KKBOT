// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace KKBOT.AI.Abstractions;

public interface IAiService
{
    Task<AiResponse> GenerateAsync(string prompt, CancellationToken cancellationToken);
    
    /// <summary>
    /// Generates a streaming response from the AI model.
    /// </summary>
    /// <param name="prompt">The prompt to send to the model.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of streaming chunks.</returns>
    IAsyncEnumerable<AiStreamChunk> GenerateStreamAsync(string prompt, CancellationToken cancellationToken);
}

public sealed record AiResponse(string Content, AiUsage Usage);

public sealed record AiUsage(int Tokens, int DurationMs);

/// <summary>
/// Represents a chunk of streaming AI response.
/// </summary>
public sealed record AiStreamChunk(
    string Content,
    bool IsFinal,
    AiUsage? Usage = null,
    string? FinishReason = null,
    Dictionary<string, object>? Metadata = null)
{
    public static AiStreamChunk Final(string content, AiUsage usage, string? finishReason = null) 
        => new(content, true, usage, finishReason);
    
    public static AiStreamChunk Chunk(string content, Dictionary<string, object>? metadata = null) 
        => new(content, false, null, null, metadata);
}