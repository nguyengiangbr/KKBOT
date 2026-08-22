using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;

namespace KKBOT.Perception.OpenCv;

/// <summary>
/// Loads template images from the module's asset directory.
/// </summary>
public sealed class TemplateAssetLoader
{
    private readonly string _assetsRoot;
    private readonly ILogger<TemplateAssetLoader> _logger;

    public TemplateAssetLoader(string assetsRoot, ILogger<TemplateAssetLoader> logger)
    {
        _assetsRoot = assetsRoot;
        _logger = logger;
    }

    public IEnumerable<(string Name, byte[] Data)> LoadAll()
    {
        if (!Directory.Exists(_assetsRoot))
        {
            _logger.LogWarning("Template assets directory '{Root}' does not exist", _assetsRoot);
            yield break;
        }
        foreach (var file in Directory.EnumerateFiles(_assetsRoot, "*.png", SearchOption.AllDirectories))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var data = File.ReadAllBytes(file);
            yield return (name, data);
        }
    }
}