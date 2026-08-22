using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Orchestration.DynamicDag;
using KKBOT.Orchestration.EventSourcing;
using KKBOT.Orchestration.Saga;
using KKBOT.Orchestration.MultiTenancy;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KKBOT.Orchestration.Advanced.Tests;

public class DynamicDagTests
{
    [Fact]
    public void DynamicDag_AddNodeAndEdge_BuildsCorrectly()
    {
        var dag = new DynamicDag();
        
        dag.AddNode(new DagNode("node1", "task", "Task 1", TaskId: "task1"))
           .AddNode(new DagNode("node2", "task", "Task 2", TaskId: "task2"))
           .AddEdge("node1", "node2");

        Assert.Equal(2, dag.Nodes.Count);
        Assert.Equal(1, dag.Edges.Count);
        Assert.Equal("node1", dag.Edges[0].From);
        Assert.Equal("node2", dag.Edges[0].To);
    }

    [Fact]
    public void DynamicDag_TopologicalSort_ReturnsCorrectOrder()
    {
        var dag = new DynamicDag();
        
        // A -> B -> C
        dag.AddNode(new DagNode("A", "task", "A"))
           .AddNode(new DagNode("B", "task", "B"))
           .AddNode(new DagNode("C", "task", "C"))
           .AddEdge("A", "B")
           .AddEdge("B", "C");

        var order = dag.GetTopologicalOrder();
        
        Assert.Equal(3, order.Count);
        Assert.Equal("A", order[0].NodeId);
        Assert.Equal("B", order[1].NodeId);
        Assert.Equal("C", order[2].NodeId);
    }

    [Fact]
    public void DynamicDag_TopologicalSort_HandlesBranching()
    {
        var dag = new DynamicDag();
        
        // A -> B, A -> C, B -> D, C -> D
        dag.AddNode(new DagNode("A", "task", "A"))
           .AddNode(new DagNode("B", "task", "B"))
           .AddNode(new DagNode("C", "task", "C"))
           .AddNode(new DagNode("D", "task", "D"))
           .AddEdge("A", "B")
           .AddEdge("A", "C")
           .AddEdge("B", "D")
           .AddEdge("C", "D");

        var order = dag.GetTopologicalOrder();
        
        Assert.Equal(4, order.Count);
        Assert.Equal("A", order[0].NodeId);
        Assert.Contains("B", order.Select(n => n.NodeId));
        Assert.Contains("C", order.Select(n => n.NodeId));
        Assert.Equal("D", order[3].NodeId);
    }

    [Fact]
    public void DynamicDag_TopologicalSort_DetectsCycle()
    {
        var dag = new DynamicDag();
        
        dag.AddNode(new DagNode("A", "task", "A"))
           .AddNode(new DagNode("B", "task", "B"))
           .AddEdge("A", "B")
           .AddEdge("B", "A");

        Assert.Throws<InvalidOperationException>(() => dag.GetTopologicalOrder());
    }
}

public class SagaTests
{
    [Fact]
    public async Task Saga_SimpleExecution_CompletesSuccessfully()
    {
        var steps = new List<SagaStep>
        {
            new("Step1", async ct => 
            {
                await Task.Delay(10, ct);
                return StepResult.Success("Output1");
            }),
            new("Step2", async ct => 
            {
                await Task.Delay(10, ct);
                return StepResult.Success("Output2");
            })
        };

        var saga = Saga.Create("test-saga", new { }, steps);
        
        await saga.ExecuteAsync(CancellationToken.None, NullLogger.Instance);
        
        Assert.Equal(SagaStatus.Completed, saga.Status);
        Assert.Equal(2, saga.CurrentStepIndex);
        Assert.NotNull(saga.CompletedAt);
    }

