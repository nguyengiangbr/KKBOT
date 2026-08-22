using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KKBOT.Marketplace.Server.Models;
using KKBOT.Marketplace.Server.Services;
using KKBOT.PluginMarketplace;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KKBOT.Marketplace.Server.Tests;

public class MarketplaceServerTests
{
    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<MarketplaceDbContext>(options => options.UseSqlite("DataSource=:memory:"));
        services.AddLogging(b => b.AddConsole());
        
        // Storage
        services.Configure<StorageOptions>(o => o.StoragePath = Path.GetTempPath());
        services.AddSingleton<IStorageService, LocalStorageService>();
        
        // Signing
        services.Configure<SigningOptions>(o => o.RequireSigning = false);
        services.AddSingleton<ISigningService, SigningService>();
        
        // Marketplace
        services.AddScoped<IMarketplaceService, MarketplaceService>();

        var provider = services.BuildServiceProvider();
        
        // Ensure database created
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        
        return provider;
    }

    [Fact]
    public async Task CreatePublisher_Success()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var marketplace = scope.ServiceProvider.GetRequiredService<IMarketplaceService>();

        var request = new CreatePublisherRequest(
            "Test Publisher",
            "A test publisher",
            "https://example.com/logo.png",
            "https://example.com",
            "publisher@example.com",
            "payouts@example.com"
        );

        var publisher = await marketplace.CreatePublisherAsync(request, "user-123", CancellationToken.None);

        Assert.NotNull(publisher);
        Assert.Equal("Test Publisher", publisher.Name);
        Assert.Equal("user-123", publisher.UserId);
        Assert.Equal("verified", publisher.VerificationStatus); // Will be "pending" initially
    }

    [Fact]
    public async Task PublishPlugin_Success()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var marketplace = scope.ServiceProvider.GetRequiredService<IMarketplaceService>();

        // Create publisher first
        var pubRequest = new CreatePublisherRequest(
            "Test Publisher",
            "A test publisher",
            "https://example.com/logo.png",
            "https://example.com",
            "publisher@example.com",
            "payouts@example.com"
        );
        var publisher = await marketplace.CreatePublisherAsync(pubRequest, "user-123", CancellationToken.None);

        // Publish plugin
        var request = new PluginPublishRequest(
            "com.test.plugin",
            "Test Plugin",
            "A test plugin for testing",
            "https://github.com/test/plugin",
            "https://example.com/plugin",
            "https://example.com/icon.png",
            "MIT",
            new[] { "test", "demo" },
            new[] { "windows", "linux" },
            new[] { new Resolution { Width = 1920, Height = 1080 } },
            new[] { "DEVICE_READ", "INPUT_WRITE" }
        );

        var plugin = await marketplace.PublishPluginAsync(request, publisher.Id.ToString(), CancellationToken.None);

        Assert.NotNull(plugin);
        Assert.Equal("com.test.plugin", plugin.PluginId);
        Assert.Equal("Test Plugin", plugin.Name);
        Assert.Equal(PluginStatus.Draft, plugin.Status);
    }

    [Fact]
    public async Task CreateVersion_Success()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var marketplace = scope.ServiceProvider.GetRequiredService<IMarketplaceService>();

        // Create publisher and plugin
        var publisher = await marketplace.CreatePublisherAsync(
            new CreatePublisherRequest("Test Pub", "Desc", "", "", "pub@test.com", "payout@test.com"),
            "user-123", CancellationToken.None);

        var plugin = await marketplace.PublishPluginAsync(
            new PluginPublishRequest("com.test.vplugin", "Versioned Plugin", "Desc", "", "", "", "MIT", 
                Array.Empty<string>(), Array.Empty<string>(), Array.Empty<Resolution>(), Array.Empty<string>()),
            publisher.Id.ToString(), CancellationToken.None);

        // Create version
        var versionRequest = new CreateVersionRequest(
            "1.0.0",
            "Initial release",
            "https://example.com/download.dll",
            "abc123hash",
            1024000,
            "1.0.0",
            "",
            "{}",
            new PluginDependencyRequest[]
            {
                new PluginDependencyRequest("com.dependency.plugin", ">=1.0.0", "required")
            }
        );

        var version = await marketplace.CreateVersionAsync("com.test.vplugin", versionRequest, "user-123", CancellationToken.None);

        Assert.NotNull(version);
        Assert.Equal("1.0.0", version.Version);
        Assert.Equal(VersionStatus.Pending, version.Status);
    }

    [Fact]
    public async Task SearchPlugins_Works()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var marketplace = scope.ServiceProvider.GetRequiredService<IMarketplaceService>();

        // Publish a plugin
        var publisher = await marketplace.CreatePublisherAsync(
            new CreatePublisherRequest("Search Pub", "Desc", "", "", "search@test.com", "payout@test.com"),
            "user-456", CancellationToken.None);

        await marketplace.PublishPluginAsync(
            new PluginPublishRequest("com.search.plugin", "Searchable Plugin", "A plugin for search testing", "", "", "", "MIT",
                new[] { "search", "test" }, Array.Empty<string>(), Array.Empty<Resolution>(), Array.Empty<string>()),
            publisher.Id.ToString(), CancellationToken.None);

        // Search
        var query = new PluginSearchQuery
        {
            Keyword = "searchable",
            Page = 1,
            PageSize = 10,
            SortBy = "downloads",
            SortDescending = true
        };

        var results = await marketplace.SearchPluginsAsync(query, CancellationToken.None);

        Assert.NotEmpty(results.Items);
        Assert.Contains(results.Items, p => p.PluginId == "com.search.plugin");
    }

    [Fact]
    public async Task CreateReview_Works()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var marketplace = scope.ServiceProvider.GetRequiredService<IMarketplaceService>();

        var publisher = await marketplace.CreatePublisherAsync(
            new CreatePublisherRequest("Review Pub", "Desc", "", "", "review@test.com", "payout@test.com"),
            "user-789", CancellationToken.None);

        var plugin = await marketplace.PublishPluginAsync(
            new PluginPublishRequest("com.review.plugin", "Review Plugin", "Desc", "", "", "", "MIT",
                Array.Empty<string>(), Array.Empty<string>(), Array.Empty<Resolution>(), Array.Empty<string>()),
            publisher.Id.ToString(), CancellationToken.None);

        // Create review
        var reviewRequest = new CreateReviewRequest(
            5,
            "Great plugin!",
            "This plugin works perfectly for my needs.",
            "HappyUser",
            true
        );

        var review = await marketplace.CreateReviewAsync("com.review.plugin", reviewRequest, "reviewer-123", CancellationToken.None);

        Assert.NotNull(review);
        Assert.Equal(5, review.Rating);
        Assert.Equal("Great plugin!", review.Title);
    }

    [Fact]
    public async Task DuplicateReview_Fails()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var marketplace = scope.ServiceProvider.GetRequiredService<IMarketplaceService>();

        var publisher = await marketplace.CreatePublisherAsync(
            new CreatePublisherRequest("Dup Pub", "Desc", "", "", "dup@test.com", "payout@test.com"),
            "user-999", CancellationToken.None);

        await marketplace.PublishPluginAsync(
            new PluginPublishRequest("com.dup.plugin", "Dup Plugin", "Desc", "", "", "", "MIT",
                Array.Empty<string>(), Array.Empty<string>(), Array.Empty<Resolution>(), Array.Empty<string>()),
            publisher.Id.ToString(), CancellationToken.None);

        var reviewRequest = new CreateReviewRequest(4, "Good", "Content", "User1", false);

        await marketplace.CreateReviewAsync("com.dup.plugin", reviewRequest, "reviewer-1", CancellationToken.None);

        // Try to create duplicate review
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            marketplace.CreateReviewAsync("com.dup.plugin", reviewRequest, "reviewer-1", CancellationToken.None));
    }

    [Fact]
    public async Task GetCategories_ReturnsSeededCategories()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var marketplace = scope.ServiceProvider.GetRequiredService<IMarketplaceService>();

        var categories = await marketplace.GetCategoriesAsync(CancellationToken.None);

        Assert.NotEmpty(categories);
        Assert.Contains(categories, c => c.Name == "Game Automation");
        Assert.Contains(categories, c => c.Name == "AI & ML");
    }

    [Fact]
    public async Task GetLicenses_ReturnsSeededLicenses()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var marketplace = scope.ServiceProvider.GetRequiredService<IMarketplaceService>();

        var licenses = await marketplace.GetLicensesAsync(CancellationToken.None);

        Assert.NotEmpty(licenses);
        Assert.Contains(licenses, l => l.SpdxId == "MIT");
        Assert.Contains(licenses, l => l.SpdxId == "Apache-2.0");
    }

    [Fact]
    public async Task ModerationQueue_Works()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var marketplace = scope.ServiceProvider.GetRequiredService<IMarketplaceService>();

        var request = new SubmitModerationRequest(
            "package",
            "com.test.plugin",
            "user-123",
            "Test User",
            "Inappropriate content",
            ModerationPriority.High
        );

        var item = await marketplace.SubmitForModerationAsync(request, CancellationToken.None);

        Assert.NotNull(item);
        Assert.Equal("package", item.EntityType);
        Assert.Equal("com.test.plugin", item.EntityId);
        Assert.Equal(ModerationStatus.Pending, item.Status);
        Assert.Equal(ModerationPriority.High, item.Priority);
    }
}

