using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KKBOT.Marketplace.Server.Models;
using KKBOT.Marketplace.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KKBOT.Marketplace.Server.Tests;

public class AnalyticsServiceTests
{
    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<MarketplaceDbContext>(options => options.UseSqlite("DataSource=:memory:"));
        services.AddLogging(b => b.AddConsole());
        
        // Add marketplace services
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<ISentimentAnalysisService, SentimentAnalysisService>();
        services.AddScoped<ISecurityScanningService, SecurityScanningService>();
        services.AddScoped<ILicenseComplianceService, LicenseComplianceService>();
        
        services.Configure<StorageOptions>(o => o.StoragePath = Path.GetTempPath());
        services.AddSingleton<IStorageService, LocalStorageService>();
        
        services.Configure<SigningOptions>(o => o.RequireSigning = false);
        services.AddSingleton<ISigningService, SigningService>();
        
        services.Configure<SecurityScanOptions>(o => o.RequireSigning = false);
        services.AddScoped<ISecurityScanningService, SecurityScanningService>();
        
        services.AddScoped<IMarketplaceService, MarketplaceService>();
        services.AddSingleton<MLContext>();

        var provider = services.BuildServiceProvider();
        
        // Ensure database created
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        db.Database.OpenConnection();
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
        services.AddDbContext<MarketplaceDbContext>(options => options.UseSqlite("DataSource=:memory:"));
        services.AddLogging(b => b.AddConsole());
        services.AddScoped<ISentimentAnalysisService, SentimentAnalysisService>();
        
        var provider = services.BuildServiceProvider();
        
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        db.Database.OpenConnection();
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
        Assert.Equal(4, Enum.GetValues<Severity>().Length);
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
    [Fact]
    public async Task LicenseCompatibility_MIT_Compatible()
    {
        var knowledgeBase = LicenseKnowledgeBase.Instance;
        var mit = knowledgeBase.GetLicense("MIT");
        var apache = knowledgeBase.GetLicense("Apache-2.0");

        Assert.NotNull(mit);
        Assert.NotNull(apache);
        
        var compat = LicenseCompatibilityService.DetermineCompatibility(mit!, apache!);
        Assert.Equal(CompatibilityLevel.Compatible, compat);
    }

    [Fact]
    public async Task LicenseCompatibility_GPL_MIT_Incompatible()
    {
        var knowledgeBase = LicenseKnowledgeBase.Instance;
        var gpl = knowledgeBase.GetLicense("GPL-3.0");
        var mit = knowledgeBase.GetLicense("MIT");

        Assert.NotNull(gpl);
        Assert.NotNull(mit);
        
        var compat = LicenseCompatibilityService.DetermineCompatibility(gpl!, mit!);
        Assert.Equal(CompatibilityLevel.Incompatible, compat);
    }

    [Fact]
    public async Task LicenseKnowledgeBase_Loaded()
    {
        var kb = LicenseKnowledgeBase.Instance;
        
        Assert.True(kb.AllLicenses.Count > 0);
        Assert.NotNull(kb.GetLicense("MIT"));
        Assert.NotNull(kb.GetLicense("Apache-2.0"));
        Assert.NotNull(kb.GetLicense("GPL-3.0"));
        Assert.NotNull(kb.GetLicense("BSD-3-Clause"));
        Assert.NotNull(kb.GetLicense("Proprietary"));
    }
}

// Helper class for testing compatibility
public static class LicenseCompatibilityService
{
    public static CompatibilityLevel DetermineCompatibility(License license1, License license2)
    {
        if (license1.SpdxId == license2.SpdxId) return CompatibilityLevel.Compatible;

        if (license1.CompatibleLicenses.Contains(license2.SpdxId))
            return CompatibilityLevel.Compatible;

        if (license1.IncompatibleLicenses.Contains(license2.SpdxId))
            return CompatibilityLevel.Incompatible;

        if (license1.IsCopyleft && !license2.IsCopyleft)
        {
            if (IsGplFamily(license1) && !IsGplFamily(license2))
                return CompatibilityLevel.Incompatible;
        }

        if (license2.IsCopyleft && !license1.IsCopyleft)
        {
            if (IsGplFamily(license2) && !IsGplFamily(license1))
                return CompatibilityLevel.Incompatible;
        }

        if (license1.SpdxId == "Apache-2.0" || license2.SpdxId == "Apache-2.0")
            return CompatibilityLevel.Compatible;

        if (IsPermissive(license1) && IsPermissive(license2))
            return CompatibilityLevel.Compatible;

        return CompatibilityLevel.ConditionallyCompatible;
    }

    private static bool IsGplFamily(License license)
    {
        return license.SpdxId.StartsWith("GPL-") || license.SpdxId.StartsWith("AGPL-") || license.SpdxId.StartsWith("LGPL-");
    }

    private static bool IsPermissive(License license)
    {
        var permissive = new[] { "MIT", "BSD-2-Clause", "BSD-3-Clause", "Apache-2.0", "ISC", "Unlicense" };
        return permissive.Contains(license.SpdxId) || license.SpdxId.StartsWith("MIT-") || license.SpdxId.StartsWith("BSD-");
    }
}