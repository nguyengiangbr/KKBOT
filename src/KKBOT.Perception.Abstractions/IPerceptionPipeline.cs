// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

namespace KKBOT.Perception.Abstractions;

/// <summary>
/// Represents a pipeline that processes a frame and returns detection results.
/// </summary>
public interface IPerceptionPipeline
{
    Task<IReadOnlyList<DetectorResult>> ProcessAsync(Frame frame, CancellationToken cancellationToken);
}