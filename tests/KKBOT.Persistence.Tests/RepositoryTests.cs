using System;
using System.Threading.Tasks;
using KKBOT.Domain.Models;
using KKBOT.Persistence.Abstractions;
using KKBOT.Persistence.EfCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KKBOT.Persistence.Tests;
{
    public class EfRepositoryTests
    {
        private static ServiceProvider CreateProvider()
        {
            var services = new ServiceCollection();
            services.AddDbContext<KkbotDbContext>(options => options.UseSqlite("DataSource=:memory:"));
            services.AddScoped<IInstanceRepository, EfInstanceRepository>();
            services.AddScoped<IJobRepository, EfJobRepository>();
            var provider = services.BuildServiceProvider();
            // Ensure database created
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KkbotDbContext>();
            db.Database.OpenConnection();
            db.Database.EnsureCreated();
            return provider;
        }

        [Fact]
        public async Task InstanceRepository_CRUD_Works()
        {
            var provider = CreateProvider();
            using var scope = provider.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IInstanceRepository>();

            var instance = new Instance(Guid.NewGuid(), "Test Instance", InstanceStatus.Created, null);
            await repo.AddAsync(instance, CancellationToken.None);

            var fetched = await repo.GetAsync(instance.Id, CancellationToken.None);
            Assert.NotNull(fetched);
            Assert.Equal("Test Instance", fetched!.Name);

            instance = instance with { Name = "Updated" };
            await repo.UpdateAsync(instance, CancellationToken.None);
            fetched = await repo.GetAsync(instance.Id, CancellationToken.None);
            Assert.Equal("Updated", fetched!.Name);

            await repo.DeleteAsync(instance.Id, CancellationToken.None);
            fetched = await repo.GetAsync(instance.Id, CancellationToken.None);
            Assert.Null(fetched);
        }

        [Fact]
        public async Task JobRepository_CRUD_Works()
        {
            var provider = CreateProvider();
            using var scope = provider.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IJobRepository>();

            var job = new Job(Guid.NewGuid(), Guid.NewGuid(), JobType.OneShot, JobStatus.Pending, "{}");
            await repo.AddAsync(job, CancellationToken.None);

            var fetched = await repo.GetAsync(job.Id, CancellationToken.None);
            Assert.NotNull(fetched);
            Assert.Equal(JobStatus.Pending, fetched!.Status);

            job = job with { Status = JobStatus.Running };
            await repo.UpdateAsync(job, CancellationToken.None);
            fetched = await repo.GetAsync(job.Id, CancellationToken.None);
            Assert.Equal(JobStatus.Running, fetched!.Status);

            await repo.DeleteAsync(job.Id, CancellationToken.None);
            fetched = await repo.GetAsync(job.Id, CancellationToken.None);
            Assert.Null(fetched);
        }
    }
}