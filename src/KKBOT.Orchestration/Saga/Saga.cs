// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Orchestration.EventSourcing;
using KKBOT.Orchestration.Saga;
using Microsoft.Extensions.Logging;

namespace KKBOT.Orchestration.Saga;

public sealed class Saga : ISaga
{
    private readonly List<SagaStep> _steps = new();
    private int _currentStepIndex = 0;
    private SagaStatus _status = SagaStatus.Pending;
    private string? _errorMessage;

    public Guid SagaId { get; }
    public string SagaType { get; }
    public SagaStatus Status => _status;
    public IReadOnlyList<SagaStep> Steps => _steps;
    public int CurrentStepIndex => _currentStepIndex;
    public DateTime StartedAt { get; }
    public DateTime? CompletedAt { get; private set; }
    public string? ErrorMessage => _errorMessage;

    private Saga(Guid sagaId, string sagaType, IEnumerable<SagaStep> steps)
    {
        SagaId = sagaId;
        SagaType = sagaType;
        StartedAt = DateTime.UtcNow;
        _steps.AddRange(steps);
    }

    public static Saga Create<TData>(string sagaType, TData data, IEnumerable<SagaStep> steps)
    {
        var saga = new Saga(Guid.NewGuid(), sagaType, steps);
        // Store initial data as first step output
        return saga;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken, ILogger logger)
    {
        _status = SagaStatus.Running;
        logger.LogInformation("Starting saga {SagaId} ({SagaType})", SagaId, SagaType);

        for (; _currentStepIndex < _steps.Count; _currentStepIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var step = _steps[_currentStepIndex];
            logger.LogDebug("Executing step {StepIndex}/{TotalSteps}: {StepName}", 
                _currentStepIndex + 1, _steps.Count, step.Name);

            try
            {
                var result = await step.ExecuteAsync(cancellationToken);
                
                if (!result.Success)
                {
                    _errorMessage = result.ErrorMessage ?? $"Step {step.Name} failed";
                    logger.LogWarning("Step {StepName} failed: {Error}", step.Name, _errorMessage);
                    
                    if (step.IsCritical)
                    {
                        await CompensateAsync(cancellationToken, logger);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _errorMessage = ex.Message;
                logger.LogError(ex, "Step {StepName} threw exception", step.Name);
                
                if (step.IsCritical)
                {
                    await CompensateAsync(cancellationToken, logger);
                    return;
                }
            }
        }

        _status = SagaStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        logger.LogInformation("Saga {SagaId} completed successfully", SagaId);
    }

    public async Task CompensateAsync(CancellationToken cancellationToken, ILogger logger)
    {
        _status = SagaStatus.Compensating;
        logger.LogWarning("Starting compensation for saga {SagaId}", SagaId);

        for (int i = _currentStepIndex - 1; i >= 0; i--)
        {
            var step = _steps[i];
            if (step.CompensateAsync != null)
            {
                try
                {
                    logger.LogDebug("Compensating step {StepName}", step.Name);
                    await step.CompensateAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Compensation failed for step {StepName}", step.Name);
                    _errorMessage = $"Compensation failed at step {step.Name}: {ex.Message}";
                    _status = SagaStatus.Failed;
                    return;
                }
            }
        }

        _status = SagaStatus.Compensated;
        CompletedAt = DateTime.UtcNow;
        logger.LogInformation("Saga {SagaId} compensated successfully", SagaId);
    }

    public void MarkFailed(string errorMessage)
    {
        _status = SagaStatus.Failed;
        _errorMessage = errorMessage;
        CompletedAt = DateTime.UtcNow;
    }
}

public sealed class SagaOrchestrator : ISagaOrchestrator
{
    private readonly ISagaRepository _repository;
    private readonly ILogger<SagaOrchestrator> _logger;

    public SagaOrchestrator(ISagaRepository repository, ILogger<SagaOrchestrator> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ISaga> StartSagaAsync<TData>(string sagaType, TData data, CancellationToken cancellationToken = default)
    {
        // This would be implemented with specific saga definitions
        // For now, return a placeholder
        var saga = new Saga(Guid.NewGuid(), sagaType, Array.Empty<SagaStep>());
        await _repository.SaveAsync(saga, cancellationToken);
        return saga;
    }

    public async Task<ISaga> ContinueSagaAsync(Guid sagaId, CancellationToken cancellationToken = default)
    {
        var saga = await _repository.GetAsync(sagaId, cancellationToken);
        if (saga == null) throw new InvalidOperationException($"Saga {sagaId} not found");
        
        if (saga is Saga sagaImpl)
        {
            await sagaImpl.ExecuteAsync(cancellationToken, _logger);
            await _repository.SaveAsync(sagaImpl, cancellationToken);
        }
        
        return saga;
    }

    public async Task<ISaga> CompensateSagaAsync(Guid sagaId, CancellationToken cancellationToken = default)
    {
        var saga = await _repository.GetAsync(sagaId, cancellationToken);
        if (saga == null) throw new InvalidOperationException($"Saga {sagaId} not found");
        
        if (saga is Saga sagaImpl)
        {
            await sagaImpl.CompensateAsync(cancellationToken, _logger);
            await _repository.SaveAsync(sagaImpl, cancellationToken);
        }
        
        return saga;
    }

    public async Task<ISaga?> GetSagaAsync(Guid sagaId, CancellationToken cancellationToken = default)
    {
        return await _repository.GetAsync(sagaId, cancellationToken);
    }
}