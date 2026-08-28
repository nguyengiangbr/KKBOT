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
            e.Property(i => i.DisplayName).IsRequired().HasMaxLength(200);
            e.Property(i => i.State).HasConversion<string>();

            e.HasOne(i => i.Device)
                .WithMany(d => d.Instances)
                .HasForeignKey(i => i.DeviceId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(i => i.Module)
                .WithMany(m => m.Instances)
                .HasForeignKey(i => i.ModuleId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(i => i.Profile)
                .WithMany(p => p.Instances)
                .HasForeignKey(i => i.ProfileId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Device>(e =>
        {
            e.HasKey(d => d.Id);
            e.Property(d => d.DisplayName).IsRequired().HasMaxLength(200);
            e.HasOne(d => d.CurrentInstance)
                .WithMany()
                .HasForeignKey(d => d.CurrentInstanceId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Profile>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.DisplayName).IsRequired().HasMaxLength(200);
            e.HasOne(p => p.Module)
                .WithMany(m => m.Profiles)
                .HasForeignKey(p => p.ModuleId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Module>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.DisplayName).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<Job>(e =>
        {
            e.HasOne(j => j.Module)
                .WithMany()
                .HasForeignKey(j => j.ModuleId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Job>(e =>
        {
            e.HasKey(j => j.Id);
            e.Property(j => j.Key).IsRequired().HasMaxLength(100);
            e.Property(j => j.DisplayName).IsRequired().HasMaxLength(200);
            e.Property(j => j.TaskId).IsRequired().HasMaxLength(100);
            e.Property(j => j.Parameters).HasColumnType("TEXT");
        });

        modelBuilder.Entity<Module>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.DisplayName).IsRequired().HasMaxLength(200);
            e.Property(m => m.Version).IsRequired().HasMaxLength(50);
        });
    }
}