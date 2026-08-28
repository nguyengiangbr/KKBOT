// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Marketplace.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KKBOT.Marketplace.Server.Services;

public interface IMarketplaceService
{
    // Plugin Package Management
    Task<PluginPackage> PublishPluginAsync(PluginPublishRequest request, string publisherId, CancellationToken ct = default);
    Task<PluginPackage?> GetPluginAsync(string pluginId, CancellationToken ct = default);
    Task<PluginPackage?> GetPluginBySlugAsync(string slug, CancellationToken ct = default);
    Task<PagedResult<PluginPackage>> SearchPluginsAsync(PluginSearchQuery query, CancellationToken ct = default);
    Task<PluginPackage> UpdatePluginAsync(string pluginId, PluginUpdateRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeletePluginAsync(string pluginId, string userId, CancellationToken ct = default);
    Task<bool> TransferOwnershipAsync(string pluginId, string newOwnerId, string userId, CancellationToken ct = default);

    // Version Management
    Task<PluginVersion> CreateVersionAsync(string pluginId, CreateVersionRequest request, string userId, CancellationToken ct = default);
    Task<PluginVersion?> GetVersionAsync(string pluginId, string version, CancellationToken ct = default);
    Task<PagedResult<PluginVersion>> GetVersionsAsync(string pluginId, VersionQuery query, CancellationToken ct = default);
    Task<PluginVersion> UpdateVersionAsync(string pluginId, string version, UpdateVersionRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeprecateVersionAsync(string pluginId, string version, string deprecationNotice, string userId, CancellationToken ct = default);
    Task<bool> DeleteVersionAsync(string pluginId, string version, string userId, CancellationToken ct = default);

    // Download Tracking
    Task RecordDownloadAsync(Guid pluginVersionId, string userId, string userAgent, string ipAddress, CancellationToken ct = default);
    Task<DownloadStats> GetDownloadStatsAsync(string pluginId, CancellationToken ct = default);
    Task<string> GetDownloadUrlAsync(Guid pluginVersionId, CancellationToken ct = default);

    // Reviews
    Task<PluginReview> CreateReviewAsync(string pluginId, CreateReviewRequest request, string userId, CancellationToken ct = default);
    Task<PagedResult<PluginReview>> GetReviewsAsync(string pluginId, ReviewQuery query, CancellationToken ct = default);
    Task<PluginReview> UpdateReviewAsync(Guid reviewId, UpdateReviewRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeleteReviewAsync(Guid reviewId, string userId, CancellationToken ct = default);
    Task<bool> FlagReviewAsync(Guid reviewId, string reason, string userId, CancellationToken ct = default);

    // Publisher Management
    Task<Publisher> CreatePublisherAsync(CreatePublisherRequest request, string userId, CancellationToken ct = default);
    Task<Publisher?> GetPublisherAsync(Guid publisherId, CancellationToken ct = default);
    Task<Publisher?> GetPublisherByUserIdAsync(string userId, CancellationToken ct = default);
    Task<Publisher> UpdatePublisherAsync(Guid publisherId, UpdatePublisherRequest request, string userId, CancellationToken ct = default);
    Task<PagedResult<PublisherPayout>> GetPayoutsAsync(Guid publisherId, PayoutQuery query, CancellationToken ct = default);

    // Tags & Categories
    Task<IReadOnlyList<PluginTag>> GetPopularTagsAsync(int limit = 20, CancellationToken ct = default);
    Task<IReadOnlyList<PluginCategory>> GetCategoriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<License>> GetLicensesAsync(CancellationToken ct = default);

    // Moderation
    Task<ModerationQueue> SubmitForModerationAsync(SubmitModerationRequest request, CancellationToken ct = default);
    Task<PagedResult<ModerationQueue>> GetModerationQueueAsync(ModerationQuery query, CancellationToken ct = default);
    Task<ModerationQueue> ModerateItemAsync(Guid moderationId, ModerateRequest request, string moderatorId, CancellationToken ct = default);
}

public sealed class MarketplaceService : IMarketplaceService
{
    private readonly MarketplaceDbContext _dbContext;
    private readonly ILogger<MarketplaceService> _logger;
    private readonly IStorageService _storage;
    private readonly ISigningService _signing;

    public MarketplaceService(
        MarketplaceDbContext dbContext,
        ILogger<MarketplaceService> logger,
        IStorageService storage,
        ISigningService signing)
    {
        _dbContext = dbContext;
        _logger = logger;
        _storage = storage;
        _signing = signing;
    }

    public async Task<PluginPackage> PublishPluginAsync(PluginPublishRequest request, string publisherId, CancellationToken ct = default)
    {
        // Verify publisher exists and user owns it
        var publisher = await _dbContext.Publishers.FirstOrDefaultAsync(p => p.Id == Guid.Parse(publisherId), ct);
        if (publisher == null)
            throw new InvalidOperationException("Publisher not found");

        // Check if plugin ID already exists
        var existing = await _dbContext.PluginPackages.FirstOrDefaultAsync(p => p.PluginId == request.PluginId, ct);
        if (existing != null)
            throw new InvalidOperationException("Plugin ID already exists");

        var plugin = new PluginPackage
        {
            PluginId = request.PluginId,
            Name = request.Name,
            Description = request.Description,
            RepositoryUrl = request.RepositoryUrl,
            HomepageUrl = request.HomepageUrl,
            IconUrl = request.IconUrl,
            LicenseId = request.LicenseId,
            Visibility = "private",
            PublisherId = publisher.Id,
            Status = PluginStatus.Draft,
            Metadata = new PluginMetadata
            {
                Tags = request.Tags,
                SupportedPlatforms = request.SupportedPlatforms,
                SupportedResolutions = request.SupportedResolutions,
                RequiredPermissions = request.RequiredPermissions
            }
        };

        _dbContext.PluginPackages.Add(plugin);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created plugin package {PluginId} for publisher {PublisherId}", plugin.PluginId, publisher.Id);
        return plugin;
    }

    public async Task<PluginPackage?> GetPluginAsync(string pluginId, CancellationToken ct = default)
    {
        return await _dbContext.PluginPackages
            .Include(p => p.Publisher)
            .Include(p => p.Versions)
            .Include(p => p.Reviews)
            .FirstOrDefaultAsync(p => p.PluginId == pluginId, ct);
    }

    public async Task<PluginPackage?> GetPluginBySlugAsync(string slug, CancellationToken ct = default)
    {
        return await _dbContext.PluginPackages
            .Include(p => p.Publisher)
            .FirstOrDefaultAsync(p => p.PluginId == slug, ct);
    }

    public async Task<PagedResult<PluginPackage>> SearchPluginsAsync(PluginSearchQuery query, CancellationToken ct = default)
    {
        var q = _dbContext.PluginPackages
            .Include(p => p.Publisher)
            .Where(p => p.Visibility == "public" && p.Status == PluginStatus.Published)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.ToLower();
            q = q.Where(p => p.Name.ToLower().Contains(kw) || 
                            p.Description.ToLower().Contains(kw) ||
                            p.PluginId.ToLower().Contains(kw));
        }

        if (query.Tags?.Length > 0)
        {
            // This would need a proper many-to-many relationship for tags
            // For now, filter in memory or use JSON contains
        }

        if (query.CategoryId.HasValue)
        {
            // Filter by category
        }

        if (query.VerifiedOnly == true)
        {
            q = q.Where(p => p.Publisher.VerificationStatus == "verified");
        }

        if (query.LicenseId.HasValue)
        {
            q = q.Where(p => p.LicenseId == query.LicenseId.Value.ToString());
        }

        // Sort
        q = query.SortBy?.ToLower() switch
        {
            "name" => query.SortDescending ? q.OrderByDescending(p => p.Name) : q.OrderBy(p => p.Name),
            "updated" => query.SortDescending ? q.OrderByDescending(p => p.UpdatedAt) : q.OrderBy(p => p.UpdatedAt),
            "rating" => query.SortDescending ? q.OrderByDescending(p => p.AverageRating) : q.OrderBy(p => p.AverageRating),
            "downloads" => query.SortDescending ? q.OrderByDescending(p => p.DownloadCount) : q.OrderBy(p => p.DownloadCount),
            _ => query.SortDescending ? q.OrderByDescending(p => p.CreatedAt) : q.OrderBy(p => p.CreatedAt)
        };

        var totalCount = await q.CountAsync(ct);
        var items = await q.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);

        return new PagedResult<PluginPackage>
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<PluginPackage> UpdatePluginAsync(string pluginId, PluginUpdateRequest request, string userId, CancellationToken ct = default)
    {
        var plugin = await _dbContext.PluginPackages.FirstOrDefaultAsync(p => p.PluginId == pluginId, ct);
        if (plugin == null)
            throw new InvalidOperationException("Plugin not found");

        // Check ownership
        var publisher = await _dbContext.Publishers.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (publisher == null || plugin.PublisherId != publisher.Id)
            throw new UnauthorizedAccessException("Not authorized to update this plugin");

        plugin.Name = request.Name ?? plugin.Name;
        plugin.Description = request.Description ?? plugin.Description;
        plugin.RepositoryUrl = request.RepositoryUrl ?? plugin.RepositoryUrl;
        plugin.HomepageUrl = request.HomepageUrl ?? plugin.HomepageUrl;
        plugin.IconUrl = request.IconUrl ?? plugin.IconUrl;
        plugin.LicenseId = request.LicenseId ?? plugin.LicenseId;
        plugin.Visibility = request.Visibility ?? plugin.Visibility;
        plugin.Metadata = new PluginMetadata
        {
            Tags = request.Tags ?? plugin.Metadata.Tags,
            SupportedPlatforms = request.SupportedPlatforms ?? plugin.Metadata.SupportedPlatforms,
            SupportedResolutions = request.SupportedResolutions ?? plugin.Metadata.SupportedResolutions,
            RequiredPermissions = request.RequiredPermissions ?? plugin.Metadata.RequiredPermissions
        };
        plugin.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        return plugin;
    }

    public async Task<bool> DeletePluginAsync(string pluginId, string userId, CancellationToken ct = default)
    {
        var plugin = await _dbContext.PluginPackages.FirstOrDefaultAsync(p => p.PluginId == pluginId, ct);
        if (plugin == null)
            return false;

        var publisher = await _dbContext.Publishers.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (publisher == null || plugin.PublisherId != publisher.Id)
            throw new UnauthorizedAccessException("Not authorized to delete this plugin");

        // Soft delete
        plugin.Status = PluginStatus.Removed;
        plugin.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> TransferOwnershipAsync(string pluginId, string newOwnerId, string userId, CancellationToken ct = default)
    {
        var plugin = await _dbContext.PluginPackages.FirstOrDefaultAsync(p => p.PluginId == pluginId, ct);
        if (plugin == null)
            return false;

        var currentPublisher = await _dbContext.Publishers.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (currentPublisher == null || plugin.PublisherId != currentPublisher.Id)
            throw new UnauthorizedAccessException("Not authorized to transfer this plugin");

        var newPublisher = await _dbContext.Publishers.FirstOrDefaultAsync(p => p.UserId == newOwnerId, ct);
        if (newPublisher == null)
            throw new InvalidOperationException("New owner not found");

        plugin.PublisherId = newPublisher.Id;
        plugin.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PluginVersion> CreateVersionAsync(string pluginId, CreateVersionRequest request, string userId, CancellationToken ct = default)
    {
        var plugin = await _dbContext.PluginPackages
            .Include(p => p.Versions)
            .FirstOrDefaultAsync(p => p.PluginId == pluginId, ct);
        
        if (plugin == null)
            throw new InvalidOperationException("Plugin not found");

        // Check ownership
        var publisher = await _dbContext.Publishers.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (publisher == null || plugin.PublisherId != publisher.Id)
            throw new UnauthorizedAccessException("Not authorized to create version for this plugin");

        // Check version uniqueness
        if (plugin.Versions.Any(v => v.Version == request.Version))
            throw new InvalidOperationException("Version already exists");

        var version = new PluginVersion
        {
            PluginPackageId = plugin.Id,
            Version = request.Version,
            Changelog = request.Changelog,
            DownloadUrl = request.DownloadUrl,
            FileHash = request.FileHash,
            FileSize = request.FileSize,
            MinEngineVersion = request.MinEngineVersion,
            MaxEngineVersion = request.MaxEngineVersion,
            ManifestJson = request.ManifestJson,
            Status = VersionStatus.Pending
        };

        _dbContext.PluginVersions.Add(version);
        
        // Add dependencies
        foreach (var dep in request.Dependencies)
        {
            _dbContext.PluginDependencies.Add(new PluginDependency
            {
                PluginVersionId = version.Id,
                DependencyPluginId = dep.DependencyPluginId,
                VersionRange = dep.VersionRange,
                DependencyType = dep.DependencyType
            });
        }

        // Update plugin status
        plugin.Status = PluginStatus.PendingReview;
        plugin.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        return version;
    }

    public async Task<PluginVersion?> GetVersionAsync(string pluginId, string version, CancellationToken ct = default)
    {
        var plugin = await _dbContext.PluginPackages.FirstOrDefaultAsync(p => p.PluginId == pluginId, ct);
        if (plugin == null) return null;

        return await _dbContext.PluginVersions
            .Include(v => v.Dependencies)
            .Include(v => v.Signature)
            .FirstOrDefaultAsync(v => v.PluginPackageId == plugin.Id && v.Version == version, ct);
    }

    public async Task<PagedResult<PluginVersion>> GetVersionsAsync(string pluginId, VersionQuery query, CancellationToken ct = default)
    {
        var plugin = await _dbContext.PluginPackages.FirstOrDefaultAsync(p => p.PluginId == pluginId, ct);
        if (plugin == null)
            return new PagedResult<PluginVersion> { Items = new List<PluginVersion>(), TotalCount = 0, Page = query.Page, PageSize = query.PageSize };

        var q = _dbContext.PluginVersions
            .Include(v => v.Dependencies)
            .Include(v => v.Signature)
            .Where(v => v.PluginPackageId == plugin.Id)
            .AsQueryable();

        if (query.Status.HasValue)
            q = q.Where(v => v.Status == query.Status.Value);

        var totalCount = await q.CountAsync(ct);
        var items = await q.OrderByDescending(v => v.PublishedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<PluginVersion>
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<PluginVersion> UpdateVersionAsync(string pluginId, string version, UpdateVersionRequest request, string userId, CancellationToken ct = default)
    {
        var v = await GetVersionAsync(pluginId, version, ct);
        if (v == null)
            throw new InvalidOperationException("Version not found");

        // Check ownership
        var plugin = await _dbContext.PluginPackages.FirstOrDefaultAsync(p => p.Id == v.PluginPackageId, ct);
        var publisher = await _dbContext.Publishers.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (publisher == null || plugin?.PublisherId != publisher.Id)
            throw new UnauthorizedAccessException("Not authorized to update this version");

        v.Changelog = request.Changelog ?? v.Changelog;
        v.DownloadUrl = request.DownloadUrl ?? v.DownloadUrl;
        v.FileHash = request.FileHash ?? v.FileHash;
        v.FileSize = request.FileSize ?? v.FileSize;
        v.MinEngineVersion = request.MinEngineVersion ?? v.MinEngineVersion;
        v.MaxEngineVersion = request.MaxEngineVersion ?? v.MaxEngineVersion;
        v.ManifestJson = request.ManifestJson ?? v.ManifestJson;
        v.DeprecationNotice = request.DeprecationNotice ?? v.DeprecationNotice;

        await _dbContext.SaveChangesAsync(ct);
        return v;
    }

    public async Task<bool> DeprecateVersionAsync(string pluginId, string version, string deprecationNotice, string userId, CancellationToken ct = default)
    {
        var v = await GetVersionAsync(pluginId, version, ct);
        if (v == null) return false;

        v.Status = VersionStatus.Deprecated;
        v.DeprecationNotice = deprecationNotice;
        v.DeprecatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteVersionAsync(string pluginId, string version, string userId, CancellationToken ct = default)
    {
        var v = await GetVersionAsync(pluginId, version, ct);
        if (v == null) return false;

        // Check ownership
        var plugin = await _dbContext.PluginPackages.FirstOrDefaultAsync(p => p.Id == v.PluginPackageId, ct);
        var publisher = await _dbContext.Publishers.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (publisher == null || plugin?.PublisherId != publisher.Id)
            throw new UnauthorizedAccessException("Not authorized to delete this version");

        // Soft delete
        v.Status = VersionStatus.Removed;
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task RecordDownloadAsync(Guid pluginVersionId, string userId, string userAgent, string ipAddress, CancellationToken ct = default)
    {
        var download = new PluginDownload
        {
            PluginVersionId = pluginVersionId,
            UserId = userId,
            UserAgent = userAgent,
            IpAddress = ipAddress,
            DownloadedAt = DateTime.UtcNow
        };

        _dbContext.PluginDownloads.Add(download);

        // Update download count
        var version = await _dbContext.PluginVersions.FindAsync(new object[] { pluginVersionId }, ct);
        if (version != null)
        {
            version.DownloadCount++;
        }

        // Update plugin download count
        var versionWithPlugin = await _dbContext.PluginVersions
            .Include(v => v.PluginPackage)
            .FirstOrDefaultAsync(v => v.Id == pluginVersionId, ct);
        if (versionWithPlugin?.PluginPackage != null)
        {
            versionWithPlugin.PluginPackage.DownloadCount++;
        }

        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<DownloadStats> GetDownloadStatsAsync(string pluginId, CancellationToken ct = default)
    {
        var plugin = await _dbContext.PluginPackages.FirstOrDefaultAsync(p => p.PluginId == pluginId, ct);
        if (plugin == null)
            return new DownloadStats();

        var totalDownloads = plugin.DownloadCount;
        var versionDownloads = await _dbContext.PluginVersions
            .Where(v => v.PluginPackageId == plugin.Id)
            .SumAsync(v => (long?)v.DownloadCount ?? 0, ct);

        var recentDownloads = await _dbContext.PluginDownloads
            .Where(d => d.PluginVersion.PluginPackageId == plugin.Id && d.DownloadedAt >= DateTime.UtcNow.AddDays(-30))
            .CountAsync(ct);

        return new DownloadStats
        {
            TotalDownloads = totalDownloads,
            MonthlyDownloads = recentDownloads,
            VersionBreakdown = await _dbContext.PluginVersions
                .Where(v => v.PluginPackageId == plugin.Id)
                .Select(v => new VersionDownloadStat { Version = v.Version, Downloads = v.DownloadCount })
                .ToListAsync(ct)
        };
    }

    public async Task<string> GetDownloadUrlAsync(Guid pluginVersionId, CancellationToken ct = default)
    {
        var version = await _dbContext.PluginVersions
            .Include(v => v.PluginPackage)
            .FirstOrDefaultAsync(v => v.Id == pluginVersionId, ct);

        if (version == null)
            throw new InvalidOperationException("Plugin version not found");

        var objectName = $"plugins/{version.PluginPackage.PluginId}/{version.Version}/package.dll";
        return await _storage.GeneratePresignedUrlAsync(objectName, TimeSpan.FromMinutes(15), ct);
    }

    public async Task<PluginReview> CreateReviewAsync(string pluginId, CreateReviewRequest request, string userId, CancellationToken ct = default)
    {
        var plugin = await _dbContext.PluginPackages.FirstOrDefaultAsync(p => p.PluginId == pluginId, ct);
        if (plugin == null)
            throw new InvalidOperationException("Plugin not found");

        // Check if user already reviewed
        var existing = await _dbContext.PluginReviews
            .FirstOrDefaultAsync(r => r.PluginPackageId == plugin.Id && r.UserId == userId, ct);
        if (existing != null)
            throw new InvalidOperationException("User has already reviewed this plugin");

        var review = new PluginReview
        {
            PluginPackageId = plugin.Id,
            UserId = userId,
            UserName = request.UserName,
            Rating = request.Rating,
            Title = request.Title,
            Content = request.Content,
            IsVerifiedPurchase = request.IsVerifiedPurchase
        };

        _dbContext.PluginReviews.Add(review);

        // Update plugin rating
        await UpdatePluginRatingAsync(plugin.Id, ct);

        await _dbContext.SaveChangesAsync(ct);
        return review;
    }

    public async Task<PagedResult<PluginReview>> GetReviewsAsync(string pluginId, ReviewQuery query, CancellationToken ct = default)
    {
        var plugin = await _dbContext.PluginPackages.FirstOrDefaultAsync(p => p.PluginId == pluginId, ct);
        if (plugin == null)
            return new PagedResult<PluginReview> { Items = new List<PluginReview>(), TotalCount = 0, Page = query.Page, PageSize = query.PageSize };

        var q = _dbContext.PluginReviews
            .Where(r => r.PluginPackageId == plugin.Id && !r.IsFlagged)
            .AsQueryable();

        if (query.Rating.HasValue)
            q = q.Where(r => r.Rating == query.Rating.Value);

        var totalCount = await q.CountAsync(ct);
        var items = await q.OrderByDescending(r => r.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<PluginReview>
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<PluginReview> UpdateReviewAsync(Guid reviewId, UpdateReviewRequest request, string userId, CancellationToken ct = default)
    {
        var review = await _dbContext.PluginReviews.FindAsync(new object[] { reviewId }, ct);
        if (review == null)
            throw new InvalidOperationException("Review not found");

        if (review.UserId != userId)
            throw new UnauthorizedAccessException("Not authorized to update this review");

        review.Rating = request.Rating ?? review.Rating;
        review.Title = request.Title ?? review.Title;
        review.Content = request.Content ?? review.Content;
        review.IsEdited = true;
        review.UpdatedAt = DateTime.UtcNow;

        await UpdatePluginRatingAsync(review.PluginPackageId, ct);
        await _dbContext.SaveChangesAsync(ct);
        return review;
    }

    public async Task<bool> DeleteReviewAsync(Guid reviewId, string userId, CancellationToken ct = default)
    {
        var review = await _dbContext.PluginReviews.FindAsync(new object[] { reviewId }, ct);
        if (review == null) return false;

        if (review.UserId != userId)
            throw new UnauthorizedAccessException("Not authorized to delete this review");

        var pluginId = review.PluginPackageId;
        _dbContext.PluginReviews.Remove(review);
        await UpdatePluginRatingAsync(pluginId, ct);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> FlagReviewAsync(Guid reviewId, string reason, string userId, CancellationToken ct = default)
    {
        var review = await _dbContext.PluginReviews.FindAsync(new object[] { reviewId }, ct);
        if (review == null) return false;

        review.IsFlagged = true;
        review.FlagReason = reason;
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    private async Task UpdatePluginRatingAsync(Guid pluginPackageId, CancellationToken ct)
    {
        var reviews = await _dbContext.PluginReviews
            .Where(r => r.PluginPackageId == pluginPackageId && !r.IsFlagged)
            .ToListAsync(ct);

        if (reviews.Count > 0)
        {
            var plugin = await _dbContext.PluginPackages.FindAsync(new object[] { pluginPackageId }, ct);
            if (plugin != null)
            {
                plugin.AverageRating = (double)reviews.Average(r => r.Rating);
                plugin.ReviewCount = reviews.Count;
            }
        }
    }

    public async Task<Publisher> CreatePublisherAsync(CreatePublisherRequest request, string userId, CancellationToken ct = default)
    {
        var existing = await _dbContext.Publishers.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (existing != null)
            throw new InvalidOperationException("Publisher already exists for this user");

        var slug = GenerateSlug(request.Name);
        var existingSlug = await _dbContext.Publishers.FirstOrDefaultAsync(p => p.Slug == slug, ct);
        if (existingSlug != null)
            slug = $"{slug}-{Guid.NewGuid().ToString()[..8]}";

        var publisher = new Publisher
        {
            UserId = userId,
            Name = request.Name,
            Slug = slug,
            Description = request.Description,
            LogoUrl = request.LogoUrl,
            WebsiteUrl = request.WebsiteUrl,
            Email = request.Email,
            PayoutEmail = request.PayoutEmail,
            RevenueSharePercent = 70
        };

        _dbContext.Publishers.Add(publisher);
        await _dbContext.SaveChangesAsync(ct);
        return publisher;
    }

    public async Task<Publisher?> GetPublisherAsync(Guid publisherId, CancellationToken ct = default)
    {
        return await _dbContext.Publishers
            .Include(p => p.Packages)
            .FirstOrDefaultAsync(p => p.Id == publisherId, ct);
    }

    public async Task<Publisher?> GetPublisherByUserIdAsync(string userId, CancellationToken ct = default)
    {
        return await _dbContext.Publishers
            .Include(p => p.Packages)
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);
    }

    public async Task<Publisher> UpdatePublisherAsync(Guid publisherId, UpdatePublisherRequest request, string userId, CancellationToken ct = default)
    {
        var publisher = await _dbContext.Publishers.FindAsync(new object[] { publisherId }, ct);
        if (publisher == null)
            throw new InvalidOperationException("Publisher not found");

        if (publisher.UserId != userId)
            throw new UnauthorizedAccessException("Not authorized to update this publisher");

        publisher.Name = request.Name ?? publisher.Name;
        publisher.Description = request.Description ?? publisher.Description;
        publisher.LogoUrl = request.LogoUrl ?? publisher.LogoUrl;
        publisher.WebsiteUrl = request.WebsiteUrl ?? publisher.WebsiteUrl;
        publisher.Email = request.Email ?? publisher.Email;
        publisher.PayoutEmail = request.PayoutEmail ?? publisher.PayoutEmail;
        publisher.PayoutMethod = request.PayoutMethod ?? publisher.PayoutMethod;
        publisher.PayoutAccountId = request.PayoutAccountId ?? publisher.PayoutAccountId;

        await _dbContext.SaveChangesAsync(ct);
        return publisher;
    }

    public async Task<PagedResult<PublisherPayout>> GetPayoutsAsync(Guid publisherId, PayoutQuery query, CancellationToken ct = default)
    {
        var q = _dbContext.PublisherPayouts
            .Where(p => p.PublisherId == publisherId)
            .AsQueryable();

        if (query.Status.HasValue)
            q = q.Where(p => p.Status == query.Status.Value);

        var totalCount = await q.CountAsync(ct);
        var items = await q.OrderByDescending(p => p.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<PublisherPayout>
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<IReadOnlyList<PluginTag>> GetPopularTagsAsync(int limit = 20, CancellationToken ct = default)
    {
        return await _dbContext.PluginTags
            .OrderByDescending(t => t.UsageCount)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PluginCategory>> GetCategoriesAsync(CancellationToken ct = default)
    {
        return await _dbContext.PluginCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<License>> GetLicensesAsync(CancellationToken ct = default)
    {
        return await _dbContext.Licenses
            .Where(l => !l.IsDeprecated)
            .OrderBy(l => l.Name)
            .ToListAsync(ct);
    }

    public async Task<ModerationQueue> SubmitForModerationAsync(SubmitModerationRequest request, CancellationToken ct = default)
    {
        var item = new ModerationQueue
        {
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            SubmitterId = request.SubmitterId,
            SubmitterName = request.SubmitterName,
            Reason = request.Reason,
            Priority = request.Priority
        };

        _dbContext.ModerationQueue.Add(item);
        await _dbContext.SaveChangesAsync(ct);
        return item;
    }

    public async Task<PagedResult<ModerationQueue>> GetModerationQueueAsync(ModerationQuery query, CancellationToken ct = default)
    {
        var q = _dbContext.ModerationQueue.AsQueryable();

        if (query.Status.HasValue)
            q = q.Where(m => m.Status == query.Status.Value);

        if (!string.IsNullOrEmpty(query.EntityType))
            q = q.Where(m => m.EntityType == query.EntityType);

        if (query.Priority.HasValue)
            q = q.Where(m => m.Priority == query.Priority.Value);

        var totalCount = await q.CountAsync(ct);
        var items = await q.OrderByDescending(m => m.SubmittedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<ModerationQueue>
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<ModerationQueue> ModerateItemAsync(Guid moderationId, ModerateRequest request, string moderatorId, CancellationToken ct = default)
    {
        var item = await _dbContext.ModerationQueue.FindAsync(new object[] { moderationId }, ct);
        if (item == null)
            throw new InvalidOperationException("Moderation item not found");

        item.Status = request.Action switch
        {
            ModerationAction.Approve => ModerationStatus.Approved,
            ModerationAction.Reject => ModerationStatus.Rejected,
            ModerationAction.Escalate => ModerationStatus.Escalated,
            _ => item.Status
        };

        item.ModeratorId = moderatorId;
        item.ModeratorNotes = request.Notes;
        item.ReviewedAt = DateTime.UtcNow;
        item.ResolvedAt = DateTime.UtcNow;

        // Apply moderation decision
        await ApplyModerationDecisionAsync(item, request.Action, ct);

        await _dbContext.SaveChangesAsync(ct);
        return item;
    }

    private async Task ApplyModerationDecisionAsync(ModerationQueue item, ModerationAction action, CancellationToken ct)
    {
        switch (item.EntityType)
        {
            case "package":
                var pkg = await _dbContext.PluginPackages.FirstOrDefaultAsync(p => p.Id.ToString() == item.EntityId, ct);
                if (pkg != null)
                {
                    pkg.Status = action == ModerationAction.Approve ? PluginStatus.Approved : PluginStatus.Rejected;
                }
                break;
            case "version":
                var ver = await _dbContext.PluginVersions.FirstOrDefaultAsync(v => v.Id.ToString() == item.EntityId, ct);
                if (ver != null)
                {
                    ver.Status = action == ModerationAction.Approve ? VersionStatus.Approved : VersionStatus.Rejected;
                }
                break;
            case "publisher":
                var pub = await _dbContext.Publishers.FirstOrDefaultAsync(p => p.Id.ToString() == item.EntityId, ct);
                if (pub != null)
                {
                    pub.VerificationStatus = action == ModerationAction.Approve ? "verified" : "rejected";
                }
                break;
        }
    }

    private string GenerateSlug(string name)
    {
        return name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace(".", "")
            .Replace("_", "-")
            .Replace("--", "-")
            .Trim('-');
    }
}

// Request/Response Models

public sealed record PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public sealed record PluginPublishRequest(
    string PluginId,
    string Name,
    string Description,
    string RepositoryUrl,
    string HomepageUrl,
    string IconUrl,
    string LicenseId,
    string[] Tags,
    string[] SupportedPlatforms,
    Resolution[] SupportedResolutions,
    string[] RequiredPermissions
);

public sealed record PluginUpdateRequest(
    string? Name = null,
    string? Description = null,
    string? RepositoryUrl = null,
    string? HomepageUrl = null,
    string? IconUrl = null,
    string? LicenseId = null,
    string? Visibility = null,
    string[]? Tags = null,
    string[]? SupportedPlatforms = null,
    Resolution[]? SupportedResolutions = null,
    string[]? RequiredPermissions = null
);

public sealed record CreateVersionRequest(
    string Version,
    string Changelog,
    string DownloadUrl,
    string FileHash,
    long FileSize,
    string MinEngineVersion,
    string MaxEngineVersion,
    string ManifestJson,
    PluginDependencyRequest[] Dependencies
);

public sealed record PluginDependencyRequest(
    string DependencyPluginId,
    string VersionRange,
    string DependencyType
);

public sealed record UpdateVersionRequest(
    string? Changelog = null,
    string? DownloadUrl = null,
    string? FileHash = null,
    long? FileSize = null,
    string? MinEngineVersion = null,
    string? MaxEngineVersion = null,
    string? ManifestJson = null,
    string? DeprecationNotice = null
);

public sealed record PluginSearchQuery(
    string? Keyword = null,
    Guid? CategoryId = null,
    bool? VerifiedOnly = null,
    int? LicenseId = null,
    string SortBy = "createdAt",
    bool SortDescending = true,
    int Page = 1,
    int PageSize = 20,
    string[]? Tags = null
);

public sealed record VersionQuery(
    VersionStatus? Status = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record CreateReviewRequest(
    int Rating,
    string Title,
    string Content,
    string UserName,
    bool IsVerifiedPurchase
);

public sealed record ReviewQuery(
    int? Rating = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record UpdateReviewRequest(
    int? Rating = null,
    string? Title = null,
    string? Content = null
);

public sealed record CreatePublisherRequest(
    string Name,
    string Description,
    string LogoUrl,
    string WebsiteUrl,
    string Email,
    string PayoutEmail
);

public sealed record UpdatePublisherRequest(
    string? Name = null,
    string? Description = null,
    string? LogoUrl = null,
    string? WebsiteUrl = null,
    string? Email = null,
    string? PayoutEmail = null,
    string? PayoutMethod = null,
    string? PayoutAccountId = null
);

public sealed record PayoutQuery(
    PayoutStatus? Status = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record SubmitModerationRequest(
    string EntityType,
    string EntityId,
    string SubmitterId,
    string SubmitterName,
    string Reason,
    ModerationPriority Priority = ModerationPriority.Normal
);

public sealed record ModerationQuery(
    ModerationStatus? Status = null,
    string? EntityType = null,
    ModerationPriority? Priority = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record ModerateRequest(
    ModerationAction Action,
    string Notes
);

public enum ModerationAction
{
    Approve,
    Reject,
    Escalate
}

public sealed record DownloadStats
{
    public long TotalDownloads { get; init; }
    public long MonthlyDownloads { get; init; }
    public IReadOnlyList<VersionDownloadStat> VersionBreakdown { get; init; } = Array.Empty<VersionDownloadStat>();
}

public sealed record VersionDownloadStat
{
    public string Version { get; init; } = string.Empty;
    public long Downloads { get; init; }
}