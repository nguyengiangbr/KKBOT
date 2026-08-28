// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Windows;
using System.Windows.Controls;

namespace KKBOT.PluginHost.Wpf;

/// <summary>
/// Base <see cref="UserControl"/> for plugin-provided configuration surfaces. Exposes the
/// module manifest and a config binder so derived controls can persist settings.
/// </summary>
public class GamePluginConfigControl : UserControl
{
    public GamePluginConfigControl(ModuleManifest manifest, IConfigBinder binder)
    {
        Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        Binder = binder ?? throw new ArgumentNullException(nameof(binder));
    }

    public ModuleManifest Manifest { get; }

    public IConfigBinder Binder { get; }
}
