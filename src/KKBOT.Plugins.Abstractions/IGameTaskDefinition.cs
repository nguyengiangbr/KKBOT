// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using KKBOT.Domain.Models.Perception;

namespace KKBOT.Plugins.Abstractions;

public interface IGameTaskDefinition
{
    string Id { get; }
    string Name { get; }
    string? Description { get; }
    TaskType Type { get; }
    TimeSpan DefaultTimeout { get; }
    RetryPolicy DefaultRetryPolicy { get; }
    bool RequiresApproval { get; }
    Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken cancellationToken);
}

public enum TaskType { OneShot, Repeating, Manual }
public sealed record TaskContext(Guid InstanceId, Guid JobId, IDictionary<string, object?> Parameters);
