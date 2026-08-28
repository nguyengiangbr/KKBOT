// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Windows;
using KKBOT.Plugins.Abstractions;

namespace KKBOT.Plugins.Wpf;

/// <summary>
/// Helpers for hosting a plugin's WPF configuration UI inside the operator application.
/// </summary>
public static class PluginWpfExtensions
{
    /// <summary>
    /// Resolves the appropriate WPF control for <paramref name="plugin"/>:
    /// a custom <see cref="IPluginWpfUi"/> view when available, otherwise the
    /// schema-driven <see cref="SchemaFormControl"/> fallback.
    /// </summary>
    public static FrameworkElement BuildConfigView(IGameModule plugin, IConfigBinder binder)
    {
        if (plugin is null)
        {
            throw new System.ArgumentNullException(nameof(plugin));
        }

        if (binder is null)
        {
            throw new System.ArgumentNullException(nameof(binder));
        }

        var manifest = plugin.GetManifest();
        if (plugin is IPluginWpfUi ui && ui.CreateConfigView(manifest, binder) is { } custom)
        {
            return custom;
        }

        return new SchemaFormControl(manifest, binder);
    }
}
