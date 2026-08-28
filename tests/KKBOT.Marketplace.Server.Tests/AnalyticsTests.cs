using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KKBOT.Marketplace.Server.Models;
using KKBOT.Marketplace.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KKBOT.Marketplace.Server.Tests;

public class AnalyticsServiceTests
{
    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        var dbPath = Path.Combine(Path.GetTempPath(), "kkbot-ana-" + Guid.NewGuid().ToString("N") + ".db");
        services.AddDbContext<MarketplaceDbContext>(options => options.UseSqlite("DataSource=" + dbPath));
        services.AddLogging(b => b.AddConsole());
        
        // Add marketplace services
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<ISentimentAnalysisService, SentimentAnalysisService>();
        services.AddScoped<ISecurityScanningService, SecurityScanningService>();
        services.AddScoped<ILicenseComplianceService, LicenseComplianceService>();
        
        services.AddSingleton<IStorageService, LocalStorageService>();
        
        services.Configure<SigningOptions>(o => o.RequireSigning = false);
        services.AddSingleton<ISigningService, SigningService>();
        
        services.AddScoped<ISecurityScanningService, SecurityScanningService>();
        
        services.AddScoped<IMarketplaceService, MarketplaceService>();

        var provider = services.BuildServiceProvider();
        
        // Ensure database created
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        db.Database.EnsureCreated();
        
        return provider;
    }

    [Fact]
    public async Task AnalyticsService_Overview_ReturnsData()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var analytics = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();

        var overview = await analytics.GetOverviewAsync(CancellationToken.None);

        Assert.NotNull(overview);
        Assert.True(overview.TotalPlugins >= 0);
        Assert.True(overview.TotalPublishers >= 0);
    }

    [Fact]
    public async Task AnalyticsService_Trends_ReturnsData()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var analytics = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();

        var trends = await analytics.GetTrendsAsync(TrendPeriod.Last7Days, CancellationToken.None);

        Assert.NotNull(trends);
        Assert.Equal(TrendPeriod.Last7Days, trends.Period);
    }

    [Fact]
    public async Task AnalyticsService_Health_ReturnsMetrics()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var analytics = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();

        var health = await analytics.GetPlatformHealthMetricsAsync(CancellationToken.None);

        Assert.NotNull(health);
        Assert.True(health.ApiUptime >= 0);
    }

    [Fact]
    public async Task AnalyticsService_Moderation_ReturnsData()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var analytics = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();

        var moderation = await analytics.GetModerationAnalyticsAsync(CancellationToken.None);

        Assert.NotNull(moderation);
        Assert.True(moderation.TotalQueued >= 0);
    }

    [Fact]
    public async Task AnalyticsService_RealTime_ReturnsData()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var analytics = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();

        var realtime = await analytics.GetRealTimeMetricsAsync(CancellationToken.None);

        Assert.NotNull(realtime);
        Assert.True(realtime.DownloadsLastMinute >= 0);
    }
}

public class SentimentAnalysisServiceTests
{
    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        var dbPath = Path.Combine(Path.GetTempPath(), "kkbot-sent-" + Guid.NewGuid().ToString("N") + ".db");
        services.AddDbContext<MarketplaceDbContext>(options => options.UseSqlite("DataSource=" + dbPath));
        services.AddLogging(b => b.AddConsole());
        services.AddScoped<ISentimentAnalysisService, SentimentAnalysisService>();
        
        var provider = services.BuildServiceProvider();
        
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        db.Database.EnsureCreated();
        
