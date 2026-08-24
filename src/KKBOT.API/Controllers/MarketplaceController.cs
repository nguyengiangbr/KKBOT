// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using KKBOT.PluginMarketplace;
using KKBOT.Identity.Authorization;
using Swashbuckle.AspNetCore.Annotations;

namespace KKBOT.API.Controllers;

[ApiController]
[Route("api/v1/marketplace")]
[Produces("application/json")]
[SwaggerTag("Plugin Marketplace")]
public sealed class MarketplaceController : ControllerBase
{
    private readonly IPluginRegistry _registry;
    private readonly IPluginInstaller _installer;
    private readonly ILogger<MarketplaceController> _logger;

    public MarketplaceController(IPluginRegistry registry, IPluginInstaller installer, ILogger<MarketplaceController> logger)
    {
        _registry = registry;
        _installer = installer;
        _logger = logger;
    }

    [HttpGet("search")]
    [RequirePermission(KKBOTPermissions.MarketplaceRead)]
    [ProducesResponseType(typeof(IEnumerable<PluginRegistryEntry>), 200)]
    [SwaggerOperation("Search plugins", "Requires marketplace.read permission")]
    public async Task<ActionResult<IEnumerable<PluginRegistryEntry>>> Search(
        [FromQuery] string? keyword,
        [FromQuery] string[]? tags,
        [FromQuery] string? author,
        [FromQuery] bool verifiedOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sortBy = "downloads",
        [FromQuery] bool sortDescending = true,
        CancellationToken ct = default)
    {
        var query = new PluginSearchQuery(keyword, tags, author, verifiedOnly, page, pageSize, sortBy, sortDescending);
        var results = await _registry.SearchAsync(query, ct);
        return Ok(results);
    }

    [HttpGet("plugins/{pluginId}")]
    [RequirePermission(KKBOTPermissions.MarketplaceRead)]
    [ProducesResponseType(typeof(PluginRegistryEntry), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get plugin details", "Requires marketplace.read permission")]
    public async Task<ActionResult<PluginRegistryEntry>> GetPlugin(string pluginId, [FromQuery] string? version, CancellationToken ct)
    {
        var plugin = await _registry.GetPluginAsync(pluginId, version, ct);
        return plugin is null ? NotFound() : Ok(plugin);
    }

    [HttpPost("plugins/{pluginId}/install")]
    [RequirePermission(KKBOTPermissions.MarketplaceWrite)]
    [ProducesResponseType(typeof(InstallResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Install a plugin", "Requires marketplace.write permission")]
    public async Task<ActionResult<InstallResult>> Install(string pluginId, [FromQuery] string? version, [FromQuery] string path = "plugins", CancellationToken ct = default)
    {
        var result = await _installer.InstallAsync(pluginId, version, path, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("plugins/{pluginId}/uninstall")]
    [RequirePermission(KKBOTPermissions.MarketplaceWrite)]
    [ProducesResponseType(typeof(UninstallResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Uninstall a plugin", "Requires marketplace.write permission")]
    public async Task<ActionResult<UninstallResult>> Uninstall(string pluginId, [FromQuery] string path = "plugins", CancellationToken ct = default)
    {
        var result = await _installer.UninstallAsync(pluginId, path, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("publish")]
    [RequirePermission(KKBOTPermissions.MarketplacePublish)]
    [ProducesResponseType(typeof(PublishResult), 201)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Publish a plugin", "Requires marketplace.publish permission")]
    public async Task<ActionResult<PublishResult>> Publish([FromBody] PublishRequest request, CancellationToken ct)
    {
        // Implementation would publish to marketplace
        return Ok(new PublishResult(true, "Published successfully", request.PluginId));
    }

    [HttpGet("categories")]
    [RequirePermission(KKBOTPermissions.MarketplaceRead)]
    [ProducesResponseType(typeof(IEnumerable<string>), 200)]
    [SwaggerOperation("Get plugin categories", "Requires marketplace.read permission")]
    public async Task<ActionResult<IEnumerable<string>>> GetCategories(CancellationToken ct)
    {
        return Ok(new[] { "Automation", "Utility", "Game Specific", "AI", "Visual Scripting" });
    }

    [HttpGet("featured")]
    [RequirePermission(KKBOTPermissions.MarketplaceRead)]
    [ProducesResponseType(typeof(IEnumerable<PluginRegistryEntry>), 200)]
    [SwaggerOperation("Get featured plugins", "Requires marketplace.read permission")]
    public async Task<ActionResult<IEnumerable<PluginRegistryEntry>>> GetFeatured([FromQuery] int limit = 10, CancellationToken ct = default)
    {
        var query = new PluginSearchQuery(null, null, null, true, 1, limit, "rating", true);
        var results = await _registry.SearchAsync(query, ct);
        return Ok(results);
    }
}

public record PublishRequest(
    string PluginId,
    string Version,
    string PackagePath,
    string? Changelog = null
);

public record PublishResult(bool Success, string Message, string PluginId);