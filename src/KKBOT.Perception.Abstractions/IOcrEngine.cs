// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

namespace KKBOT.Perception.Abstractions;

public interface IOcrEngine
{
    Task<OcrResult> RecognizeAsync(byte[] imageData, CancellationToken cancellationToken);
}

public sealed record OcrResult(string Text, double Confidence);