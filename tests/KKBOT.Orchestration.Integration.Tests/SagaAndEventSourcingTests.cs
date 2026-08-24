// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Orchestration.Saga;
using Xunit;
using FluentAssertions;

namespace KKBOT.Orchestration.Integration.Tests;

/// <summary>
/// Integration tests for Saga pattern with compensation
/// </summary>
public sealed class SagaIntegrationTests
{
    [Fact]
    public async Task Saga_AllStepsSucceed_ShouldCompleteSuccessfully()
    {
        // Arrange
        var saga = new Saga();
        var executedSteps = new List<string>();
        var compensatedSteps = new List<string>();

        saga.AddStep(
            async (ct) => { await Task.Delay(10, ct); executedSteps.Add("Step1"); },
            async (ct) => { await Task.Delay(5, ct); compensatedSteps.Add("Step1"); },
            "Step 1"
        );

        saga.AddStep(
            async (ct) => { await Task.Delay(10, ct); executedSteps.Add("Step2"); },
            async (ct) => { await Task.Delay(5, ct); compensatedSteps.Add("Step2"); },
            "Step 2"
        );

        saga.AddStep(
            async (ct) => { await Task.Delay(10, ct); executedSteps.Add("Step3"); },
            async (ct) => { await Task.Delay(5, ct); compensatedSteps.Add("Step3"); },
            "Step 3"
        );

        // Act
        var result = await saga.ExecuteAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        executedSteps.Should().HaveCount(3);
        compensatedSteps.Should().BeEmpty();
    }

    [Fact]
    public async Task Saga_MiddleStepFails_ShouldCompensatePreviousStepsInReverseOrder()
    {
        // Arrange
        var saga = new Saga();
        var executedSteps = new List<string>();
        var compensatedSteps = new List<string>();

        saga.AddStep(
            async (ct) => { await Task.Delay(10, ct); executedSteps.Add("Step1"); },
            async (ct) => { await Task.Delay(5, ct); compensatedSteps.Add("Step1"); },
            "Step 1"
        );

        saga.AddStep(
            async (ct) => { await Task.Delay(10, ct); executedSteps.Add("Step2"); throw new InvalidOperationException("Step 2 failed"); },
            async (ct) => { await Task.Delay(5, ct); compensatedSteps.Add("Step2"); },
            "Step 2"
        );

        saga.AddStep(
            async (ct) => { await Task.Delay(10, ct); executedSteps.Add("Step3"); },
            async (ct) => { await Task.Delay(5, ct); compensatedSteps.Add("Step3"); },
            "Step 3"
        );

        // Act
        var result = await saga.ExecuteAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Step 2 failed");
        
        // Steps 1 and 2 executed, step 3 should not
        executedSteps.Should().HaveCount(2);
        executedSteps.Should().ContainInOrder("Step1", "Step2");
        
        // Compensation should run in reverse order for completed steps
        compensatedSteps.Should().HaveCount(2);
        compensatedSteps.Should().ContainInOrder("Step2", "Step1"); // Reverse order!
    }

    [Fact]
    public async Task Saga_FirstStepFails_ShouldNotCompensate()
    {
        // Arrange
        var saga = new Saga();
        var executedSteps = new List<string>();
        var compensatedSteps = new List<string>();

        saga.AddStep(
            async (ct) => { await Task.Delay(10, ct); executedSteps.Add("Step1"); throw new InvalidOperationException("Step 1 failed"); },
            async (ct) => { await Task.Delay(5, ct); compensatedSteps.Add("Step1"); },
            "Step 1"
        );

        saga.AddStep(
            async (ct) => { await Task.Delay(10, ct); executedSteps.Add("Step2"); },
            async (ct) => { await Task.Delay(5, ct); compensatedSteps.Add("Step2"); },
            "Step 2"
        );

        // Act
        var result = await saga.ExecuteAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        executedSteps.Should().HaveCount(1);
        compensatedSteps.Should().BeEmpty(); // No compensation for first step failure
    }

