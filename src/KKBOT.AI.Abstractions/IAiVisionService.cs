// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

namespace KKBOT.AI.Abstractions;

public interface IAiVisionService
{
    Task<AiResponse> GenerateAsync(byte[] imageData, string prompt, CancellationToken cancellationToken);
}