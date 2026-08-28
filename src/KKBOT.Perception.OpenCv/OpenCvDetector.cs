using KKBOT.Perception.Abstractions;
using KKBOT.Device.Abstractions;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace KKBOT.Perception.OpenCv;

/// <summary>
/// A game detector implementation using OpenCV template matching.
/// </summary>
public sealed class OpenCvDetector : IGameDetector
{
    private readonly ImageMatcher _matcher;
    private readonly ILogger<OpenCvDetector> _logger;

    public OpenCvDetector(ImageMatcher matcher, ILogger<OpenCvDetector> logger)
    {
        _matcher = matcher;
        _logger = logger;
    }

    public string Id => "opencv-detector";
    public string Name => "OpenCV Template Detector";
    public double RequiredConfidence => 0.85;
    public async Task<DetectorResult> DetectAsync(Frame frame, CancellationToken cancellationToken)
    {
        // Convert frame to Mat
        using var mat = Mat.FromImageData(frame.Data, ImreadModes.Color);
        var match = await _matcher.MatchAsync(mat, cancellationToken);
        var result = new DetectorResult
        {
            DetectorId = Id,
            DetectorName = Name,
            Confidence = match.Confidence,
            IsMatch = match.Confidence >= RequiredConfidence,
            DetectedValue = match.TemplateName,
            RawScore = match.Score
        };
        _logger.LogDebug("Detector {Id} produced confidence {Confidence:P2}", Id, result.Confidence);
        return result;
    }
}
