// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using KKBOT.Plugins.Abstractions;
using KKBOT.Plugins.Wpf;

namespace KKBOT.ControlCenter.Wpf.ViewModels;

/// <summary>
/// Binds a loaded plugin module to its configuration binder for the operator UI.
/// </summary>
public sealed class PluginEntry
{
    public PluginEntry(IGameModule module, IConfigBinder binder)
    {
        Module = module ?? throw new System.ArgumentNullException(nameof(module));
        Binder = binder ?? throw new System.ArgumentNullException(nameof(binder));
        DisplayName = module.GetManifest().Name;
    }

    public IGameModule Module { get; }

    public IConfigBinder Binder { get; }

    public string DisplayName { get; }
}
