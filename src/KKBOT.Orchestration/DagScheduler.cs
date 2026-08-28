// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KKBOT.Orchestration;

/// <summary>
/// Schedules jobs based on a directed‑acyclic graph (DAG) of dependencies.
/// Each job is represented by a <see cref="DagJob"/> which contains the job id and its dependent ids.
/// The scheduler executes jobs as soon as all their dependencies have completed.
/// </summary>
public sealed class DagScheduler
{
    private readonly ILogger<DagScheduler> _logger;
    private readonly EventStore _eventStore;

    public DagScheduler(ILogger<DagScheduler> logger, EventStore eventStore)
    {
        _logger = logger;
        _eventStore = eventStore;
    }

    /// <summary>
    /// Runs the provided jobs respecting their dependency graph.
    /// </summary>
    /// <param name="jobs">List of jobs with dependencies.</param>
    /// <param name="runJobAsync">Callback that runs a single job and returns true on success.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public async Task RunAsync(IReadOnlyList<DagJob> jobs, Func<DagJob, CancellationToken, Task<bool>> runJobAsync, CancellationToken cancellationToken)
    {
        // Topological sort to detect cycles and order execution.
        var sorted = TopologicalSort(jobs);
        var completed = new HashSet<Guid>();

        foreach (var job in sorted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Wait until all dependencies are completed.
            while (!job.Dependencies.All(d => completed.Contains(d)))
            {
                await Task.Delay(50, cancellationToken);
            }

            _logger.LogInformation("Starting DAG job {JobId}", job.JobId);
            await _eventStore.Append(new JobStartedEvent(Guid.Empty, job.JobId)); // InstanceId empty for generic usage.
            var success = await runJobAsync(job, cancellationToken);
            completed.Add(job.JobId);
            await _eventStore.Append(new JobCompletedEvent(Guid.Empty, job.JobId, success));
            _logger.LogInformation("Job {JobId} completed with success={Success}", job.JobId, success);
        }
    }

    private static List<DagJob> TopologicalSort(IReadOnlyList<DagJob> jobs)
    {
        var graph = jobs.ToDictionary(j => j.JobId, j => j.Dependencies.ToList());
        var result = new List<DagJob>();
        var visited = new HashSet<Guid>();
        var tempMark = new HashSet<Guid>();

        void Visit(Guid id)
        {
            if (tempMark.Contains(id))
                throw new InvalidOperationException("Cyclic dependency detected in DAG jobs.");
            if (!visited.Contains(id))
            {
                tempMark.Add(id);
                foreach (var dep in graph[id])
                    Visit(dep);
                tempMark.Remove(id);
                visited.Add(id);
                var job = jobs.First(j => j.JobId == id);
                result.Add(job);
            }
        }

        foreach (var job in jobs)
            Visit(job.JobId);

        return result;
    }
}

public sealed record DagJob(Guid JobId, IReadOnlyCollection<Guid> Dependencies);
