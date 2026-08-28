using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using KKBOT.Application;
using KKBOT.Orchestration;
using KKBOT.Security;
using KKBOT.Observability;
using KKBOT.Observability.Logging;
using MediatR;

namespace KKBOT.Dashboard;

public class DashboardProgram
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Logging
        builder.Host.UseSerilogLogging();

        // Services
        builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DashboardProgram).Assembly));
        builder.Services.AddSingleton<IInstanceManager, KKBOT.Orchestration.InstanceManager>();
        // Add other services as needed
        builder.Services.AddControllers();

        var app = builder.Build();
        app.MapControllers();
        app.MapGet("/healthz", () => Results.Ok(new { status = "Healthy" }));
        app.Run();
    }
}
