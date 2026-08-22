// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KKBOT.Orchestration.Saga;

public interface ISaga
{
    Guid SagaId { get; }
    string SagaType { get; }
    SagaStatus Status { get; }
    IReadOnlyList<SagaStep> Steps { get; }
    int CurrentStepIndex { get; }
    DateTime StartedAt { get; }
    DateTime? CompletedAt { get; }
    string? ErrorMessage { get; }
}

public enum SagaStatus
{
    Pending,
    Running,
    Completed,
    Compensating,
    Compensated,
    Failed
}

public sealed record SagaStep(
    string Name,
    Func<CancellationToken, Task<StepResult>> ExecuteAsync,
    Func<CancellationToken, Task>? CompensateAsync = null,
    bool IsCritical = false
);

public sealed record StepResult(bool Success, string? ErrorMessage = null, object? Output = null);

public interface ISagaOrchestrator
{
    Task<ISaga> StartSagaAsync<TData>(string sagaType, TData data, CancellationToken cancellationToken = default);
    Task<ISaga> ContinueSagaAsync(Guid sagaId, CancellationToken cancellationToken = default);
    Task<ISaga> CompensateSagaAsync(Guid sagaId, CancellationToken cancellationToken = default);
    Task<ISaga?> GetSagaAsync(Guid sagaId, CancellationToken cancellationToken = default);
}

public interface ISagaRepository
{
    Task<ISaga> GetAsync(Guid sagaId, CancellationToken ct);
    Task SaveAsync(ISaga saga, CancellationToken ct);
    Task<IReadOnlyList<ISaga>> GetByStatusAsync(SagaStatus status, CancellationToken ct);
}