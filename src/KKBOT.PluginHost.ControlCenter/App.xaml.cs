// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using KKBOT.PluginHost;
using KKBOT.PluginHost.Loader;
using KKBOT.PluginHost.Navigation;
using KKBOT.PluginHost.Wpf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KKBOT.PluginHost.ControlCenter;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var baseDir = AppContext.BaseDirectory;
        var pluginDir = Path.Combine(baseDir, "plugins");
        Directory.CreateDirectory(pluginDir);

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());
        services.AddKkbotNavigation();
        var provider = services.BuildServiceProvider();

        var loader = new PluginLoader(
            provider.GetRequiredService<ILogger<PluginLoader>>(),
            provider,
            pluginDir);
        var modules = loader.LoadAll();

        var entries = new ObservableCollection<PluginEntry>();
        foreach (var m in modules)
        {
            var binder = new PluginConfigBinder(m, baseDir);
            entries.Add(new PluginEntry(m, binder));
        }

        var vm = new ControlCenterViewModel(entries);
        var window = new MainWindow { DataContext = vm };
        MainWindow = window;
        window.Show();
    }
}

public sealed class PluginEntry
{
    public PluginEntry(IGameModule module, IConfigBinder binder)
    {
        Module = module;
        Binder = binder;
        DisplayName = module.GetManifest().Name;
    }

    public IGameModule Module { get; }
    public IConfigBinder Binder { get; }
    public string DisplayName { get; }
}

public sealed class ControlCenterViewModel : INotifyPropertyChanged
{
    private readonly ObservableCollection<PluginEntry> _plugins;
    private PluginEntry? _selected;
    private object? _view;

    public ControlCenterViewModel(ObservableCollection<PluginEntry> plugins) => _plugins = plugins;

    public ReadOnlyObservableCollection<PluginEntry> Plugins => new(_plugins);

    public PluginEntry? SelectedPlugin
    {
        get => _selected;
        set
        {
            if (!ReferenceEquals(_selected, value))
            {
                _selected = value;
                OnPropertyChanged();
                _view = value is null ? null : PluginWpfExtensions.BuildConfigView(value.Module, value.Binder);
                OnPropertyChanged(nameof(SelectedView));
            }
        }
    }

    public object? SelectedView => _view;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
