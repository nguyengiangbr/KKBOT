// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Marketplace.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace KKBOT.Marketplace.Server.Controllers;

[ApiController]
[Route("api/v1/security")]
[Authorize(Policy = "RequirePermission:security.scan")]
[Produces("application/json")]
[SwaggerTag("Security Scanning")]
public sealed class SecurityController : ControllerBase
{
    private readonly ISecurityScanningService _security;
    private readonly ILogger<SecurityController> _logger;

    public SecurityController(ISecurityScanningService security, ILogger<SecurityController> logger)
    {
        _security = security;
        _logger = logger;
    }

    [HttpPost("scan/{pluginId}/{version}")]
    [ProducesResponseType(typeof(SecurityScanResult), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Trigger a security scan for a plugin version")]
    public async Task<ActionResult<SecurityScanResult>> ScanPlugin(
        string pluginId,
        string version,
        CancellationToken ct = default)
    {
        try
        {
            // If file upload is provided, use it; otherwise download from storage
            Stream? pluginStream = null;
            if (Request.Form.Files.Count > 0)
            {
                var file = Request.Form.Files[0];
                pluginStream = file.OpenReadStream();
            }

            var result = await _security.ScanPluginAsync(pluginId, version, CancellationToken.None);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Scan failed", details = ex.Message });
        }
    }

    [HttpPost("scan/{pluginId}/{version}/upload")]
    [ProducesResponseType(typeof(SecurityScanResult), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Upload and scan a plugin file")]
    public async Task<ActionResult<SecurityScanResult>> ScanUploadedPlugin(
        string pluginId,
        string version,
        CancellationToken ct = default)
    {
        if (Request.Form.Files.Count == 0)
            return BadRequest(new { error = "No file uploaded" });

        var file = Request.Form.Files[0];
        if (file.Length == 0)
            return BadRequest(new { error = "Empty file" });

        // Check file size
        if (file.Length > 100 * 1024 * 1024) // 100MB limit
            return BadRequest(new { error = "File too large (max 100MB)" });

        try
        {
            var versionEntity = new PluginVersion
            {
                PluginPackage = new PluginPackage { PluginId = pluginId },
                Version = version
            };

            using var stream = file.OpenReadStream();
            var result = await _security.ScanPluginAsync(pluginId, version, CancellationToken.None);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Scan failed", details = ex.Message });
        }
    }

    [HttpGet("scan/{pluginId}/{version}")]
    [ProducesResponseType(typeof(SecurityScanResult), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get latest security scan result for a plugin version")]
    public async Task<ActionResult<SecurityScanResult>> GetLatestScan(string pluginId, string version, CancellationToken ct)
    {
        var result = await _security.GetLatestScanAsync(pluginId, version, ct);
        return result != null ? Ok(result) : NotFound();
    }

    [HttpGet("scan/{pluginId}/{version}/history")]
    [ProducesResponseType(typeof(IEnumerable<SecurityScanResult>), 200)]
    [SwaggerOperation("Get scan history for a plugin version")]
    public async Task<ActionResult<IEnumerable<SecurityScanResult>>> GetScanHistory(
        string pluginId,
        string version,
        CancellationToken ct = default)
    {
        var history = await _security.GetScanHistoryAsync(pluginId, version, ct);
        return Ok(history);
    }

    [HttpGet("scan/{pluginId}/{version}/safe")]
    [ProducesResponseType(typeof(bool), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Check if a plugin version is safe (passed security scan)")]
    public async Task<ActionResult<bool>> IsSafe(string pluginId, string version, CancellationToken ct)
    {
        var isSafe = await _security.IsPluginSafeAsync(pluginId, version, ct);
        return Ok(isSafe);
    }

    [HttpGet("rules")]
    [ProducesResponseType(typeof(IEnumerable<SecurityRuleSummary>), 200)]
    [SwaggerOperation("Get list of security scanning rules")]
    public ActionResult<IEnumerable<SecurityRuleSummary>> GetRules()
    {
        // Would return list of configured security rules
        return Ok(new List<SecurityRuleSummary>());
    }

    [HttpGet("vulnerabilities")]
    [ProducesResponseType(typeof(IEnumerable<DependencyVulnerability>), 200)]
    [SwaggerOperation("Get known vulnerabilities database stats")]
    public ActionResult<IEnumerable<DependencyVulnerability>> GetVulnerabilities(
        [FromQuery] string? package = null,
        [FromQuery] Severity? severity = null,
        CancellationToken ct = default)
    {
        // Would query vulnerability database
        return Ok(new List<DependencyVulnerability>());
    }
}

public sealed record SecurityRuleSummary
{
    public string RuleId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public Severity Severity { get; init; }
    public string Category { get; init; } = string.Empty;
    public string CweId { get; init; } = string.Empty;
    public string OwaspCategory { get; init; } = string.Empty;
    public bool Enabled { get; init; } = true;
}