    [Fact]
    public async Task Saga_WithCancellation_ShouldCompensateCompletedSteps()
    {
        // Arrange
        var saga = new Saga();
        var executedSteps = new List<string>();
        var compensatedSteps = new List<string>();
        var cts = new CancellationTokenSource();

        saga.AddStep(
            async (ct) => { await Task.Delay(100, ct); executedSteps.Add("Step1"); },
            async (ct) => { await Task.Delay(10, ct); compensatedSteps.Add("Step1"); },
            "Step 1"
        );

        saga.AddStep(
            async (ct) => { await Task.Delay(100, ct); executedSteps.Add("Step2"); },
            async (ct) => { await Task.Delay(10, ct); compensatedSteps.Add("Step2"); },
            "Step 2"
        );

        // Act - Cancel after a short delay
        var executeTask = saga.ExecuteAsync(cts.Token);
        await Task.Delay(50);
        cts.Cancel();
        
        var result = await executeTask;

        // Assert
        result.IsSuccess.Should().BeFalse();
        // At least step 1 should have started and been compensated
        compensatedSteps.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Saga_StepTimeout_ShouldTriggerCompensation()
    {
        // Arrange
        var saga = new Saga();
        var executedSteps = new List<string>();
        var compensatedSteps = new List<string>();

        saga.AddStep(
            async (ct) => 
            { 
                await Task.Delay(10, ct); 
                executedSteps.Add("Step1"); 
            },
            async (ct) => { await Task.Delay(5, ct); compensatedSteps.Add("Step1"); },
            "Step 1"
        );

        saga.AddStep(
            async (ct) => 
            { 
                // This step will timeout
                await Task.Delay(Timeout.Infinite, ct); 
            },
            async (ct) => { await Task.Delay(5, ct); compensatedSteps.Add("Step2"); },
            "Step 2"
        );

        // Act - Execute with short timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var result = await saga.ExecuteAsync(cts.Token);

        // Assert
        result.IsSuccess.Should().BeFalse();
        compensatedSteps.Should().Contain("Step1");
    }
}

/// <summary>
/// Tests for EventSourcing projections
/// </summary>
public sealed class EventSourcingIntegrationTests
{
    [Fact]
    public void EventStore_Projection_ShouldBuildCorrectState()
    {
        // Arrange
        var eventStore = new EventStore();
        var instanceId = Guid.NewGuid();

        // Act - Append events in sequence
        eventStore.Append(new InstanceCreatedEvent(instanceId));
        eventStore.Append(new InstanceStateChangedEvent(instanceId, InstanceState.Created, InstanceState.Ready, "Initialized"));
        eventStore.Append(new InstanceStateChangedEvent(instanceId, InstanceState.Ready, InstanceState.Running, "Started"));
        eventStore.Append(new JobStartedEvent(instanceId, Guid.NewGuid()));
        eventStore.Append(new JobCompletedEvent(instanceId, Guid.NewGuid(), true));
        eventStore.Append(new InstanceStateChangedEvent(instanceId, InstanceState.Running, InstanceState.Ready, "Job completed"));

        // Assert - Build projection
        var events = eventStore.GetEvents(instanceId);
        events.Should().HaveCount(6);

        var stateChanges = events.OfType<InstanceStateChangedEvent>().ToList();
        stateChanges.Should().HaveCount(3);
        stateChanges[0].FromState.Should().Be(InstanceState.Created);
        stateChanges[0].ToState.Should().Be(InstanceState.Ready);
        stateChanges[1].FromState.Should().Be(InstanceState.Ready);
        stateChanges[1].ToState.Should().Be(InstanceState.Running);
        stateChanges[2].FromState.Should().Be(InstanceState.Running);
        stateChanges[2].ToState.Should().Be(InstanceState.Ready);

        var jobEvents = events.Where(e => e is JobStartedEvent or JobCompletedEvent).ToList();
        jobEvents.Should().HaveCount(2);
    }

