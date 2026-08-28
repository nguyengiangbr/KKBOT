using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using KKBOT.Orchestration;
using KKBOT.Dashboard;
using KKBOT.Security;
using KKBOT.Observability;
using KKBOT.Observability.Logging;
using KKBOT.Host;
using MediatR;

var host = Host.CreateDefaultBuilder(args)
    .UseSerilogLogging()
    .ConfigureServices((ctx, services) =>
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
        services.AddSingleton<IInstanceManager, InstanceManager>();
        services.AddSingleton<IScheduler, Scheduler>();
        services.AddSingleton<IRecoveryCoordinator, RecoveryCoordinator>();
        // Register a dummy background service so the host has something to run.
        services.AddHostedService<DummyHostedService>();
    })
    .Build();
await host.RunAsync();
