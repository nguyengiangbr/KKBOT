// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace KKBOT.PluginHost;

/// <summary>
/// Abstraction over a single game client's input/perception surface (one per clone account).
/// The real implementation issues Win32 input and screen capture; the slice uses a stub.
/// </summary>
public interface IDeviceSession
{
    string DeviceId { get; }

    Task SendKeyAsync(string key, CancellationToken cancellationToken = default);

    Task ClickAtAsync(int x, int y, CancellationToken cancellationToken = default);

    Task<byte[]> CaptureFrameAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A single botting account (clone) bound to a game.
/// </summary>
public sealed record AccountProfile(
    string Id,
    string GameId,
    string Username,
    string Server,
    string? Character = null,
    IReadOnlyDictionary<string, string>? Settings = null);
