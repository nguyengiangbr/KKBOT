// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using KKBOT.Domain.Models.Perception;

namespace KKBOT.Plugins.Abstractions;

public interface IStateDefinition
{
    string Id { get; }
    string Name { get; }
    string Category { get; }
    Func<PerceptionContext?, bool> EntryCondition { get; }
    IStateAction? Action { get; }
    IReadOnlyList<string> NextStateIds { get; }
    TimeSpan Timeout { get; }
    RetryPolicy RetryPolicy { get; }
    EvidencePolicy EvidencePolicy { get; }
}

public sealed record RetryPolicy(int MaxRetries = 3, int RetryDelayMs = 500, bool ExponentialBackoff = true);
public sealed record EvidencePolicy(bool RequireScreenshot = true, bool RequireOcrResult = false);
public interface IStateAction { Task ExecuteAsync(CancellationToken ct); }