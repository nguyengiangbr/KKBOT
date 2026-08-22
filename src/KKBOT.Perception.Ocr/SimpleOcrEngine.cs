// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using KKBOT.Perception.Abstractions;
using Microsoft.Extensions.Logging;

namespace KKBOT.Perception.Ocr;

/// <summary>
/// Very simple OCR engine that pretends to recognise text.
/// For demonstration purposes it just returns a fixed placeholder text.
/// In a real implementation you would hook into Tesseract or another OCR library.
/// </summary>
public sealed class SimpleOcrEngine : IOcrEngine
{
    private readonly ILogger<SimpleOcrEngine> _logger;
    private readonly string _placeholder = "placeholder";
    private readonly double _confidence = 1.0;

    public SimpleOcrEngine(ILogger<SimpleOcrEngine> logger)
    {
        _logger = logger;
    }

    public Task<OcrResult> RecognizeAsync(byte[] imageData, CancellationToken cancellationToken)
    {
        // No real OCR – just log and return placeholder.
        _logger.LogDebug("SimpleOcrEngine invoked – returning placeholder result.");
        return Task.FromResult(new OcrResult(_placeholder, _confidence));
    }
}