    [Fact]
    public void EventStore_MultipleInstances_ShouldIsolateEvents()
    {
        // Arrange
        var eventStore = new EventStore();
        var instance1 = Guid.NewGuid();
        var instance2 = Guid.NewGuid();

        // Act
        eventStore.Append(new InstanceCreatedEvent(instance1));
        eventStore.Append(new InstanceCreatedEvent(instance2));
        eventStore.Append(new InstanceStateChangedEvent(instance1, InstanceState.Created, InstanceState.Running, "Start 1"));
        eventStore.Append(new InstanceStateChangedEvent(instance2, InstanceState.Created, InstanceState.Ready, "Ready 2"));

        // Assert
        var events1 = eventStore.GetEvents(instance1);
        var events2 = eventStore.GetEvents(instance2);

        events1.Should().HaveCount(2);
        events2.Should().HaveCount(2);
        events1.Should().AllSatisfy(e => e.InstanceId.Should().Be(instance1));
        events2.Should().AllSatisfy(e => e.InstanceId.Should().Be(instance2));
    }
}

/// <summary>
/// Tests for Recovery Coordinator escalation
/// </summary>
public sealed class RecoveryIntegrationTests
{
    [Fact]
    public async Task Recovery_EscalationLadder_ShouldFollowCorrectOrder()
    {
        // Arrange
        var logger = new Mock<ILogger<RecoveryCoordinator>>().Object;
        var instanceManager = new Mock<IInstanceManager>().Object;
        var coordinator = new RecoveryCoordinator(logger, instanceManager);
        var instanceId = Guid.NewGuid();

        // Act - Track escalation levels
        var levels = new List<RecoveryLevel>();
        
        // Simulate repeated failures
        for (int i = 0; i < 20; i++)
        {
            var level = await coordinator.DetermineNextLevelAsync(instanceId, ErrorCodes.TASK_FAILED, CancellationToken.None);
            levels.Add(level);
        }

        // Assert - Should follow escalation ladder
        levels[0].Should().Be(RecoveryLevel.RetryOperation);
        
        // Should eventually reach ManualReview or StopInstance
        levels.Should().Contain(RecoveryLevel.RecaptureAndReevaluate);
        levels.Should().Contain(RecoveryLevel.RestartTask);
        levels.Should().Contain(RecoveryLevel.RestartApplication);
        levels.Should().Contain(RecoveryLevel.ReconnectDevice);
        
        // Last level should be terminal
        levels.Last().Should().BeOneOf(RecoveryLevel.ManualReview, RecoveryLevel.StopInstance);
    }

    [Fact]
    public async Task Recovery_ResetCounter_ShouldRestartEscalation()
    {
        // Arrange
        var logger = new Mock<ILogger<RecoveryCoordinator>>().Object;
        var instanceManager = new Mock<IInstanceManager>().Object;
        var coordinator = new RecoveryCoordinator(logger, instanceManager);
        var instanceId = Guid.NewGuid();

        // Act - Escalate a few levels
        await coordinator.DetermineNextLevelAsync(instanceId, ErrorCodes.TASK_FAILED, CancellationToken.None);
        await coordinator.DetermineNextLevelAsync(instanceId, ErrorCodes.TASK_FAILED, CancellationToken.None);
        await coordinator.DetermineNextLevelAsync(instanceId, ErrorCodes.TASK_FAILED, CancellationToken.None);
        
        var levelBeforeReset = await coordinator.DetermineNextLevelAsync(instanceId, ErrorCodes.TASK_FAILED, CancellationToken.None);
        levelBeforeReset.Should().BeGreaterThan(RecoveryLevel.RetryOperation);

        // Reset
        await coordinator.ResetRecoveryCounterAsync(instanceId, CancellationToken.None);

        // Act - Should start from RetryOperation again
        var levelAfterReset = await coordinator.DetermineNextLevelAsync(instanceId, ErrorCodes.TASK_FAILED, CancellationToken.None);
        levelAfterReset.Should().Be(RecoveryLevel.RetryOperation);
    }
}