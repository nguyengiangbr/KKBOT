// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using KKBOT.Device.Abstractions;
using KKBOT.Perception.Abstractions;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace KKBOT.Perception.OpenCv;

/// <summary>
/// Real OpenCV detector that performs template matching against a set of pre‑loaded templates.
/// The detector looks for the best match among all templates and returns a <see cref="DetectorResult"/>.
/// </summary>
public sealed class OpenCvGameDetector : IGameDetector
{
    private readonly string _templatesPath;
    private readonly ILogger<OpenCvGameDetector> _logger;
    private readonly Dictionary<string, Mat> _templates = new(); // key = template name (filename without extension)
    private readonly double _requiredConfidence;

    public OpenCvGameDetector(string templatesPath, double requiredConfidence, ILogger<OpenCvGameDetector> logger)
    {
        _templatesPath = templatesPath;
        _requiredConfidence = requiredConfidence;
        _logger = logger;
        LoadTemplates();
    }

    private void LoadTemplates()
    {
        if (!Directory.Exists(_templatesPath))
        {
            _logger.LogWarning("Templates path '{Path}' does not exist – detector will never match.", _templatesPath);
            return;
        }

        var files = Directory.GetFiles(_templatesPath, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
        foreach (var file in files)
        {
            try
            {
                var mat = Cv2.ImRead(file, ImreadModes.Grayscale);
                if (mat.Empty())
                {
                    _logger.LogWarning("Failed to load template image '{File}'. Image is empty.", file);
                    continue;
                }
                var name = Path.GetFileNameWithoutExtension(file);
                _templates[name] = mat;
                _logger.LogDebug("Loaded template '{Name}' ({Width}x{Height})", name, mat.Width, mat.Height);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading template '{File}'", file);
            }
        }
    }

    public string Id => "opencv-template-matcher";
    public string Name => "OpenCV Template Matcher";
    public double RequiredConfidence => _requiredConfidence;

    public async Task<DetectorResult> DetectAsync(Frame frame, CancellationToken cancellationToken)
    {
        // Decode frame bytes to Mat (grayscale for matching)
        using var frameMat = Cv2.ImDecode(frame.Data, ImreadModes.Grayscale);
        if (frameMat.Empty())
        {
            _logger.LogWarning("Received empty frame for detection.");
            return new DetectorResult
            {
                DetectorId = Id,
                DetectorName = Name,
                Confidence = 0,
                IsMatch = false,
                DetectedValue = null,
                RawScore = 0
            };
        }

        double bestScore = 0;
        string? bestTemplate = null;
        foreach (var kvp in _templates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tmpl = kvp.Value;
            // Template must be smaller than source
            if (tmpl.Width > frameMat.Width || tmpl.Height > frameMat.Height)
                continue;

            using var result = new Mat();
            Cv2.MatchTemplate(frameMat, tmpl, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out _);

            if (maxVal > bestScore)
            {
                bestScore = maxVal;
                bestTemplate = kvp.Key;
            }
        }

        var isMatch = bestScore >= _requiredConfidence;
        _logger.LogDebug("Detection completed. Best template: {Template}, Score: {Score:F3}, Required: {Req}",
            bestTemplate ?? "<none>", bestScore, _requiredConfidence);

        return new DetectorResult
        {
            DetectorId = Id,
            DetectorName = Name,
            Confidence = bestScore,
            IsMatch = isMatch,
            DetectedValue = bestTemplate,
            RawScore = bestScore
        };
    }
}