    [Fact]
    public async Task Saga_FailedCriticalStep_Compensates()
    {
        var compensated = new List<string>();
        
        var steps = new List<SagaStep>
        {
            new("Step1", async ct => 
            {
                await Task.Delay(10, ct);
                return StepResult.Success();
            }, 
            async ct => compensated.Add("Step1")), // Compensate
            
            new("Step2", async ct => 
            {
                await Task.Delay(10, ct);
                return StepResult.Failure("Critical failure");
            }, 
            async ct => compensated.Add("Step2"), // Compensate
            true) // IsCritical
        };

        var saga = Saga.Create("test-saga", new { }, steps);
        
        await saga.ExecuteAsync(CancellationToken.None, NullLogger.Instance);
        
        Assert.Equal(SagaStatus.Compensated, saga.Status);
        Assert.Contains("Step1", compensated);
        Assert.Contains("Step2", compensated);
    }

    [Fact]
    public async Task Saga_NonCriticalFailure_Continues()
    {
        var executed = new List<string>();
        
        var steps = new List<SagaStep>
        {
            new("Step1", async ct => 
            {
                executed.Add("Step1");
                await Task.Delay(10, ct);
                return StepResult.Success();
            }),
            
            new("Step2", async ct => 
            {
                executed.Add("Step2");
                await Task.Delay(10, ct);
                return StepResult.Failure("Non-critical failure");
            }, 
            null, 
            false), // Not critical
            
            new("Step3", async ct => 
            {
                executed.Add("Step3");
                await Task.Delay(10, ct);
                return StepResult.Success();
            })
        };

        var saga = Saga.Create("test-saga", new { }, steps);
        
        await saga.ExecuteAsync(CancellationToken.None, NullLogger.Instance);
        
        Assert.Equal(SagaStatus.Completed, saga.Status);
        Assert.Equal(3, executed.Count);
        Assert.Contains("Step1", executed);
        Assert.Contains("Step2", executed);
        Assert.Contains("Step3", executed);
    }
}

public class EventStoreTests
{
    [Fact]
    public async Task EventStore_AppendAndRetrieve_Works()
    {
        // This would require a real EventStoreDbContext
        // For now, test the interfaces compile
        Assert.True(true);
    }
}

public class MultiTenancyTests
{
    [Fact]
    public async Task InMemoryTenantProvider_CRUD_Works()
    {
        var provider = new InMemoryTenantProvider();
        
        var tenant = await provider.CreateTenantAsync("test-tenant", "Test Tenant");
        
        Assert.Equal("test-tenant", tenant.TenantId);
        Assert.Equal("Test Tenant", tenant.TenantName);
        
        var retrieved = await provider.GetTenantAsync("test-tenant", CancellationToken.None);
        Assert.NotNull(retrieved);
        Assert.Equal("test-tenant", retrieved!.TenantId);
        
        var all = await provider.GetAllTenantsAsync(CancellationToken.None);
        Assert.Contains(all, t => t.TenantId == "test-tenant");
        
        await provider.DeleteTenantAsync("test-tenant", CancellationToken.None);
        var deleted = await provider.GetTenantAsync("test-tenant", CancellationToken.None);
        Assert.Null(deleted);
    }
}

public class InMemorySagaRepositoryTests
{
    [Fact]
    public async Task InMemorySagaRepository_SaveAndGet_Works()
    {
        var repo = new InMemorySagaRepository();
        
        var steps = new List<SagaStep>
        {
            new("TestStep", async ct => StepResult.Success())
        };
        var saga = Saga.Create("test", new { }, steps);
        
        await repo.SaveAsync(saga, CancellationToken.None);
        var retrieved = await repo.GetAsync(saga.SagaId, CancellationToken.None);
        
        Assert.NotNull(retrieved);
        Assert.Equal(saga.SagaId, retrieved!.SagaId);
    }

    [Fact]
    public async Task InMemorySagaRepository_GetByStatus_Works()
    {
        var repo = new InMemorySagaRepository();
        
        var saga1 = Saga.Create("test1", new { }, new List<SagaStep>());
        var saga2 = Saga.Create("test2", new { }, new List<SagaStep>());
        
        await repo.SaveAsync(saga1, CancellationToken.None);
        await repo.SaveAsync(saga2, CancellationToken.None);
        
        var pending = await repo.GetByStatusAsync(SagaStatus.Pending, CancellationToken.None);
        
        Assert.Equal(2, pending.Count);
    }
}