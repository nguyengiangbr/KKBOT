// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using KKBOT.PluginHost;
using KKBOT.PluginHost.Wpf;
using Microsoft.Extensions.Logging;

namespace KKBOT.PluginHost.Sample;

/// <summary>
/// Demonstration game plugin: implements <see cref="BaseGamePlugin"/> plus a custom
/// WPF configuration surface via <see cref="IPluginWpfUi"/> and a stub navigation mover.
/// </summary>
public sealed class SampleGamePlugin : BaseGamePlugin, IPluginWpfUi
{
    private readonly SampleMover _mover = new();

    public SampleGamePlugin(ILogger logger, IServiceProvider services)
        : base(logger, services)
    {
    }

    public override string Id => "sample";

    public override string Version => "1.0.0";

    public override ModuleManifest GetManifest() => new()
    {
        Id = Id,
        Name = "Sample Game",
        Version = Version,
        Author = "KKBOT",
        Description = "Demo plugin illustrating the KKBOT plugin host contract.",
        DefaultConfig = "{ \"targetX\": 100, \"targetY\": 200, \"loop\": true }",
        ConfigSchema = "{ \"type\": \"object\", \"properties\": { \"targetX\": { \"type\": \"number\" }, \"targetY\": { \"type\": \"number\" }, \"loop\": { \"type\": \"boolean\" } } }",
    };

    protected override Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        GameContext.Navigation.SetMover(_mover);
        return Task.CompletedTask;
    }

    protected override Task OnStartAsync(CancellationToken cancellationToken)
        => GameContext.Navigation.NavigateToAsync(new WorldPoint(100, 200), cancellationToken);

    public FrameworkElement? CreateConfigView(ModuleManifest manifest, IConfigBinder binder)
        => new SampleConfigControl(manifest, binder);
}

/// <summary>
/// Custom configuration control demonstrating the override path (instead of the schema form).
/// </summary>
public sealed class SampleConfigControl : GamePluginConfigControl
{
    private readonly TextBox _x;
    private readonly TextBox _y;
    private readonly TextBlock _status;

    public SampleConfigControl(ModuleManifest manifest, IConfigBinder binder)
        : base(manifest, binder)
    {
        _x = new TextBox { Text = "100", Margin = new Thickness(0, 2, 0, 2) };
        _y = new TextBox { Text = "200", Margin = new Thickness(0, 2, 0, 2) };
        _status = new TextBlock { Margin = new Thickness(0, 6, 0, 0), Foreground = System.Windows.Media.Brushes.Gray };

        var save = new Button { Content = "Save", Width = 80, HorizontalAlignment = HorizontalAlignment.Left };
        save.Click += OnSave;

        Content = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = "Sample Game Settings", FontWeight = FontWeights.Bold },
                new TextBlock { Text = "Target X" },
                _x,
                new TextBlock { Text = "Target Y" },
                _y,
                save,
                _status,
            },
        };
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var json = $"{{ \"targetX\": {_x.Text}, \"targetY\": {_y.Text}, \"loop\": true }}";
        try
        {
            Binder.ApplyJson(json);
            _status.Text = "Saved.";
            _status.Foreground = System.Windows.Media.Brushes.LightGreen;
        }
        catch (Exception ex)
        {
            _status.Text = $"Error: {ex.Message}";
            _status.Foreground = System.Windows.Media.Brushes.OrangeRed;
        }
    }
}

/// <summary>
/// Stub mover that "teleports" instantly; a real plugin would issue device input.
/// </summary>
public sealed class SampleMover : INavigationMover
{
    public WorldPoint CurrentPosition { get; private set; } = new(0, 0);

    public Task MoveTowardAsync(WorldPoint direction, CancellationToken cancellationToken)
    {
        CurrentPosition = direction;
        return Task.CompletedTask;
    }

    public Task<bool> IsStuckAsync(CancellationToken cancellationToken) => Task.FromResult(false);
}
