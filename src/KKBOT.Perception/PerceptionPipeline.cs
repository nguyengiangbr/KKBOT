// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using KKBOT.Domain.Models.Perception;
using KKBOT.Perception.Abstractions;
using Microsoft.Extensions.Logging;

namespace KKBOT.Perception;

/// <summary>
/// Implements the perception pipeline: runs a list of detectors on a frame, optionally runs OCR, and returns all detector results.
/// </summary>
public sealed class PerceptionPipeline : IPerceptionPipeline
{
    private readonly IEnumerable<IGameDetector> _detectors;
    private readonly IOcrEngine? _ocrEngine;
    private readonly ILogger<PerceptionPipeline> _logger;

    public PerceptionPipeline(IEnumerable<IGameDetector> detectors, IOcrEngine? ocrEngine, ILogger<PerceptionPipeline> logger)
    {
        _detectors = detectors;
        _ocrEngine = ocrEngine;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DetectorResult>> ProcessAsync(Frame frame, CancellationToken cancellationToken)
    {
        var results = new List<DetectorResult>();
        foreach (var detector in _detectors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = await detector.DetectAsync(frame, cancellationToken);
                results.Add(result);
                _logger.LogDebug("Detector {Id} returned confidence {Conf:F3}, match={IsMatch}",
                    result.DetectorId, result.Confidence, result.IsMatch);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Detector {Id} threw an exception.", detector.Id);
                // Add a failed result if needed – here we simply skip.
            }
        }

        // If OCR is required by any detector (could be made smarter), run it and log.
        if (_ocrEngine != null)
        {
            try
            {
                var ocrResult = await _ocrEngine.RecognizeAsync(frame.Data, cancellationToken);
                _logger.LogInformation("OCR result: '{Text}' (confidence {Conf:P})", ocrResult.Text, ocrResult.Confidence);
                // OCR result could be merged into detector results – omitted for brevity.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OCR engine failed.");
            }
        }

        return results;
    }
}
