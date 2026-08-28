// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Windows;
using KKBOT.ControlCenter.Wpf.ViewModels;
using KKBOT.Navigation;
using KKBOT.Plugins.Abstractions;
using KKBOT.Plugins.Loader;
using KKBOT.Plugins.Wpf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KKBOT.ControlCenter.Wpf;

/// <summary>
/// Application entry point. Builds the host composition root, loads plugins, and shows the shell.
/// </summary>
public partial class App : System.Windows.Application
{
    private IServiceProvider _serviceProvider = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddKkbotNavigation();
        services.AddPluginLoader(configuration);

        _serviceProvider = services.BuildServiceProvider();

        var pluginLoader = _serviceProvider.GetRequiredService<PluginLoader>();
        pluginLoader.LoadAllPluginsAsync().GetAwaiter().GetResult();

        var entries = new System.Collections.ObjectModel.ObservableCollection<PluginEntry>();
        foreach (var module in pluginLoader.GetLoadedPluginInstances())
        {
            var binder = ActivatorUtilities.CreateInstance<PluginConfigBinder>(_serviceProvider, module);
            entries.Add(new PluginEntry(module, binder));
        }

        var viewModel = new ControlCenterViewModel(entries);

        var mainWindow = new MainWindow
        {
            DataContext = viewModel,
        };
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
