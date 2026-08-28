// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

namespace KKBOT.Perception.Abstractions;

/// <summary>
/// Detector result from perception analysis.
/// </summary>
public record DetectorResult
{
    public string DetectorId { get; init; } = string.Empty;
    public string DetectorName { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public bool IsMatch { get; init; }
    public string? DetectedValue { get; init; }
    public double RawScore { get; init; }
}
