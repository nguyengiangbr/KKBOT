// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace KKBOT.PluginHost;

/// <summary>
/// Minimal runtime context exposed to a plugin. Wraps the host service provider,
/// a JSON-backed configuration store, and the navigation service.
/// </summary>
public sealed class GameContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, object> _config = new();

    public GameContext(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public IServiceProvider Services => _serviceProvider;

    public INavigationService Navigation =>
        _serviceProvider.GetRequiredService<INavigationService>();

    public IDeviceSession? Device =>
        _serviceProvider.GetService<IDeviceSession>();

    public TConfig GetConfig<TConfig>() where TConfig : class, new()
    {
        var json = _config.TryGetValue("json", out var j) ? (string)j : "{}";
        return JsonSerializer.Deserialize<TConfig>(json) ?? new TConfig();
    }

    public void SetConfigJson(string json) => _config["json"] = json;

    public T? GetService<T>() where T : class => _serviceProvider.GetService<T>();
}
