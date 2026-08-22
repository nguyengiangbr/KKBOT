using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace KKBOT.Observability.Logging;

/// <summary>
/// Configures Serilog for the application.
/// </summary>
public static class Bootstrapper
{
    public static IHostBuilder UseSerilogLogging(this IHostBuilder builder)
    {
        return builder.UseSerilog((ctx, lc) =>
        {
            lc.MinimumLevel.Information()
              .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
              .Enrich.FromLogContext()
              .Enrich.WithProperty("Application", "KKBOT")
              .WriteTo.Console()
              .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day);
        });
    }
}