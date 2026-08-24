// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using KKBOT.Plugins.Loader;
using KKBOT.Device.Abstractions;
using KKBOT.Perception.Abstractions;
using KKBOT.AI.Abstractions;
using KKBOT.Orchestration;
using KKBOT.Observability;
using KKBOT.Plugins.Abstractions;
using KKBOT.Device.Abstractions;
using KKBOT.AI.Abstractions;
using KKBOT.Orchestration;
using KKBOT.Observability;
using KKBOT.Plugins.Abstractions;

namespace KKBOT.CLI;

/// <summary>
/// KKBOT Command Line Interface.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("KKBOT - Game Automation Platform CLI");

        // Instance commands
        var instanceCommand = new Command("instance", "Manage automation instances");
        var startCommand = new Command("start", "Start an instance");
        var stopCommand = new Command("stop", "Stop an instance");
        var listCommand = new Command("list", "List all instances");
        var statusCommand = new Command("status", "Get instance status");

        startCommand.AddArgument(new Argument<string>("instance-id", "Instance ID to start"));
        stopCommand.AddArgument(new Argument<string>("instance-id", "Instance ID to stop"));
        statusCommand.AddArgument(new Argument<string>("instance-id", "Instance ID to check"));

        startCommand.SetHandler(async (instanceId, services) =>
        {
            var orchestrator = services.GetRequiredService<InstanceOrchestrator>();
            await orchestrator.StartInstanceAsync(instanceId, CancellationToken.None);
            Console.WriteLine($"Started instance {instanceId}");
        }, new Argument<string>("instance-id"), new FromKeyedServices("orchestrator"));

        stopCommand.SetHandler(async (instanceId, services) =>
        {
            var orchestrator = services.GetRequiredService<InstanceOrchestrator>();
            await orchestrator.StopInstanceAsync(instanceId, "CLI stop", CancellationToken.None);
            Console.WriteLine($"Stopped instance {instanceId}");
        }, new Argument<string>("instance-id"), new FromKeyedServices("orchestrator"));

        statusCommand.SetHandler(async (instanceId, services) =>
        {
            var manager = services.GetRequiredService<IInstanceManager>();
            var instance = manager.GetInstance(instanceId);
            if (instance != null)
            {
                Console.WriteLine($"Instance: {instance.DisplayName}");
                Console.WriteLine($"State: {instance.State}");
                Console.WriteLine($"Device: {instance.DeviceId}");
                Console.WriteLine($"Module: {instance.Module?.Id}");
            }
            else
            {
                Console.WriteLine($"Instance {instanceId} not found");
            }
        }, new Argument<string>("instance-id"), new FromKeyedServices("instanceManager"));

        listCommand.SetHandler(async (services) =>
        {
            var manager = services.GetRequiredService<IInstanceManager>();
            var instances = manager.GetAllInstances();
            Console.WriteLine($"{"ID",-36} {"Name",-20} {"State",-15} {"Device",-20} {"Module",-20}");
            foreach (var inst in instances)
            {
                Console.WriteLine($"{inst.Id,-36} {inst.DisplayName,-20} {inst.State,-15} {inst.DeviceId,-20} {inst.Module?.Id ?? "N/A",-20}");
            }
        }, new FromKeyedServices("instanceManager"));

        instanceCommand.AddCommand(startCommand);
        instanceCommand.AddCommand(stopCommand);
        instanceCommand.AddCommand(listCommand);
        instanceCommand.AddCommand(statusCommand);

        // Plugin commands
        var pluginCommand = new Command("plugin", "Manage plugins");
        var pluginListCommand = new Command("list", "List loaded plugins");
        var pluginLoadCommand = new Command("load", "Load plugins from directory");
        var pluginValidateCommand = new Command("validate", "Validate a plugin");

        pluginLoadCommand.AddArgument(new Argument<string>("path", "Path to plugins directory") 
        { Arity = ArgumentArity.ZeroOrOne });

        pluginListCommand.SetHandler(async (services) =>
        {
            var loader = services.GetRequiredService<PluginLoader>();
            var plugins = await loader.LoadPluginsAsync("plugins", CancellationToken.None);
            Console.WriteLine($"{"ID",-30} {"Name",-30} {"Version",-10} {"Author",-20}");
            foreach (var plugin in plugins)
            {
                var manifest = plugin.GetManifest();
                Console.WriteLine($"{manifest.Id,-30} {manifest.Name,-30} {manifest.Version,-10} {manifest.Author,-20}");
            }
        }, new FromKeyedServices("pluginLoader"));

        pluginLoadCommand.SetHandler(async (path, services) =>
        {
            var loader = services.GetRequiredService<PluginLoader>();
            var plugins = await loader.LoadPluginsAsync(path ?? "plugins", CancellationToken.None);
            Console.WriteLine($"Loaded {plugins.Count} plugins");
        }, new Argument<string>("path"), new FromKeyedServices("pluginLoader"));

        pluginValidateCommand.AddArgument(new Argument<string>("path", "Path to plugin directory"));
        pluginValidateCommand.SetHandler(async (path, services) =>
        {
            var loader = services.GetRequiredService<PluginLoader>();
            var plugins = await loader.LoadPluginsAsync(path, CancellationToken.None);
            foreach (var plugin in plugins)
            {
                var result = await plugin.ValidateAsync(new ModuleContext(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);
                Console.WriteLine($"Plugin {plugin.Id}: {(result.IsValid ? "Valid" : "Invalid")}");
                if (!result.IsValid)
                {
                    foreach (var error in result.Errors)
                    {
                        Console.WriteLine($"  Error: {error.Code} - {error.Message}");
                    }
                }
            }
        }, new Argument<string>("path"), new FromKeyedServices("pluginLoader"));

        pluginCommand.AddCommand(pluginListCommand);
        pluginCommand.AddCommand(pluginLoadCommand);
        pluginCommand.AddCommand(pluginValidateCommand);

        // Device commands
        var deviceCommand = new Command("device", "Manage devices");
        var deviceListCommand = new Command("list", "List connected devices");
        var deviceInfoCommand = new Command("info", "Get device info");

        deviceInfoCommand.AddArgument(new Argument<string>("device-id", "Device ID"));

        deviceListCommand.SetHandler(async (services) =>
        {
            var discovery = services.GetRequiredService<IDeviceDiscovery>();
            var devices = await discovery.DiscoverAsync(CancellationToken.None);
            Console.WriteLine($"{"ID",-30} {"Name",-30} {"Type",-15} {"Status",-10}");
            foreach (var device in devices)
            {
                Console.WriteLine($"{device.DeviceId,-30} {device.Name,-30} {device.Type,-15} {device.IsConnected ? "Connected" : "Disconnected",-10}");
            }
        }, new FromKeyedServices("deviceDiscovery"));

        deviceInfoCommand.SetHandler(async (deviceId, services) =>
        {
            var factory = services.GetRequiredService<IDeviceSessionFactory>();
            var session = await factory.CreateAsync(deviceId, CancellationToken.None);
            var info = await session.GetInfoAsync(CancellationToken.None);
            Console.WriteLine($"Device: {info.DeviceId}");
            Console.WriteLine($"Model: {info.Model}");
            Console.WriteLine($"Resolution: {info.ScreenWidth}x{info.ScreenHeight}");
            Console.WriteLine($"Battery: {info.BatteryLevel}%");
        }, new Argument<string>("device-id"), new FromKeyedServices("deviceFactory"));

        deviceCommand.AddCommand(deviceListCommand);
        deviceCommand.AddCommand(deviceInfoCommand);

        // Plugin commands
        var perceptionCommand = new Command("perception", "Perception tools");
        var detectCommand = new Command("detect", "Run template detection");
        detectCommand.AddArgument(new Argument<string>("template", "Template name"));
        detectCommand.AddArgument(new Argument<string>("device-id", "Device ID"));
        detectCommand.SetHandler(async (template, deviceId, services) =>
        {
            var perception = services.GetRequiredService<IPerceptionPipeline>();
            var factory = services.GetRequiredService<IDeviceSessionFactory>();
            var session = await factory.CreateAsync(deviceId, CancellationToken.None);
            var frame = await session.CaptureAsync(new CaptureOptions(), CancellationToken.None);
            var results = await perception.ProcessAsync(frame, CancellationToken.None);
            var match = results.FirstOrDefault(r => r.DetectorName.Contains(template, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                Console.WriteLine($"Found: {match.DetectorName} (Confidence: {match.Confidence:P2})");
            }
            else
            {
                Console.WriteLine("Template not found");
            }
        }, new Argument<string>("template"), new Argument<string>("device-id"), new FromKeyedServices("perception", "deviceFactory"));

        // AI commands
        var aiCommand = new Command("ai", "AI tools");
        var aiGenerateCommand = new Command("generate", "Generate text");
        aiGenerateCommand.AddArgument(new Argument<string>("prompt", "Prompt text"));
        aiGenerateCommand.AddOption(new Option<string>("--model", "Model to use") { IsRequired = false });
        aiGenerateCommand.SetHandler(async (prompt, model, services) =>
        {
            var aiService = services.GetRequiredService<IAiService>();
            var result = await aiService.GenerateAsync(prompt, CancellationToken.None);
            Console.WriteLine(result.Content);
        }, new Argument<string>("prompt"), new Option<string>("--model"), new FromKeyedServices("aiService"));

        // Scenario commands
        var scenarioCommand = new Command("scenario", "Manage scenarios");
        var scenarioListCommand = new Command("list", "List scenarios");
        var scenarioRunCommand = new Command("run", "Run a scenario");
        var scenarioValidateCommand = new Command("validate", "Validate a scenario");

        scenarioRunCommand.AddArgument(new Argument<string>("scenario-id", "Scenario ID"));
        scenarioRunCommand.AddOption(new Option<string>("--device", "Device ID") { IsRequired = false });

        // Config commands
        var configCommand = new Command("config", "Manage configuration");
        var configShowCommand = new Command("show", "Show current configuration");
        var configSetCommand = new Command("set", "Set configuration value");
        configSetCommand.AddArgument(new Argument<string>("key", "Configuration key"));
        configSetCommand.AddArgument(new Argument<string>("value", "Configuration value"));

        // Add all commands
        rootCommand.AddCommand(instanceCommand);
        rootCommand.AddCommand(pluginCommand);
        rootCommand.AddCommand(deviceCommand);
        rootCommand.AddCommand(perceptionCommand);
        rootCommand.AddCommand(aiCommand);
        rootCommand.AddCommand(scenarioCommand);
        rootCommand.AddCommand(configCommand);

        // Build command line with DI
        var builder = new CommandLineBuilder(rootCommand)
            .UseDefaults()
            .UseHost(_ => Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Register KKBOT services
                    services.AddKkbotPersistence();
                    services.AddKkbotSecurity();
                    services.AddKkbotIdentity();
                    services.AddKkbotPluginMarketplace();
                    services.AddKkbotAIPipeline();
                    services.AddKkbotAnalytics();
                    services.AddKkbotCollaboration();
                    services.AddKkbotEnterprise();
                    services.AddKkbotOrchestration();
                    services.AddKkbotApplication();
                    services.AddKkbotAgent();
                    services.AddKkbotAgentCoordination();
                    services.AddKkbotLogisticsSurvival();
                    services.AddKkbotAgentCoordination();
                    services.AddKkbotEconomyTrading();
                    services.AddKkbotPerformance();
                    services.AddKkbotObservability();
                    services.AddKkbotVisualScripting();
                    
                    // Register CLI-specific services
                    services.AddSingleton<PluginLoader>();
                }))
            .UseMiddleware(async (context, next) =>
            {
                try
                {
                    await next(context);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error: {ex.Message}");
                    context.ExitCode = 1;
                }
            })
            .Build();

        return await builder.InvokeAsync(args);
    }
}

