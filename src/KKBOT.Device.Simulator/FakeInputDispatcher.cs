using KKBOT.Device.Abstractions;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Threading;

namespace KKBOT.Device.Simulator;

/// <summary>
/// Input dispatcher that logs commands without executing them.
/// </summary>
public sealed class FakeInputDispatcher : IInputDispatcher
{
    private readonly ILogger _logger;

    public FakeInputDispatcher(ILogger logger)
    {
        _logger = logger;
    }

    public Task<DispatchResult> DispatchAsync(InputCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Simulated dispatch of {CommandType}: {Payload}", command.Type, command.Text);
        var result = new DispatchResult { Success = true, Details = "Simulated dispatch succeeded" };
        return Task.FromResult(result);
    }
}