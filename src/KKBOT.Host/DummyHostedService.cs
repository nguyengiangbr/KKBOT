using Microsoft.Extensions.Hosting; using Microsoft.Extensions.Logging; using System.Threading; using System.Threading.Tasks;

namespace KKBOT.Host;

public sealed class DummyHostedService : BackgroundService
{
    private readonly ILogger<DummyHostedService> _logger;
    public DummyHostedService(ILogger<DummyHostedService> logger) => _logger = logger;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("KKBOT Host started – dummy background service running.");
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        _logger.LogInformation("KKBOT Host stopping.");
    }
}