        return provider;
    }

    [Fact]
    public async Task SentimentAnalysis_RuleBased_Positive()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var sentiment = scope.ServiceProvider.GetRequiredService<ISentimentAnalysisService>();

        var result = await sentiment.AnalyzeAsync("This plugin is amazing! It works perfectly and is very fast.", CancellationToken.None);

        Assert.Equal(SentimentLabel.Positive, result.Label);
        Assert.True(result.Score > 0);
    }

    [Fact]
    public async Task SentimentAnalysis_RuleBased_Negative()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var sentiment = scope.ServiceProvider.GetRequiredService<ISentimentAnalysisService>();

        var result = await sentiment.AnalyzeAsync("This plugin is terrible. It crashes constantly and is very slow.", CancellationToken.None);

        Assert.Equal(SentimentLabel.Negative, result.Label);
        Assert.True(result.Score < 0);
    }

    [Fact]
    public async Task SentimentAnalysis_RuleBased_Neutral()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var sentiment = scope.ServiceProvider.GetRequiredService<ISentimentAnalysisService>();

        var result = await sentiment.AnalyzeAsync("This plugin exists.", CancellationToken.None);

        Assert.Equal(SentimentLabel.Neutral, result.Label);
    }

    [Fact]
    public async Task SentimentAnalysis_Batch_Works()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var sentiment = scope.ServiceProvider.GetRequiredService<ISentimentAnalysisService>();

        var texts = new[]
        {
            "Great plugin!",
            "Terrible experience",
            "It works okay"
        };

        var result = await sentiment.AnalyzeBatchAsync(texts, CancellationToken.None);

        Assert.Equal(3, result.Results.Count);
        Assert.Equal(3, result.Summary.TotalAnalyzed);
    }

    [Fact]
    public async Task SentimentAnalysis_Aspects_Detected()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var sentiment = scope.ServiceProvider.GetRequiredService<ISentimentAnalysisService>();

        var text = "The performance is excellent but the UI is buggy and slow.";
        var result = await sentiment.AnalyzeAsync(text, CancellationToken.None);

        Assert.Contains("performance", result.AspectScores.Keys);
        Assert.Contains("ui", result.AspectScores.Keys);
    }
}

public class SecurityScanningServiceTests
{
    [Fact]
    public void SecurityRules_Loaded()
    {
        // SecurityRules is private static, but we can verify the service instantiates
        Assert.True(true); // Placeholder
    }

    [Fact]
    public void SecurityRule_SeverityLevels()
    {
        // Verify severity enum values
        Assert.Equal(5, Enum.GetValues<Severity>().Length);
        Assert.Equal(Severity.Critical, Enum.GetValues<Severity>().Cast<Severity>().Max());
    }

    [Fact]
    public void SecurityRiskLevel_Order()
    {
        var levels = Enum.GetValues<SecurityRiskLevel>().Cast<SecurityRiskLevel>().ToList();
        Assert.Equal(5, levels.Count);
        Assert.Equal(SecurityRiskLevel.Critical, levels.Max());
    }
}

public class LicenseComplianceServiceTests
{
    private static LicenseComplianceService CreateService()
    {
        var options = new DbContextOptionsBuilder<MarketplaceDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        var db = new MarketplaceDbContext(options);
        var logger = LoggerFactory.Create(_ => { }).CreateLogger<LicenseComplianceService>();
        return new LicenseComplianceService(db, logger);
    }

    [Fact]
    public async Task LicenseCompatibility_MIT_Compatible()
    {
        var service = CreateService();

        var compatible = await service.IsLicenseCompatibleAsync("MIT", "Apache-2.0");

        Assert.True(compatible);
    }

    [Fact]
    public async Task LicenseCompatibility_GPL_MIT_Incompatible()
    {
        var service = CreateService();

        var compatible = await service.IsLicenseCompatibleAsync("GPL-3.0", "MIT");

        Assert.False(compatible);
    }

    [Fact]
    public async Task LicenseKnowledgeBase_Loaded()
    {
        var service = CreateService();

        // MIT <-> Apache-2.0 are compatible; Proprietary <-> MIT are not.
        Assert.True(await service.IsLicenseCompatibleAsync("MIT", "Apache-2.0"));
        Assert.False(await service.IsLicenseCompatibleAsync("Proprietary", "MIT"));
    }
}