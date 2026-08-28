// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KKBOT.Plugins.Wpf;

/// <summary>
/// Base class for a game plugin's custom WPF configuration control.
/// Provides a consistent titled header so every game's UI looks familiar to the operator.
/// Derive from this in your plugin assembly and return it from <see cref="IPluginWpfUi.CreateConfigView"/>.
/// </summary>
public class GamePluginConfigControl : UserControl
{
    /// <summary>Gets the binder for reading/persisting this plugin's configuration.</summary>
    protected IConfigBinder Binder { get; }

    public GamePluginConfigControl(IConfigBinder binder, string title)
    {
        Binder = binder ?? throw new System.ArgumentNullException(nameof(binder));

        var dock = new DockPanel();
        var header = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            Padding = new Thickness(10),
            Child = new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
            },
        };
        DockPanel.SetDock(header, Dock.Top);
        dock.Children.Add(header);

        var body = new ScrollViewer
        {
            Padding = new Thickness(10),
            Content = BuildBody(),
        };
        dock.Children.Add(body);

        Content = dock;
    }

    /// <summary>Override to add game-specific controls into the body area.</summary>
    protected virtual UIElement BuildBody() => new TextBlock
    {
        Text = "This plugin has not provided a custom body. Use the schema editor instead.",
        Margin = new Thickness(0, 4, 0, 0),
    };
}
