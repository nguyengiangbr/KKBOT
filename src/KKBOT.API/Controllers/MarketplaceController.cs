// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using KKBOT.PluginMarketplace;
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
    [ProducesResponseType(typeof(IEnumerable<PluginRegistryEntry>), 200)]
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
    [ProducesResponseType(typeof(PluginRegistryEntry), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<PluginRegistryEntry>> GetPlugin(string pluginId, [FromQuery] string? version, CancellationToken ct)
    {
        var plugin = await _registry.GetPluginAsync(pluginId, version, ct);
        return plugin is null ? NotFound() : Ok(plugin);
    }

    [HttpGet("plugins")]
    [ProducesResponseType(typeof(IEnumerable<PluginRegistryEntry>), 200)]
    public async Task<ActionResult<IEnumerable<PluginRegistryEntry>>> GetAll(CancellationToken ct)
    {
        var plugins = await _registry.GetAllAsync(ct);
        return Ok(plugins);
    }

    [HttpPost("plugins")]
    [ProducesResponseType(typeof(PluginRegistryEntry), 201)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<PluginRegistryEntry>> Publish([FromBody] PluginPublishRequest request, CancellationToken ct)
    {
        var plugin = await _registry.PublishAsync(request, ct);
        return CreatedAtAction(nameof(GetPlugin), new { pluginId = plugin.PluginId, version = plugin.Version }, plugin);
    }

    [HttpDelete("plugins/{pluginId}/{version}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Unpublish(string pluginId, string version, CancellationToken ct)
    {
        var success = await _registry.UnpublishAsync(pluginId, version, ct);
        return success ? NoContent() : NotFound();
    }

    [HttpPost("install/{pluginId}")]
    [ProducesResponseType(typeof(InstallResult), 200)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<InstallResult>> Install(string pluginId, [FromQuery] string? version, CancellationToken ct)
    {
        var result = await _installer.InstallAsync(pluginId, version, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("update/{pluginId}")]
    [ProducesResponseType(typeof(InstallResult), 200)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<InstallResult>> Update(string pluginId, CancellationToken ct)
    {
        var result = await _installer.UpdateAsync(pluginId, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("uninstall/{pluginId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Uninstall(string pluginId, CancellationToken ct)
    {
        var success = await _installer.UninstallAsync(pluginId, ct);
        return success ? NoContent() : NotFound();
    }

    [HttpGet("installed")]
    [ProducesResponseType(typeof(IEnumerable<InstalledPluginInfo>), 200)]
    public async Task<ActionResult<IEnumerable<InstalledPluginInfo>>> GetInstalled(CancellationToken ct)
    {
        var installed = await _installer.GetInstalledAsync(ct);
        return Ok(installed);
    }

    [HttpGet("dependencies/{pluginId}")]
    [ProducesResponseType(typeof(DependencyResolutionResult), 200)]
    public async Task<ActionResult<DependencyResolutionResult>> ResolveDependencies(string pluginId, [FromQuery] string? version, CancellationToken ct)
    {
        var result = await _installer.ResolveDependenciesAsync(pluginId, version ?? "latest", ct);
        return Ok(result);
    }
}