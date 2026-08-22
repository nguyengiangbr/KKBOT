// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace KKBOT.Marketplace.Server.Models;

public sealed class MarketplaceDbContext : DbContext
{
    public DbSet<PluginPackage> PluginPackages => Set<PluginPackage>();
    public DbSet<PluginVersion> PluginVersions => Set<PluginVersion>();
    public DbSet<PluginReview> PluginReviews => Set<PluginReview>();
    public DbSet<PluginDownload> PluginDownloads => Set<PluginDownload>();
    public DbSet<PluginDependency> PluginDependencies => Set<PluginDependency>();
    public DbSet<Publisher> Publishers => Set<Publisher>();
    public DbSet<PublisherPayout> PublisherPayouts => Set<PublisherPayout>();
    public DbSet<ModerationQueue> ModerationQueue => Set<ModerationQueue>();
    public DbSet<PluginTag> PluginTags => Set<PluginTag>();
    public DbSet<PluginCategory> PluginCategories => Set<PluginCategory>();
    public DbSet<License> Licenses => Set<License>();
    public DbSet<PluginSignature> PluginSignatures => Set<PluginSignature>();
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();
    public DbSet<SecurityScan> SecurityScans => Set<SecurityScan>();

    public MarketplaceDbContext(DbContextOptions<MarketplaceDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // PluginPackage
        modelBuilder.Entity<PluginPackage>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.PluginId).IsUnique();
            e.HasIndex(p => p.Status);
            e.HasIndex(p => p.CreatedAt);
            e.Property(p => p.PluginId).HasMaxLength(100).IsRequired();
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.Property(p => p.Description).HasColumnType("TEXT");
            e.Property(p => p.RepositoryUrl).HasMaxLength(500);
            e.Property(p => p.HomepageUrl).HasMaxLength(500);
            e.Property(p => p.IconUrl).HasMaxLength(500);
            e.Property(p => p.LicenseId).HasMaxLength(50);
            e.Property(p => p.Visibility).HasMaxLength(20).HasDefaultValue("private");
            e.OwnsOne(p => p.Metadata, m =>
            {
                m.Property(x => x.Tags).HasColumnType("TEXT");
                m.Property(x => x.SupportedPlatforms).HasColumnType("TEXT");
                m.Property(x => x.SupportedResolutions).HasColumnType("TEXT");
            });
        });

        // PluginVersion
        modelBuilder.Entity<PluginVersion>(e =>
        {
            e.HasKey(v => v.Id);
            e.HasIndex(v => new { v.PluginPackageId, v.Version }).IsUnique();
            e.HasIndex(v => v.Status);
            e.HasIndex(v => v.PublishedAt);
            e.Property(v => v.Version).HasMaxLength(50).IsRequired();
            e.Property(v => v.Changelog).HasColumnType("TEXT");
            e.Property(v => v.DownloadUrl).HasMaxLength(500);
            e.Property(v => v.FileHash).HasMaxLength(128).IsRequired();
            e.Property(v => v.FileSize).IsRequired();
            e.Property(v => v.MinEngineVersion).HasMaxLength(50);
            e.Property(v => v.MaxEngineVersion).HasMaxLength(50);
            e.Property(v => v.ManifestJson).HasColumnType("TEXT").IsRequired();
            e.HasOne(v => v.PluginPackage)
                .WithMany(p => p.Versions)
                .HasForeignKey(v => v.PluginPackageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PluginReview
        modelBuilder.Entity<PluginReview>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => new { r.PluginPackageId, r.UserId });
            e.HasIndex(r => r.CreatedAt);
            e.Property(r => r.Title).HasMaxLength(200);
            e.Property(r => r.Content).HasColumnType("TEXT");
            e.Property(r => r.UserId).HasMaxLength(100).IsRequired();
            e.Property(r => r.UserName).HasMaxLength(200);
            e.HasOne(r => r.PluginPackage)
                .WithMany(p => p.Reviews)
                .HasForeignKey(r => r.PluginPackageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PluginDownload
        modelBuilder.Entity<PluginDownload>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasIndex(d => d.PluginVersionId);
            e.HasIndex(d => d.UserId);
            e.HasIndex(d => d.DownloadedAt);
            e.Property(d => d.UserId).HasMaxLength(100);
            e.Property(d => d.UserAgent).HasMaxLength(500);
            e.Property(d => d.IpAddress).HasMaxLength(45);
            e.HasOne(d => d.PluginVersion)
                .WithMany(v => v.Downloads)
                .HasForeignKey(d => d.PluginVersionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PluginDependency
        modelBuilder.Entity<PluginDependency>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasIndex(d => new { d.PluginVersionId, d.DependencyPluginId });
            e.Property(d => d.DependencyPluginId).HasMaxLength(100).IsRequired();
            e.Property(d => d.VersionRange).HasMaxLength(100).IsRequired();
            e.Property(d => d.DependencyType).HasMaxLength(20).HasDefaultValue("required");
            e.HasOne(d => d.PluginVersion)
                .WithMany(v => v.Dependencies)
                .HasForeignKey(d => d.PluginVersionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Publisher
        modelBuilder.Entity<Publisher>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.UserId).IsUnique();
            e.HasIndex(p => p.Slug).IsUnique();
            e.Property(p => p.UserId).HasMaxLength(100).IsRequired();
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.Property(p => p.Slug).HasMaxLength(100).IsRequired();
            e.Property(p => p.Description).HasColumnType("TEXT");
            e.Property(p => p.LogoUrl).HasMaxLength(500);
            e.Property(p => p.WebsiteUrl).HasMaxLength(500);
            e.Property(p => p.Email).HasMaxLength(200);
            e.Property(p => p.VerificationStatus).HasMaxLength(20).HasDefaultValue("pending");
            e.Property(p => p.RevenueSharePercent).HasDefaultValue(70);
            e.Property(p => p.PayoutEmail).HasMaxLength(200);
        });

        // PublisherPayout
        modelBuilder.Entity<PublisherPayout>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.PublisherId);
            e.HasIndex(p => p.Status);
            e.HasIndex(p => p.CreatedAt);
            e.Property(p => p.Amount).HasPrecision(18, 2);
            e.Property(p => p.Currency).HasMaxLength(3).HasDefaultValue("USD");
            e.Property(p => p.Status).HasMaxLength(20).HasDefaultValue("pending");
            e.Property(p => p.PayoutMethod).HasMaxLength(50);
            e.Property(p => p.TransactionId).HasMaxLength(200);
            e.HasOne(p => p.Publisher)
                .WithMany(pub => pub.Payouts)
                .HasForeignKey(p => p.PublisherId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ModerationQueue
        modelBuilder.Entity<ModerationQueue>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => m.Status);
            e.HasIndex(m => m.SubmittedAt);
            e.HasIndex(m => m.Priority);
            e.Property(m => m.EntityType).HasMaxLength(50).IsRequired();
            e.Property(m => m.EntityId).HasMaxLength(100).IsRequired();
            e.Property(m => m.SubmitterId).HasMaxLength(100).IsRequired();
            e.Property(m => m.Reason).HasColumnType("TEXT");
            e.Property(m => m.ModeratorNotes).HasColumnType("TEXT");
            e.Property(m => m.Status).HasMaxLength(20).HasDefaultValue("pending");
            e.Property(m => m.Priority).HasMaxLength(20).HasDefaultValue("normal");
        });

        // PluginTag
        modelBuilder.Entity<PluginTag>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => t.Name).IsUnique();
            e.Property(t => t.Name).HasMaxLength(50).IsRequired();
            e.Property(t => t.Description).HasMaxLength(200);
            e.Property(t => t.Color).HasMaxLength(7).HasDefaultValue("#1976D2");
        });

        // PluginCategory
        modelBuilder.Entity<PluginCategory>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.Name).IsUnique();
            e.Property(c => c.Name).HasMaxLength(100).IsRequired();
            e.Property(c => c.Description).HasMaxLength(500);
            e.Property(c => c.Icon).HasMaxLength(100);
            e.Property(c => c.ParentId);
        });

        // License
        modelBuilder.Entity<License>(e =>
        {
            e.HasKey(l => l.Id);
            e.HasIndex(l => l.SpdxId).IsUnique();
            e.Property(l => l.SpdxId).HasMaxLength(50).IsRequired();
            e.Property(l => l.Name).HasMaxLength(200).IsRequired();
            e.Property(l => l.Url).HasMaxLength(500);
            e.Property(l => l.FullText).HasColumnType("TEXT");
            e.Property(l => l.IsOsiApproved).HasDefaultValue(false);
        });

        // PluginSignature
        modelBuilder.Entity<PluginSignature>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.PluginVersionId).IsUnique();
            e.Property(s => s.Algorithm).HasMaxLength(50).IsRequired();
            e.Property(s => s.PublicKey).HasColumnType("TEXT").IsRequired();
            e.Property(s => s.Signature).HasColumnType("TEXT").IsRequired();
            e.Property(s => s.Certificate).HasColumnType("TEXT");
            e.Property(s => s.SignedAt).IsRequired();
            e.Property(s => s.SignerId).HasMaxLength(100).IsRequired();
            e.HasOne(s => s.PluginVersion)
                .WithOne(v => v.Signature)
                .HasForeignKey<PluginSignature>(s => s.PluginVersionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // WebhookSubscription
        modelBuilder.Entity<WebhookSubscription>(e =>
        {
            e.HasKey(w => w.Id);
            e.HasIndex(w => w.UserId);
            e.HasIndex(w => w.EventTypes);
            e.Property(w => w.UserId).HasMaxLength(100).IsRequired();
            e.Property(w => w.Url).HasMaxLength(500).IsRequired();
            e.Property(w => w.Secret).HasMaxLength(200);
            e.Property(w => w.EventTypes).HasColumnType("TEXT").IsRequired(); // JSON array
            e.Property(w => w.IsActive).HasDefaultValue(true);
            e.Property(w => w.RetryCount).HasDefaultValue(0);
        });

        // WebhookDelivery
        modelBuilder.Entity<WebhookDelivery>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasIndex(d => d.WebhookSubscriptionId);
            e.HasIndex(d => d.EventType);
            e.HasIndex(d => d.CreatedAt);
            e.Property(d => d.EventType).HasMaxLength(100).IsRequired();
            e.Property(d => d.Payload).HasColumnType("TEXT");
            e.Property(d => d.ResponseStatus).HasMaxLength(10);
            e.Property(d => d.ResponseBody).HasColumnType("TEXT");
            e.Property(d => d.AttemptCount).HasDefaultValue(0);
            e.Property(d => d.ErrorMessage).HasColumnType("TEXT");
            e.HasOne(d => d.WebhookSubscription)
                .WithMany(w => w.Deliveries)
                .HasForeignKey(d => d.WebhookSubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // SecurityScan
        modelBuilder.Entity<SecurityScan>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => new { s.PluginId, s.Version });
            e.HasIndex(s => s.ScannedAt);
            e.HasIndex(s => s.Status);
            e.Property(s => s.PluginId).HasMaxLength(100).IsRequired();
            e.Property(s => s.Version).HasMaxLength(50).IsRequired();
            e.Property(s => s.FindingsJson).HasColumnType("TEXT");
            e.Property(s => s.PermissionsJson).HasColumnType("TEXT");
            e.Property(s => s.DependencyVulnerabilitiesJson).HasColumnType("TEXT");
            e.Property(s => s.CodeQualityJson).HasColumnType("TEXT");
            e.Property(s => s.BehaviorJson).HasColumnType("TEXT");
        });

        // Seed default licenses
        modelBuilder.Entity<License>().HasData(
            new License { Id = 1, SpdxId = "MIT", Name = "MIT License", Url = "https://opensource.org/licenses/MIT", IsOsiApproved = true },
            new License { Id = 2, SpdxId = "Apache-2.0", Name = "Apache License 2.0", Url = "https://www.apache.org/licenses/LICENSE-2.0", IsOsiApproved = true },
            new License { Id = 3, SpdxId = "GPL-3.0", Name = "GNU General Public License v3.0", Url = "https://www.gnu.org/licenses/gpl-3.0.html", IsOsiApproved = true },
            new License { Id = 4, SpdxId = "BSD-3-Clause", Name = "BSD 3-Clause License", Url = "https://opensource.org/licenses/BSD-3-Clause", IsOsiApproved = true },
            new License { Id = 5, SpdxId = "Proprietary", Name = "Proprietary License", Url = "", IsOsiApproved = false }
        );

        // Seed default categories
        modelBuilder.Entity<PluginCategory>().HasData(
            new PluginCategory { Id = 1, Name = "Game Automation", Description = "Plugins for automating specific games", Icon = "gamepad-variant", SortOrder = 1 },
            new PluginCategory { Id = 2, Name = "AI & ML", Description = "AI models and machine learning plugins", Icon = "brain", SortOrder = 2 },
            new PluginCategory { Id = 3, Name = "Computer Vision", Description = "OpenCV, OCR, and vision plugins", Icon = "eye", SortOrder = 3 },
            new PluginCategory { Id = 4, Name = "Device Control", Description = "ADB, input, and device management", Icon = "cellphone-android", SortOrder = 4 },
            new PluginCategory { Id = 5, Name = "Utility", Description = "General utility and helper plugins", Icon = "tools", SortOrder = 5 },
            new PluginCategory { Id = 6, Name = "Scripting", Description = "Visual scripting and workflow plugins", Icon = "script-text", SortOrder = 6 }
        );
    }
}

