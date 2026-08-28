// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Text.Json;
using KKBOT.Plugins.Abstractions;

namespace KKBOT.Plugins.Wpf;

/// <summary>
/// File-backed <see cref="IConfigBinder"/>. Stores configuration under
/// <c>plugins/&lt;pluginId&gt;.config.json</c> and falls back to the manifest default.
/// </summary>
public sealed class PluginConfigBinder : IConfigBinder
{
    private readonly ModuleManifest _manifest;
    private readonly string _configFilePath;
    private string _currentJson;

    public PluginConfigBinder(ModuleManifest manifest, string pluginDirectory)
    {
        _manifest = manifest ?? throw new System.ArgumentNullException(nameof(manifest));
        var directory = pluginDirectory ?? System.IO.Directory.GetCurrentDirectory();
        _configFilePath = Path.Combine(directory, $"{manifest.Id}.config.json");
        _currentJson = LoadOrDefault();
    }

    public string GetCurrentJson() => _currentJson;

    public string GetDefaultJson() => _manifest.DefaultConfig;

    public void ApplyJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        // Validate it is at least legal JSON before persisting.
        using var _ = JsonDocument.Parse(json);
        _currentJson = json;
        File.WriteAllText(_configFilePath, json);
    }

    public T? Bind<T>() where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(_currentJson);
        }
        catch (JsonException)
        {
            return JsonSerializer.Deserialize<T>(_manifest.DefaultConfig);
        }
    }

    private string LoadOrDefault()
    {
        if (File.Exists(_configFilePath))
        {
            return File.ReadAllText(_configFilePath);
        }

        return _manifest.DefaultConfig;
    }
}
