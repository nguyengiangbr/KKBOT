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
    private readonly ILogger<FakeInputDispatcher> _logger;

    public FakeInputDispatcher(ILogger<FakeInputDispatcher> logger)
    {
        _logger = logger;
    }

    public Task<DispatchResult> DispatchAsync(InputCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Simulated dispatch of {CommandType}: {Payload}", command.CommandType, command.Payload);
        var result = new DispatchResult { Success = true, Details = "Simulated dispatch succeeded" };
        return Task.FromResult(result);
    }
}