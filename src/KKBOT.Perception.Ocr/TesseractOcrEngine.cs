using KKBOT.Perception.Abstractions;
using Microsoft.Extensions.Logging;
using Tesseract;
using System.Threading.Tasks;
using System.Threading;

namespace KKBOT.Perception.Ocr;

/// <summary>
/// OCR engine using Tesseract.
/// </summary>
public sealed class TesseractOcrEngine : IOcrEngine
{
    private readonly ILogger<TesseractOcrEngine> _logger;
    private readonly TesseractEngine _engine;

    public TesseractOcrEngine(ILogger<TesseractOcrEngine> logger, string dataPath = @"tessdata", string language = "eng")
    {
        _logger = logger;
        _engine = new TesseractEngine(dataPath, language, EngineMode.Default);
    }

    public async Task<OcrResult> RecognizeAsync(byte[] imageData, CancellationToken cancellationToken)
    {
        using var pix = Pix.LoadFromMemory(imageData);
        using var page = _engine.Process(pix);
        var text = page.GetText();
        var confidence = page.GetMeanConfidence();
        var result = new OcrResult { Text = text, Confidence = confidence };
        _logger.LogDebug("OCR recognized text with confidence {Confidence:P2}", confidence);
        return await Task.FromResult(result);
    }
}