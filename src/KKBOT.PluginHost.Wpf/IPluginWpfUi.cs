// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Windows;

namespace KKBOT.PluginHost.Wpf;

/// <summary>
/// Implemented by plugins that supply a custom WPF configuration surface.
/// When not implemented, the host falls back to a schema-driven auto-generated form.
/// </summary>
public interface IPluginWpfUi
{
    FrameworkElement? CreateConfigView(ModuleManifest manifest, IConfigBinder binder);
}
