using KKBOT.Device.Abstractions;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KKBOT.Device.Simulator;

/// <summary>
/// Provides frames from pre-recorded scenario files.
/// </summary>
public sealed class ReplayFrameProvider
{
    private readonly string _scenarioPath;
    private readonly ILogger _logger;

    public ReplayFrameProvider(string scenarioPath, ILogger logger)
    {
        _scenarioPath = scenarioPath;
        _logger = logger;
    }

    public async IAsyncEnumerable<Frame> GetFramesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Assume the scenario folder contains binary frame files named sequentially.
        var files = Directory.GetFiles(_scenarioPath, "*.frame", SearchOption.TopDirectoryOnly);
        Array.Sort(files);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var data = await File.ReadAllBytesAsync(file, cancellationToken);
            yield return new Frame { Timestamp = File.GetCreationTimeUtc(file), Data = data };
        }
    }
}