// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using KKBOT.Perception.Abstractions;

namespace KKBOT.Plugins.Abstractions;

/// <summary>
/// Context for perception evaluation within the FSM and plugin state conditions.
/// </summary>
public record PerceptionContext(
    string InstanceId,
    byte[]? FrameData,
    int FrameWidth,
    int FrameHeight,
    double CurrentConfidence,
    IReadOnlyDictionary<string, object?>? AdditionalData = null,
    IReadOnlyList<DetectorResult>? Detections = null);
