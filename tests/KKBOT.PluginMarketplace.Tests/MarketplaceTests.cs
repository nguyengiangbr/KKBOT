using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using KKBOT.PluginMarketplace;
using KKBOT.Plugins.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KKBOT.PluginMarketplace.Tests;

public class LocalPluginRegistryTests
{
    private readonly string _testStoragePath;
    private readonly LocalPluginRegistry _registry;

    public LocalPluginRegistryTests()
    {
        _testStoragePath = Path.Combine(Path.GetTempPath(), "kkbot-marketplace-test", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testStoragePath);

        var options = Options.Create(new MarketplaceOptions { StoragePath = _testStoragePath });
        _registry = new LocalPluginRegistry(NullLogger<LocalPluginRegistry>.Instance, options);
    }

    [Fact]
    public async Task PublishAndGetPlugin_Works()
    {
        var request = new PluginPublishRequest(
            "test-plugin",
            "1.0.0",
            "Test Author",
            "A test plugin",
            new[] { "test", "demo" },
            "https://github.com/test/plugin",
            "https://example.com/download.dll",
            "https://example.com/icon.png",
            new PluginManifest(
                "test-plugin", "Test Plugin", "1.0.0", "Test Author", "A test plugin",
                "TestPlugin.dll", Array.Empty<string>(), Array.Empty<PluginDependency>(),
                "{}", "{}", Array.Empty<Resolution>(), Array.Empty<string>()
            )
        );

        var published = await _registry.PublishAsync(request, CancellationToken.None);

        Assert.Equal("test-plugin", published.PluginId);
        Assert.Equal("1.0.0", published.Version);
        Assert.Equal("Test Author", published.Author);

        var retrieved = await _registry.GetPluginAsync("test-plugin", "1.0.0", CancellationToken.None);
        Assert.NotNull(retrieved);
        Assert.Equal("test-plugin", retrieved!.PluginId);
    }

    [Fact]
    public async Task GetLatestVersion_ReturnsHighestVersion()
    {
        await _registry.PublishAsync(CreatePublishRequest("multi-version", "1.0.0"), CancellationToken.None);
        await _registry.PublishAsync(CreatePublishRequest("multi-version", "2.0.0"), CancellationToken.None);
        await _registry.PublishAsync(CreatePublishRequest("multi-version", "1.5.0"), CancellationToken.None);

        var latest = await _registry.GetPluginAsync("multi-version", null, CancellationToken.None);

        Assert.NotNull(latest);
        Assert.Equal("2.0.0", latest!.Version);
    }

