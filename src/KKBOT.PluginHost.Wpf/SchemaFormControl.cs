// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KKBOT.PluginHost.Wpf;

/// <summary>
/// Schema-driven fallback UI shown by the host when a plugin does not supply a custom
/// <see cref="IPluginWpfUi"/> control. Edits the plugin config as JSON.
/// </summary>
public sealed class SchemaFormControl : UserControl
{
    private readonly IConfigBinder _binder;
    private readonly TextBox _editor;
    private readonly TextBlock _status;

    public SchemaFormControl(ModuleManifest manifest, IConfigBinder binder)
    {
        _binder = binder ?? throw new ArgumentNullException(nameof(binder));

        var dock = new DockPanel();

        var title = new TextBlock
        {
            Text = $"Configuration for {manifest.Name}",
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 6),
        };
        DockPanel.SetDock(title, Dock.Top);
        dock.Children.Add(title);

        _status = new TextBlock
        {
            Margin = new Thickness(0, 6, 0, 0),
            Foreground = Brushes.Gray,
        };
        DockPanel.SetDock(_status, Dock.Bottom);

        var apply = new Button
        {
            Content = "Apply",
            Width = 80,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 0),
        };
        apply.Click += OnApply;
        DockPanel.SetDock(apply, Dock.Bottom);

        _editor = new TextBox
        {
            AcceptsReturn = true,
            FontFamily = new FontFamily("Consolas"),
            Text = _binder.GetCurrentJson(),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        dock.Children.Add(new Border
        {
            BorderBrush = Brushes.DimGray,
            BorderThickness = new Thickness(1),
            Child = _editor,
        });

        Content = dock;
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        try
        {
            _binder.ApplyJson(_editor.Text);
            _status.Text = "Saved.";
            _status.Foreground = Brushes.LightGreen;
        }
        catch (Exception ex)
        {
            _status.Text = $"Invalid JSON: {ex.Message}";
            _status.Foreground = Brushes.OrangeRed;
        }
    }
}
