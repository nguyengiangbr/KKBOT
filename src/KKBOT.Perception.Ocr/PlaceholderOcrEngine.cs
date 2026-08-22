// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using KKBOT.Perception.Abstractions;
using System.Threading.Tasks;
using System.Threading;

namespace KKBOT.Perception.Ocr;

public sealed class PlaceholderOcrEngine : IOcrEngine
{
    public Task<OcrResult> RecognizeAsync(byte[] imageData, CancellationToken cancellationToken) => Task.FromResult(new OcrResult("placeholder", 1.0));
}