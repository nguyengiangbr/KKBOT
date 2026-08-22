// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Plugins.Abstractions;
using KKBOT.Plugins.Loader;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace KKBOT.API.Controllers;

/// <summary>
/// Plugin management API
/// </summary>
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

    /// <summary>
    /// Get all loaded plugins
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PluginInfo>), 200)]
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

    /// <summary>
    /// Reload plugins from directory
    /// </summary>
    [HttpPost("reload")]
    [ProducesResponseType(typeof(IEnumerable<PluginInfo>), 200)]
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
        _logger.LogInformation("Reloaded {Count} plugins from {Path}", plugins.Count, path);
        return Ok(result);
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