// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text.Json;

namespace KKBOT.PluginHost.Wpf;

/// <summary>
/// Persists a plugin's configuration (round-trips JSON against the host config store).
/// </summary>
public interface IConfigBinder
{
    string GetCurrentJson();

    void ApplyJson(string json);
}

/// <summary>
/// File-backed <see cref="IConfigBinder"/>. Stores the plugin config as JSON on disk
/// under the host's configuration directory.
/// </summary>
public sealed class PluginConfigBinder : IConfigBinder
{
    private readonly IGameModule _module;
    private readonly string _filePath;

    public PluginConfigBinder(IGameModule module, string baseDirectory)
    {
        _module = module ?? throw new ArgumentNullException(nameof(module));
        _filePath = System.IO.Path.Combine(
            baseDirectory,
            "plugins",
            "config",
            $"{module.Id}.json");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_filePath)!);
    }

    public string GetCurrentJson()
    {
        if (System.IO.File.Exists(_filePath))
        {
            return System.IO.File.ReadAllText(_filePath);
        }

        return _module.GetManifest().DefaultConfig ?? "{}";
    }

    public void ApplyJson(string json)
    {
        JsonDocument.Parse(json); // validate
        System.IO.File.WriteAllText(_filePath, json);
    }
}
