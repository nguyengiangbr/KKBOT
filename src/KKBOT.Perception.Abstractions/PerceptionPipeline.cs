// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using KKBOT.Device.Abstractions;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Perception.Abstractions;
using Microsoft.Extensions.Logging;

namespace KKBOT.Perception.Abstractions;

/// <summary>
/// Concrete implementation of <see cref="IPerceptionPipeline"/> that runs a collection of detectors
/// and optionally an OCR engine.
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
                _logger.LogDebug("Detector {Id} confidence {Conf:F3} match={Match}", result.DetectorId, result.Confidence, result.IsMatch);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Detector {Id} threw an exception.", detector.Id);
            }
        }

        if (_ocrEngine != null)
        {
            try
            {
                var ocr = await _ocrEngine.RecognizeAsync(frame.Data, cancellationToken);
                _logger.LogInformation("OCR result: '{Text}' (confidence {Conf:P})", ocr.Text, ocr.Confidence);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OCR engine failed.");
            }
        }

        return results;
    }
}