// Core Entities

public sealed class PluginPackage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PluginId { get; set; } = string.Empty; // Unique identifier like "com.author.plugin"
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RepositoryUrl { get; set; } = string.Empty;
    public string HomepageUrl { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public string LicenseId { get; set; } = string.Empty;
    public string Visibility { get; set; } = "private"; // private, public, unlisted
    public PluginMetadata Metadata { get; set; } = new();
    public Guid PublisherId { get; set; }
    public Publisher Publisher { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    public PluginStatus Status { get; set; } = PluginStatus.Draft;
    public long DownloadCount { get; set; } = 0;
    public double AverageRating { get; set; } = 0;
    public int ReviewCount { get; set; } = 0;

    // Navigation
    public ICollection<PluginVersion> Versions { get; set; } = new List<PluginVersion>();
    public ICollection<PluginReview> Reviews { get; set; } = new List<PluginReview>();
}

public sealed class PluginMetadata
{
    public string[] Tags { get; set; } = Array.Empty<string>();
    public string[] SupportedPlatforms { get; set; } = Array.Empty<string>();
    public Resolution[] SupportedResolutions { get; set; } = Array.Empty<Resolution>();
    public string[] RequiredPermissions { get; set; } = Array.Empty<string>();
    public Dictionary<string, string> CustomProperties { get; set; } = new();
}

public sealed class Resolution
{
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed class PluginVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PluginPackageId { get; set; }
    public PluginPackage PluginPackage { get; set; } = null!;
    public string Version { get; set; } = string.Empty;
    public string Changelog { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty; // SHA256
    public long FileSize { get; set; }
    public string MinEngineVersion { get; set; } = string.Empty;
    public string MaxEngineVersion { get; set; } = string.Empty;
    public string ManifestJson { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    public VersionStatus Status { get; set; } = VersionStatus.Pending;
    public string? DeprecationNotice { get; set; }
    public DateTime? DeprecatedAt { get; set; }

    // Navigation
    public ICollection<PluginDependency> Dependencies { get; set; } = new List<PluginDependency>();
    public ICollection<PluginDownload> Downloads { get; set; } = new List<PluginDownload>();
    public PluginSignature? Signature { get; set; }
}

public sealed class PluginDependency
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PluginVersionId { get; set; }
    public PluginVersion PluginVersion { get; set; } = null!;
    public string DependencyPluginId { get; set; } = string.Empty;
    public string VersionRange { get; set; } = string.Empty; // e.g., ">=1.0.0 <2.0.0"
    public string DependencyType { get; set; } = "required"; // required, optional, dev
}

public sealed class PluginReview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PluginPackageId { get; set; }
    public PluginPackage PluginPackage { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public int Rating { get; set; } // 1-5
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsVerifiedPurchase { get; set; } = false;
    public bool IsEdited { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsFlagged { get; set; } = false;
    public string? FlagReason { get; set; }
}

public sealed class PluginDownload
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PluginVersionId { get; set; }
    public PluginVersion PluginVersion { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public DateTime DownloadedAt { get; set; } = DateTime.UtcNow;
    public string Referrer { get; set; } = string.Empty;
}

public sealed class Publisher
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string LogoUrl { get; set; } = string.Empty;
    public string WebsiteUrl { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string VerificationStatus { get; set; } = "pending"; // pending, verified, rejected
    public int RevenueSharePercent { get; set; } = 70;
    public string PayoutEmail { get; set; } = string.Empty;
    public string PayoutMethod { get; set; } = string.Empty; // stripe, paypal, bank
    public string PayoutAccountId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? VerifiedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<PluginPackage> Packages { get; set; } = new List<PluginPackage>();
    public ICollection<PublisherPayout> Payouts { get; set; } = new List<PublisherPayout>();
}

public sealed class PublisherPayout
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PublisherId { get; set; }
    public Publisher Publisher { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public PayoutStatus Status { get; set; } = PayoutStatus.Pending;
    public string PayoutMethod { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? FailureReason { get; set; }
    public string PeriodStart { get; set; } = string.Empty;
    public string PeriodEnd { get; set; } = string.Empty;
    public long TotalDownloads { get; set; } = 0;
    public decimal GrossRevenue { get; set; }
    public decimal PlatformFee { get; set; }
    public decimal NetAmount { get; set; }
}

public sealed class ModerationQueue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EntityType { get; set; } = string.Empty; // package, version, review, publisher
    public string EntityId { get; set; } = string.Empty;
    public string SubmitterId { get; set; } = string.Empty;
    public string SubmitterName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string ModeratorNotes { get; set; } = string.Empty;
    public ModerationStatus Status { get; set; } = ModerationStatus.Pending;
    public ModerationPriority Priority { get; set; } = ModerationPriority.Normal;
    public string ModeratorId { get; set; } = string.Empty;
    public string ModeratorName { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public sealed class PluginTag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Color { get; set; } = "#1976D2";
    public int UsageCount { get; set; } = 0;
    public bool IsSystemTag { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class PluginCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public PluginCategory? Parent { get; set; }
    public ICollection<PluginCategory> Children { get; set; } = new List<PluginCategory>();
    public int SortOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class License
{
    public int Id { get; set; }
    public string SpdxId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string FullText { get; set; } = string.Empty;
    public bool IsOsiApproved { get; set; } = false;
    public bool IsDeprecated { get; set; } = false;
}

public sealed class PluginSignature
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PluginVersionId { get; set; }
    public PluginVersion PluginVersion { get; set; } = null!;
    public string Algorithm { get; set; } = string.Empty; // e.g., "RSA-SHA256", "ECDSA-P256"
    public string PublicKey { get; set; } = string.Empty; // PEM format
    public string Signature { get; set; } = string.Empty; // Base64
    public string Certificate { get; set; } = string.Empty; // PEM format, optional
    public DateTime SignedAt { get; set; } = DateTime.UtcNow;
    public string SignerId { get; set; } = string.Empty;
    public string SignerName { get; set; } = string.Empty;
    public bool IsVerified { get; set; } = false;
    public DateTime? VerifiedAt { get; set; }
}

public sealed class WebhookSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public string EventTypes { get; set; } = "[]"; // JSON array
    public bool IsActive { get; set; } = true;
    public int RetryCount { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastTriggeredAt { get; set; }
    public int FailureCount { get; set; } = 0
    public ICollection<WebhookDelivery> Deliveries { get; set; } = new List<WebhookDelivery>();
}

public sealed class WebhookDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WebhookSubscriptionId { get; set; }
    public WebhookSubscription WebhookSubscription { get; set; } = null!;
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public int ResponseStatus { get; set; }
    public string ResponseBody { get; set; } = string.Empty;
    public int AttemptCount { get; set; } = 0;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAt { get; set; }
}

public sealed class SecurityScan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PluginId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    public SecurityStatus Status { get; set; } = SecurityStatus.Passed;
    public SecurityRiskLevel RiskLevel { get; set; } = SecurityRiskLevel.None;
    public string FindingsJson { get; set; } = "[]";
    public string PermissionsJson { get; set; } = "[]";
    public string DependencyVulnerabilitiesJson { get; set; } = "[]";
    public string CodeQualityJson { get; set; } = "{}";
    public string BehaviorJson { get; set; } = "{}";
    public long ScanDurationMs { get; set; }
    public string ScannerVersion { get; set; } = "1.0.0";
}

// Enums

public enum PluginStatus
{
    Draft,
    PendingReview,
    Approved,
    Rejected,
    Published,
    Unlisted,
    Deprecated,
    Removed
}

public enum VersionStatus
{
    Pending,
    PendingReview,
    Approved,
    Rejected,
    Published,
    Deprecated,
    Removed
}

public enum PluginStatusEnum
{
    Draft,
    Published,
    Deprecated,
    Removed
}

public enum ModerationStatus
{
    Pending,
    UnderReview,
    Approved,
    Rejected,
    Escalated
}

public enum ModerationPriority
{
    Low,
    Normal,
    High,
    Critical
}

public enum PayoutStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Cancelled
}