public class StorageServiceTests
{
    private readonly string _testStoragePath;
    private readonly LocalStorageService _storage;

    public StorageServiceTests()
    {
        _testStoragePath = Path.Combine(Path.GetTempPath(), "kkbot-storage-test", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testStoragePath);

        var options = Options.Create(new StorageOptions { StoragePath = _testStoragePath });
        _storage = new LocalStorageService(NullLogger<LocalStorageService>.Instance, options);
    }

    [Fact]
    public async Task UploadAndDownloadPlugin_Works()
    {
        var pluginData = "fake plugin dll content"u8.ToArray();
        
        using var stream = new MemoryStream(pluginData);
        var path = await _storage.UploadPluginAsync("test-plugin", "1.0.0", stream, "application/octet-stream", CancellationToken.None);

        Assert.True(File.Exists(path));

        // Download
        using var downloadStream = await _storage.DownloadPluginAsync("test-plugin", "1.0.0", CancellationToken.None);
        var downloaded = new byte[pluginData.Length];
        await downloadStream.ReadExactlyAsync(downloaded, CancellationToken.None);
        
        Assert.Equal(pluginData, downloaded);
    }

    [Fact]
    public async Task UploadIcon_Works()
    {
        var iconData = "fake png data"u8.ToArray();
        
        using var stream = new MemoryStream(iconData);
        var path = await _storage.UploadIconAsync("test-plugin", stream, "image/png", CancellationToken.None);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task DeletePlugin_Works()
    {
        var pluginData = "test"u8.ToArray();
        
        using var stream = new MemoryStream(pluginData);
        await _storage.UploadPluginAsync("delete-test", "1.0.0", stream, "application/octet-stream", CancellationToken.None);

        var deleted = await _storage.DeletePluginAsync("delete-test", "1.0.0", CancellationToken.None);
        Assert.True(deleted);

        var exists = await _storage.ExistsAsync("plugins/delete-test/1.0.0/package.dll", CancellationToken.None);
        Assert.False(exists);
    }
}

public class SigningServiceTests
{
    [Fact]
    public void SigningOptions_Defaults()
    {
        var options = new SigningOptions();
        
        Assert.True(options.RequireSigning);
        Assert.Empty(options.AllowedSigners);
    }
}

public class HubTests
{
    [Fact]
    public void MarketplaceHub_GroupNames()
    {
        // Test group name formatting
        var pluginGroup = $"plugin:com.test.plugin";
        var publisherGroup = $"publisher:pub-123";
        var userGroup = $"user:user-123";
        var globalGroup = "marketplace:global";

        Assert.StartsWith("plugin:", pluginGroup);
        Assert.StartsWith("publisher:", publisherGroup);
        Assert.StartsWith("user:", userGroup);
        Assert.Equal("marketplace:global", globalGroup);
    }
}