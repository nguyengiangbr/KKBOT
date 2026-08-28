// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

namespace KKBOT.Plugins.Wpf;

/// <summary>
/// Two-way binder between a plugin's persisted configuration (JSON) and its UI.
/// The host provides one instance per plugin/instance pair.
/// </summary>
public interface IConfigBinder
{
    /// <summary>The current persisted configuration as a JSON string.</summary>
    string GetCurrentJson();

    /// <summary>The plugin manifest's default configuration as a JSON string.</summary>
    string GetDefaultJson();

    /// <summary>Persists the supplied JSON configuration.</summary>
    void ApplyJson(string json);

    /// <summary>Deserializes the current JSON into <typeparamref name="T"/>, or returns <c>default</c>.</summary>
    T? Bind<T>() where T : class;
}
