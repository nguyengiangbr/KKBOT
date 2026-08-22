using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace KKBOT.Perception.OpenCv;

/// <summary>
/// Performs image/template matching using OpenCV.
/// </summary>
public sealed class ImageMatcher
{
    private readonly TemplateAssetLoader _loader;
    private readonly ILogger<ImageMatcher> _logger;
    private readonly List<Mat> _templates = new();
    private readonly List<string> _templateNames = new();

    public ImageMatcher(TemplateAssetLoader loader, ILogger<ImageMatcher> logger)
    {
        _loader = loader;
        _logger = logger;
        LoadTemplates();
    }

    private void LoadTemplates()
    {
        foreach (var (name, data) in _loader.LoadAll())
        {
            var mat = Mat.FromImageData(data, ImreadModes.Grayscale);
            _templates.Add(mat);
            _templateNames.Add(name);
            _logger.LogInformation("Loaded template '{Name}' ({Size} bytes)", name, data.Length);
        }
    }

    public Task<MatchResult> MatchAsync(Mat source, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            double bestScore = double.MinValue;
            string bestTemplate = string.Empty;
            foreach (var (template, name) in System.Linq.Enumerable.Zip(_templates, _templateNames))
            {
                using var result = new Mat();
                Cv2.MatchTemplate(source, template, result, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out _);
                if (maxVal > bestScore)
                {
                    bestScore = maxVal;
                    bestTemplate = name;
                }
            }
            return new MatchResult { TemplateName = bestTemplate, Confidence = bestScore, Score = bestScore };
        }, cancellationToken);
    }
}

public sealed class MatchResult
{
    public string TemplateName { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public double Score { get; set; }
}