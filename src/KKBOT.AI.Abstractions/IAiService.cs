// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

namespace KKBOT.AI.Abstractions;

public interface IAiService
{
    Task<AiResponse> GenerateAsync(string prompt, CancellationToken cancellationToken);
}

public sealed record AiResponse(string Content, AiUsage Usage);

public sealed record AiUsage(int Tokens, int DurationMs);