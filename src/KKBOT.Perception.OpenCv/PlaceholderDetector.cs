// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using KKBOT.Perception.Abstractions;
using KKBOT.Device.Abstractions;
using System.Threading.Tasks;
using System.Threading;

namespace KKBOT.Perception.OpenCv;

public sealed class PlaceholderDetector : IGameDetector
{
    public string Id => "opencv-placeholder";
    public string Name => "OpenCV Placeholder Detector";
    public double RequiredConfidence => 0.9;
    public Task<DetectorResult> DetectAsync(Frame frame, CancellationToken cancellationToken) => Task.FromResult(new DetectorResult
    {
        DetectorId = Id,
        DetectorName = Name,
        Confidence = 1.0,
        IsMatch = true,
        DetectedValue = "placeholder",
        RawScore = 1.0
    });
}