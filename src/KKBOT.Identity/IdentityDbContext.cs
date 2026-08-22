// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using KKBOT.Identity;

namespace KKBOT.Identity;

public sealed class IdentityDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public DbSet<ApplicationTenant> Tenants => Set<ApplicationTenant>();

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ApplicationUser
        builder.Entity<ApplicationUser>(e =>
        {
            e.ToTable("Users");
            e.Property(u => u.FirstName).HasMaxLength(100);
            e.Property(u => u.LastName).HasMaxLength(100);
            e.Property(u => u.AvatarUrl).HasMaxLength(500);
            e.Property(u => u.TimeZone).HasMaxLength(50);
            e.Property(u => u.Language).HasMaxLength(10);
            e.Property(u => u.PreferencesJson).HasColumnType("TEXT");
            e.HasIndex(u => u.TenantId);
            e.HasOne(u => u.Tenant)
                .WithMany(t => t.Users)
                .HasForeignKey(u => u.TenantId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ApplicationRole
        builder.Entity<ApplicationRole>(e =>
        {
            e.ToTable("Roles");
            e.Property(r => r.Description).HasMaxLength(500);
            e.Property(r => r.PermissionsJson).HasColumnType("TEXT");
        });

        // ApplicationTenant
        builder.Entity<ApplicationTenant>(e =>
        {
            e.ToTable("Tenants");
            e.HasKey(t => t.Id);
            e.Property(t => t.Name).HasMaxLength(100).IsRequired();
            e.Property(t => t.DisplayName).HasMaxLength(200).IsRequired();
            e.Property(t => t.Domain).HasMaxLength(200);
            e.Property(t => t.LogoUrl).HasMaxLength(500);
            e.Property(t => t.Settings).HasColumnType("TEXT");
            e.Property(t => t.Subscription).HasColumnType("TEXT");
            e.HasIndex(t => t.Name).IsUnique();
            e.HasIndex(t => t.Domain).IsUnique();
        });

        // Seed system roles
        builder.Entity<ApplicationRole>().HasData(
            new ApplicationRole
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Name = "SuperAdmin",
                NormalizedName = "SUPERADMIN",
                Description = "Full system access across all tenants",
                IsSystemRole = true,
                PermissionsJson = System.Text.Json.JsonSerializer.Serialize(KKBOTPermissions.RolePermissions["SuperAdmin"])
            },
            new ApplicationRole
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Name = "TenantAdmin",
                NormalizedName = "TENANTADMIN",
                Description = "Full access within tenant",
                IsSystemRole = true,
                PermissionsJson = System.Text.Json.JsonSerializer.Serialize(KKBOTPermissions.RolePermissions["TenantAdmin"])
            },
            new ApplicationRole
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                Name = "Operator",
                NormalizedName = "OPERATOR",
                Description = "Operate instances and jobs",
                IsSystemRole = true,
                PermissionsJson = System.Text.Json.JsonSerializer.Serialize(KKBOTPermissions.RolePermissions["Operator"])
            },
            new ApplicationRole
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000004"),
                Name = "Viewer",
                NormalizedName = "VIEWER",
                Description = "Read-only access",
                IsSystemRole = true,
                PermissionsJson = System.Text.Json.JsonSerializer.Serialize(KKBOTPermissions.RolePermissions["Viewer"])
            },
            new ApplicationRole
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000005"),
                Name = "Developer",
                NormalizedName = "DEVELOPER",
                Description = "Develop and test plugins",
                IsSystemRole = true,
                PermissionsJson = System.Text.Json.JsonSerializer.Serialize(KKBOTPermissions.RolePermissions["Developer"])
            }
        );
    }
}