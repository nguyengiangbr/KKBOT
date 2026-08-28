// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Domain.Models;
using KKBOT.Plugins.Abstractions;
using KKBOT.Plugins.Loader;
using Microsoft.Extensions.Logging;

namespace KKBOT.Orchestration;

/// <summary>
/// Orchestrates a single KKBOT instance: loads its plugin, builds a state machine, schedules tasks via DAG.
/// </summary>
public sealed class InstanceOrchestrator
{
    private readonly PluginLoader _pluginLoader;
    private readonly DagScheduler _dagScheduler;
    private readonly ILogger<InstanceOrchestrator> _logger;
    private readonly EventStore _eventStore;

    public InstanceOrchestrator(PluginLoader pluginLoader, DagScheduler dagScheduler, ILogger<InstanceOrchestrator> logger, EventStore eventStore)
    {
        _pluginLoader = pluginLoader;
        _dagScheduler = dagScheduler;
        _logger = logger;
        _eventStore = eventStore;
    }

    /// <summary>
    /// Starts orchestration for the given instance using plugins located in <paramref name="pluginsPath"/>.
    /// </summary>
    public async Task RunInstanceAsync(Instance instance, string pluginsPath, CancellationToken cancellationToken)
    {
        await _eventStore.Append(new InstanceCreatedEvent(instance.Id));
        _logger.LogInformation("Orchestrating instance {InstanceId}", instance.Id);

        // Load plugins (expect exactly one matching the instance's ModuleId)
        var modules = await _pluginLoader.LoadAllPluginsAsync(cancellationToken);
        var module = modules.FirstOrDefault(m => string.Equals(m.Id?.ToString(), instance.Module?.Id.ToString(), StringComparison.OrdinalIgnoreCase));
        if (module == null)
        {
            _logger.LogWarning("No plugin found for instance {InstanceId} (module id {ModuleId}).", instance.Id, instance.Module?.Id);
            return;
        }

        // Build a simple DAG of tasks based on the module's tasks.
        var dagJobs = module.GetTasks()
            .Select(t => new DagJob(Guid.Parse(t.Id), Array.Empty<Guid>())) // For demo, no dependencies.
            .ToList();

        // Run DAG jobs – each job just logs the task execution.
        await _dagScheduler.RunAsync(dagJobs, async (job, ct) =>
        {
            _logger.LogInformation("Executing task {TaskId} for instance {InstanceId}", job.JobId, instance.Id);
            // Simulate task work.
            await Task.Delay(100, ct);
            return true;
        }, cancellationToken);

        // Build FSM from module states (simplified: just log entry order).
        foreach (var state in module.GetStates())
        {
            await _eventStore.Append(new StateEnteredEvent(instance.Id, state.Id));
            _logger.LogInformation("Instance {InstanceId} entered state {StateId}", instance.Id, state.Id);
        }
    }
}
