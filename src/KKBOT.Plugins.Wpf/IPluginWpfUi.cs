// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Windows;
using System.Windows.Controls;
using KKBOT.Plugins.Abstractions;

namespace KKBOT.Plugins.Wpf;

/// <summary>
/// Contract allowing a game plugin to supply its own WPF configuration UI.
/// Return <c>null</c> from <see cref="CreateConfigView"/> to let the host auto-generate
/// a form from the plugin's <see cref="ModuleManifest.ConfigSchema"/>.
/// </summary>
public interface IPluginWpfUi
{
    /// <summary>
    /// Builds the WPF control used to configure this plugin for a given instance/module.
    /// Return <c>null</c> to fall back to the schema-driven auto form.
    /// </summary>
    /// <param name="manifest">The plugin manifest (id, name, default config).</param>
    /// <param name="binder">Binder used to read/persist configuration as JSON.</param>
    FrameworkElement? CreateConfigView(ModuleManifest manifest, IConfigBinder binder);
}
