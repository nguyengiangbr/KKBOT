// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace KKBOT.Device.Abstractions;

/// <summary>
/// Dispatcher for sending input commands to a device (simulated or real).
/// </summary>
public interface IInputDispatcher
{
    Task<DispatchResult> DispatchAsync(InputCommand command, CancellationToken cancellationToken);
}
