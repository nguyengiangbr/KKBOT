// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using KKBOT.Device.Abstractions;

namespace KKBOT.Perception.Abstractions;

/// <summary>
/// Detects a particular game element within a frame.
/// </summary>
public interface IGameDetector
{
    string Id { get; }
    string Name { get; }
    double RequiredConfidence { get; }
    Task<DetectorResult> DetectAsync(Frame frame, CancellationToken cancellationToken);
}