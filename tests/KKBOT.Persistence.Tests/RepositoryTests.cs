using System;
using System.Threading.Tasks;
using KKBOT.Domain.Enums;
using KKBOT.Domain.Models;
using KKBOT.Persistence.Abstractions;
using KKBOT.Persistence.EfCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KKBOT.Persistence.Tests
{
    public class EfRepositoryTests
    {
        private static SqliteConnection? _activeConnection;

        private static ServiceProvider CreateProvider()
        {
            var services = new ServiceCollection();
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            _activeConnection = connection;
            services.AddDbContext<KkbotDbContext>(options => options.UseSqlite(connection));
            services.AddScoped<IInstanceRepository, EfInstanceRepository>();
            services.AddScoped<IJobRepository, EfJobRepository>();
            var provider = services.BuildServiceProvider();
            // Ensure database created on the shared, still-open connection
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KkbotDbContext>();
            db.Database.EnsureCreated();
            return provider;
        }

        [Fact]
        public async Task InstanceRepository_CRUD_Works()
        {
            var provider = CreateProvider();
            using var scope = provider.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IInstanceRepository>();

            var instance = new Instance
            {
                Key = "test-instance",
                DisplayName = "Test Instance",
                State = InstanceState.Created
            };
            await repo.AddAsync(instance, CancellationToken.None);

            var fetched = await repo.GetAsync(instance.Id, CancellationToken.None);
            Assert.NotNull(fetched);
            Assert.Equal("Test Instance", fetched!.DisplayName);

            fetched.DisplayName = "Updated";
            await repo.UpdateAsync(fetched, CancellationToken.None);
            var updated = await repo.GetAsync(instance.Id, CancellationToken.None);
            Assert.Equal("Updated", updated!.DisplayName);

            await repo.DeleteAsync(instance.Id, CancellationToken.None);
            var deleted = await repo.GetAsync(instance.Id, CancellationToken.None);
            Assert.Null(deleted);
        }

        [Fact]
        public async Task JobRepository_CRUD_Works()
        {
            var provider = CreateProvider();
            using var scope = provider.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IJobRepository>();
            var db = scope.ServiceProvider.GetRequiredService<KkbotDbContext>();

            var module = new Module
            {
                Key = "test-module",
                DisplayName = "Test Module",
                Version = "1.0.0"
            };
            db.Modules.Add(module);
            await db.SaveChangesAsync(CancellationToken.None);

            var job = new Job
            {
                Key = "test-job",
                DisplayName = "Test Job",
                ModuleId = module.Id,
                TaskId = "task-1",
                Parameters = "{}"
            };
            await repo.AddAsync(job, CancellationToken.None);

            var fetched = await repo.GetAsync(job.Id, CancellationToken.None);
            Assert.NotNull(fetched);
            Assert.Equal("Test Job", fetched!.DisplayName);

            fetched.DisplayName = "Updated";
            await repo.UpdateAsync(fetched, CancellationToken.None);
            var updated = await repo.GetAsync(job.Id, CancellationToken.None);
            Assert.Equal("Updated", updated!.DisplayName);

            await repo.DeleteAsync(job.Id, CancellationToken.None);
            var deleted = await repo.GetAsync(job.Id, CancellationToken.None);
            Assert.Null(deleted);
        }
    }
}
