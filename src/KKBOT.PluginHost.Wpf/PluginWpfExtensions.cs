// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Windows;
using KKBOT.PluginHost;

namespace KKBOT.PluginHost.Wpf;

/// <summary>
/// Resolves the appropriate WPF control for a plugin: a custom <see cref="IPluginWpfUi"/> view
/// when available, otherwise the schema-driven <see cref="SchemaFormControl"/> fallback.
/// </summary>
public static class PluginWpfExtensions
{
    public static FrameworkElement BuildConfigView(IGameModule plugin, IConfigBinder binder)
    {
        if (plugin is null)
        {
            throw new ArgumentNullException(nameof(plugin));
        }

        if (binder is null)
        {
            throw new ArgumentNullException(nameof(binder));
        }

        var manifest = plugin.GetManifest();
        if (plugin is IPluginWpfUi ui && ui.CreateConfigView(manifest, binder) is { } custom)
        {
            return custom;
        }

        return new SchemaFormControl(manifest, binder);
    }
}
