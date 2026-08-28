using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Observability.Metrics;
using KKBOT.Orchestration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KKBOT.Orchestration.Tests
{
    public class DagSchedulerTests
    {
        [Fact]
        public async Task RunAsync_ExecutesJobsRespectingDependencies()
        {
            // Arrange: three jobs A, B depends on A, C depends on B
            var jobA = new DagJob(Guid.NewGuid(), Array.Empty<Guid>());
            var jobB = new DagJob(Guid.NewGuid(), new[] { jobA.JobId });
            var jobC = new DagJob(Guid.NewGuid(), new[] { jobB.JobId });
            var jobs = new List<DagJob> { jobA, jobB, jobC };

            var executed = new List<Guid>();
            var scheduler = new DagScheduler(NullLogger<DagScheduler>.Instance, new EventStore(NullLogger<EventStore>.Instance, Options.Create(new EventStoreOptions()), new MetricsRegistry()));

            // Act
            await scheduler.RunAsync(jobs, async (job, ct) =>
            {
                executed.Add(job.JobId);
                await Task.Delay(10, ct);
                return true;
            }, CancellationToken.None);

            // Assert: order should be A, B, C
            Assert.Equal(3, executed.Count);
            Assert.Equal(jobA.JobId, executed[0]);
            Assert.Equal(jobB.JobId, executed[1]);
            Assert.Equal(jobC.JobId, executed[2]);
        }
    }
}
