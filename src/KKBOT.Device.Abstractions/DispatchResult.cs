// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

namespace KKBOT.Device.Abstractions;

/// <summary>
/// Result of a dispatched input command.
/// </summary>
public record DispatchResult
{
    public bool Success { get; init; }
    public string? Details { get; init; }
}