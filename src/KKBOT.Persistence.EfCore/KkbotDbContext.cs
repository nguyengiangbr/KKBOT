// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.EntityFrameworkCore;
using KKBOT.Domain.Models;

namespace KKBOT.Persistence.EfCore;

public sealed class KkbotDbContext : DbContext
{
    public DbSet<Instance> Instances => Set<Instance>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<Module> Modules => Set<Module>();

    public KkbotDbContext(DbContextOptions<KkbotDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Instance>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.Name).IsRequired().HasMaxLength(200);
            e.Property(i => i.Status).HasConversion<string>();
            e.OwnsOne(i => i.Module, m =>
            {
                m.Property(p => p.Id).HasColumnName("ModuleId");
                m.Property(p => p.Name).HasColumnName("ModuleName");
                m.Property(p => p.Version).HasColumnName("ModuleVersion");
            });
        });

        modelBuilder.Entity<Job>(e =>
        {
            e.HasKey(j => j.Id);
            e.Property(j => j.InstanceId).IsRequired();
            e.Property(j => j.Type).HasConversion<string>();
            e.Property(j => j.Status).HasConversion<string>();
            e.Property(j => j.Payload).HasColumnType("TEXT");
        });

        modelBuilder.Entity<Module>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Name).IsRequired().HasMaxLength(200);
            e.Property(m => m.Version).IsRequired().HasMaxLength(50);
        });
    }
}