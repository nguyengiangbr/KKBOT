// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Plugins.Abstractions;
using KKBOT.Plugins.Loader;
using KKBOT.Identity.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;

namespace KKBOT.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[SwaggerTag("Manage KKBOT plugins")]
public sealed class PluginsController : ControllerBase
{
    private readonly PluginLoader _pluginLoader;
    private readonly ILogger<PluginsController> _logger;

    public PluginsController(PluginLoader pluginLoader, ILogger<PluginsController> logger)
    {
        _pluginLoader = pluginLoader;
        _logger = logger;
    }

    [HttpGet]
    [RequirePermission(KKBOTPermissions.PluginRead)]
    [ProducesResponseType(typeof(IEnumerable<PluginInfo>), 200)]
    [SwaggerOperation("Get all loaded plugins", "Requires plugin.read permission")]
    public async Task<ActionResult<IEnumerable<PluginInfo>>> GetAll([FromQuery] string path = "plugins", CancellationToken ct = default)
    {
        var plugins = await _pluginLoader.LoadPluginsAsync(path, ct);
        var result = plugins.Select(p => new PluginInfo
        {
            Id = p.Id,
            Name = p.GetManifest().Name,
            Version = p.GetManifest().Version,
            Author = p.GetManifest().Author,
            Description = p.GetManifest().Description,
            SupportedResolutions = p.GetManifest().SupportedResolutions,
            RequiredPermissions = p.GetManifest().RequiredPermissions,
            Detectors = p.GetDetectors().Select(d => new DetectorInfo { Id = d.Id, Name = d.Name }).ToList(),
            Tasks = p.GetTasks().Select(t => new TaskInfo { Id = t.Id, Name = t.Name, Type = t.Type.ToString() }).ToList(),
            States = p.GetStates().Select(s => new StateInfo { Id = s.Id, Name = s.Name, Category = s.Category }).ToList()
        });
        return Ok(result);
    }

    [HttpGet("{pluginId}")]
    [RequirePermission(KKBOTPermissions.PluginRead, "plugin", "{pluginId}")]
    [ProducesResponseType(typeof(PluginInfo), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get plugin by ID", "Requires plugin.read permission")]
    public async Task<ActionResult<PluginInfo>> GetById(string pluginId, [FromQuery] string path = "plugins", CancellationToken ct = default)
    {
        var plugins = await _pluginLoader.LoadPluginsAsync(path, ct);
        var plugin = plugins.FirstOrDefault(p => p.Id.Equals(pluginId, StringComparison.OrdinalIgnoreCase));
        if (plugin == null) return NotFound();

        var manifest = plugin.GetManifest();
        return Ok(new PluginInfo
        {
            Id = plugin.Id,
            Name = manifest.Name,
            Version = manifest.Version,
            Author = manifest.Author,
            Description = manifest.Description,
            SupportedResolutions = manifest.SupportedResolutions,
            RequiredPermissions = manifest.RequiredPermissions,
            Detectors = plugin.GetDetectors().Select(d => new DetectorInfo { Id = d.Id, Name = d.Name }).ToList(),
            Tasks = plugin.GetTasks().Select(t => new TaskInfo { Id = t.Id, Name = t.Name, Type = t.Type.ToString() }).ToList(),
            States = plugin.GetStates().Select(s => new StateInfo { Id = s.Id, Name = s.Name, Category = s.Category }).ToList()
        });
    }

    [HttpPost("reload")]
    [RequirePermission(KKBOTPermissions.PluginManage)]
    [ProducesResponseType(typeof(IEnumerable<PluginInfo>), 200)]
    [SwaggerOperation("Reload all plugins", "Requires plugin.manage permission")]
    public async Task<ActionResult<IEnumerable<PluginInfo>>> Reload([FromQuery] string path = "plugins", CancellationToken ct = default)
    {
        var plugins = await _pluginLoader.LoadPluginsAsync(path, ct);
        var result = plugins.Select(p => new PluginInfo
        {
            Id = p.Id,
            Name = p.GetManifest().Name,
            Version = p.GetManifest().Version,
            Author = p.GetManifest().Author,
            Description = p.GetManifest().Description,
            SupportedResolutions = p.GetManifest().SupportedResolutions,
            RequiredPermissions = p.GetManifest().RequiredPermissions,
            Detectors = p.GetDetectors().Select(d => new DetectorInfo { Id = d.Id, Name = d.Name }).ToList(),
            Tasks = p.GetTasks().Select(t => new TaskInfo { Id = t.Id, Name = t.Name, Type = t.Type.ToString() }).ToList(),
            States = p.GetStates().Select(s => new StateInfo { Id = s.Id, Name = s.Name, Category = s.Category }).ToList()
        });
        _logger.LogInformation("Reloaded {Count} plugins", plugins.Count);
        return Ok(result);
    }

    [HttpPost("validate")]
    [RequirePermission(KKBOTPermissions.PluginInstall)]
    [ProducesResponseType(typeof(ModuleValidationResult), 200)]
    [SwaggerOperation("Validate a plugin", "Requires plugin.install permission")]
    public async Task<ActionResult<ModuleValidationResult>> Validate([FromBody] ValidatePluginRequest request, CancellationToken ct)
    {
        // Implementation would validate plugin assets, config, etc.
        return Ok(new ModuleValidationResult(true, Array.Empty<ModuleValidationError>()));
    }
}

public record PluginInfo(
    string Id,
    string Name,
    string Version,
    string Author,
    string Description,
    Resolution[] SupportedResolutions,
    string[] RequiredPermissions,
    List<DetectorInfo> Detectors,
    List<TaskInfo> Tasks,
    List<StateInfo> States
);

public record DetectorInfo(string Id, string Name);
public record TaskInfo(string Id, string Name, string Type);
public record StateInfo(string Id, string Name, string Category);

public record ValidatePluginRequest(
    string PluginPath,
    string? Config = null
);