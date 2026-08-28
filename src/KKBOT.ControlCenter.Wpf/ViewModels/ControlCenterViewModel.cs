// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using KKBOT.Plugins.Wpf;

namespace KKBOT.ControlCenter.Wpf.ViewModels;

/// <summary>
/// Shell view model. Hosts the list of discovered plugins and the WPF control for the selected one.
/// </summary>
public sealed class ControlCenterViewModel : INotifyPropertyChanged
{
    private readonly ObservableCollection<PluginEntry> _plugins;
    private PluginEntry? _selectedPlugin;
    private object? _selectedView;

    public ControlCenterViewModel(ObservableCollection<PluginEntry> plugins)
    {
        _plugins = plugins ?? throw new System.ArgumentNullException(nameof(plugins));
    }

    public ReadOnlyObservableCollection<PluginEntry> Plugins => new(_plugins);

    public PluginEntry? SelectedPlugin
    {
        get => _selectedPlugin;
        set
        {
            if (!ReferenceEquals(_selectedPlugin, value))
            {
                _selectedPlugin = value;
                OnPropertyChanged();
                SelectedView = value is null
                    ? null
                    : PluginWpfExtensions.BuildConfigView(value.Module, value.Binder);
            }
        }
    }

    public object? SelectedView
    {
        get => _selectedView;
        private set
        {
            if (!ReferenceEquals(_selectedView, value))
            {
                _selectedView = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