    [Fact]
    public async Task Search_FiltersByKeyword()
    {
        await _registry.PublishAsync(CreatePublishRequest("combat-plugin", "1.0.0", tags: new[] { "combat" }), CancellationToken.None);
        await _registry.PublishAsync(CreatePublishRequest("farming-plugin", "1.0.0", tags: new[] { "farming" }), CancellationToken.None);

        var results = await _registry.SearchAsync(new PluginSearchQuery { Keyword = "combat" }, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("combat-plugin", results[0].PluginId);
    }

    [Fact]
    public async Task Search_FiltersByTag()
    {
        await _registry.PublishAsync(CreatePublishRequest("tagged-plugin", "1.0.0", tags: new[] { "pvp", "combat" }), CancellationToken.None);
        await _registry.PublishAsync(CreatePublishRequest("other-plugin", "1.0.0", tags: new[] { "pve" }), CancellationToken.None);

        var results = await _registry.SearchAsync(new PluginSearchQuery { Tags = new[] { "pvp" } }, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("tagged-plugin", results[0].PluginId);
    }

    [Fact]
    public async Task Unpublish_RemovesPlugin()
    {
        await _registry.PublishAsync(CreatePublishRequest("to-remove", "1.0.0"), CancellationToken.None);

        var removed = await _registry.UnpublishAsync("to-remove", "1.0.0", CancellationToken.None);

        Assert.True(removed);

        var retrieved = await _registry.GetPluginAsync("to-remove", "1.0.0", CancellationToken.None);
        Assert.Null(retrieved);
    }

    private static PluginPublishRequest CreatePublishRequest(string id, string version, string[]? tags = null)
    {
        return new PluginPublishRequest(
            id, version, "Test Author", $"Description for {id}",
            tags ?? Array.Empty<string>(),
            "https://github.com/test/plugin",
            "https://example.com/download.dll",
            "https://example.com/icon.png",
            new PluginManifest(id, $"Plugin {id}", version, "Test Author", $"Description for {id}",
                $"{id}.dll", Array.Empty<string>(), Array.Empty<PluginDependency>(),
                "{}", "{}", Array.Empty<Resolution>(), Array.Empty<string>())
        );
    }
}

public class LocalPluginInstallerTests
{
    private readonly string _testPluginsPath;
    private readonly LocalPluginInstaller _installer;
    private readonly Mock<IPluginRegistry> _mockRegistry;
    private readonly Mock<IPluginRepository> _mockRepository;
    private readonly Mock<PluginLoader> _mockLoader;

    public LocalPluginInstallerTests()
    {
        _testPluginsPath = Path.Combine(Path.GetTempPath(), "kkbot-plugins-test", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testPluginsPath);

        _mockRegistry = new Mock<IPluginRegistry>();
        _mockRepository = new Mock<IPluginRepository>();
        _mockLoader = new Mock<PluginLoader>(NullLogger<PluginLoader>.Instance);

        var options = Options.Create(new MarketplaceOptions { PluginsPath = _testPluginsPath });

        _installer = new LocalPluginInstaller(
            _mockRegistry.Object,
            _mockRepository.Object,
            _mockLoader.Object,
            NullLogger<LocalPluginInstaller>.Instance,
            options);
    }

    [Fact]
    public async Task Install_PluginNotFound_ReturnsFailure()
    {
        _mockRegistry.Setup(r => r.GetPluginAsync("nonexistent", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PluginRegistryEntry?)null);

        var result = await _installer.InstallAsync("nonexistent", null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("not found", result.ErrorMessage);
    }

    [Fact]
    public async Task Install_Success_ReturnsSuccess()
    {
        var plugin = new PluginRegistryEntry(
            "test-plugin", "Test Plugin", "1.0.0", "Author", "Description",
            Array.Empty<string>(), "https://repo", "https://download", "https://icon",
            new PluginManifest("test-plugin", "Test Plugin", "1.0.0", "Author", "Description",
                "TestPlugin.dll", Array.Empty<string>(), Array.Empty<PluginDependency>(),
                "{}", "{}", Array.Empty<Resolution>(), Array.Empty<string>()),
            DateTime.UtcNow, 0, 0, 0, false, Array.Empty<string>()
        );

        _mockRegistry.Setup(r => r.GetPluginAsync("test-plugin", "1.0.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin);
        _mockRegistry.Setup(r => r.GetDownloadInfoAsync("test-plugin", "1.0.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginDownloadInfo("https://download", 100, "hash", "sig", plugin.Manifest));
        _mockRepository.Setup(r => r.DownloadAsync("https://download", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginPackage("test-plugin", "1.0.0", new byte[] { 1, 2, 3 }, plugin.Manifest));
        _mockRepository.Setup(r => r.VerifySignatureAsync(It.IsAny<PluginPackage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _installer.InstallAsync("test-plugin", "1.0.0", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("test-plugin", result.PluginId);
        Assert.Equal("1.0.0", result.Version);
    }

    [Fact]
    public async Task GetInstalled_ReturnsInstalledPlugins()
    {
        // First install a plugin
        var plugin = new PluginRegistryEntry(
            "installed-plugin", "Installed Plugin", "1.0.0", "Author", "Description",
            Array.Empty<string>(), "https://repo", "https://download", "https://icon",
            new PluginManifest("installed-plugin", "Installed Plugin", "1.0.0", "Author", "Description",
                "InstalledPlugin.dll", Array.Empty<string>(), Array.Empty<PluginDependency>(),
                "{}", "{}", Array.Empty<Resolution>(), Array.Empty<string>()),
            DateTime.UtcNow, 0, 0, 0, false, Array.Empty<string>()
        );

        _mockRegistry.Setup(r => r.GetPluginAsync("installed-plugin", "1.0.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin);
        _mockRegistry.Setup(r => r.GetDownloadInfoAsync("installed-plugin", "1.0.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginDownloadInfo("https://download", 100, "hash", "sig", plugin.Manifest));
        _mockRepository.Setup(r => r.DownloadAsync("https://download", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginPackage("installed-plugin", "1.0.0", new byte[] { 1, 2, 3 }, plugin.Manifest));
        _mockRepository.Setup(r => r.VerifySignatureAsync(It.IsAny<PluginPackage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _installer.InstallAsync("installed-plugin", "1.0.0", CancellationToken.None);

        var installed = await _installer.GetInstalledAsync(CancellationToken.None);

        Assert.Single(installed);
        Assert.Equal("installed-plugin", installed[0].PluginId);
    }
}

public class DependencyResolutionTests
{
    [Fact]
    public void VersionRangeParsing_Works()
    {
        // This would test a proper version range parser
        // For now, just verify the model works
        var dep = new PluginDependency("dep-plugin", ">=1.0.0 <2.0.0", false);
        
        Assert.Equal("dep-plugin", dep.PluginId);
        Assert.Equal(">=1.0.0 <2.0.0", dep.VersionRange);
        Assert.False(dep.IsOptional);
    }

    [Fact]
    public void DependencyConflict_Model_Works()
    {
        var conflict = new DependencyConflict("conflict-plugin", ">=2.0.0", "1.5.0", "version_mismatch");
        
        Assert.Equal("conflict-plugin", conflict.PluginId);
        Assert.Equal(">=2.0.0", conflict.RequiredVersion);
        Assert.Equal("1.5.0", conflict.InstalledVersion);
        Assert.Equal("version_mismatch", conflict.ConflictType);
    }
}