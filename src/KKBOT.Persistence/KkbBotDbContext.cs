using Microsoft.EntityFrameworkCore;
using KKBOT.Domain.Models;

namespace KKBOT.Persistence;

/// <summary>
/// EF Core DB context for KKBOT persistence.
/// </summary>
public sealed class KkbBotDbContext : DbContext
{
    public KkbBotDbContext(DbContextOptions<KkbBotDbContext> options) : base(options) { }

    public DbSet<Instance> Instances => Set<Instance>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Module> Modules => Set<Module>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobRun> JobRuns => Set<JobRun>();
    public DbSet<TaskRun> TaskRuns => Set<TaskRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply configurations from separate files if present
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KkbBotDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}