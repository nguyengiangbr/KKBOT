// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Marketplace.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace KKBOT.Marketplace.Server.Controllers;

[ApiController]
[Route("api/v1/license")]
[Authorize(Policy = "RequirePermission:license.view")]
[Produces("application/json")]
[SwaggerTag("License Compliance")]
public sealed class LicenseController : ControllerBase
{
    private readonly ILicenseComplianceService _license;
    private readonly ILogger<LicenseController> _logger;

    public LicenseController(ILicenseComplianceService license, ILogger<LicenseController> logger)
    {
        _license = license;
        _logger = logger;
    }

    [HttpGet("compliance/{pluginId}/{version}")]
    [ProducesResponseType(typeof(LicenseComplianceReport), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get license compliance report for a plugin version")]
    public async Task<ActionResult<LicenseComplianceReport>> GetCompliance(
        string pluginId,
        string version,
        CancellationToken ct = default)
    {
        try
        {
            var report = await _license.CheckComplianceAsync(pluginId, version, ct);
            return Ok(report);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check compliance for {PluginId} v{Version}", pluginId, version);
            return StatusCode(500, new { error = "Compliance check failed", details = ex.Message });
        }
    }

    [HttpGet("compatibility/{licenseId}")]
    [ProducesResponseType(typeof(LicenseCompatibilityMatrix), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get license compatibility matrix")]
    public async Task<ActionResult<LicenseCompatibilityMatrix>> GetCompatibilityMatrix(
        string licenseId,
        CancellationToken ct = default)
    {
        try
        {
            var matrix = await _license.GetCompatibilityMatrixAsync(licenseId, ct);
            return Ok(matrix);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("obligations/{licenseId}")]
    [ProducesResponseType(typeof(LicenseObligations), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get license obligations")]
    public async Task<ActionResult<LicenseObligations>> GetObligations(
        string licenseId,
        CancellationToken ct = default)
    {
        try
        {
            var obligations = await _license.GetLicenseObligationsAsync(licenseId, ct);
            return Ok(obligations);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("conflicts/{pluginId}/{version}")]
    [ProducesResponseType(typeof(IEnumerable<LicenseConflict>), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Detect license conflicts for a plugin version")]
    public async Task<ActionResult<IEnumerable<LicenseConflict>>> DetectConflicts(
        string pluginId,
        string version,
        CancellationToken ct = default)
    {
        try
        {
            var conflicts = await _license.DetectConflictsAsync(pluginId, version, ct);
            return Ok(conflicts);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("compatible/{license1}/{license2}")]
    [ProducesResponseType(typeof(bool), 200)]
    [SwaggerOperation("Check if two licenses are compatible")]
    public async Task<ActionResult<bool>> CheckCompatibility(
        string license1,
        string license2,
        CancellationToken ct = default)
    {
        var compatible = await _license.IsLicenseCompatibleAsync(license1, license2, ct);
        return Ok(compatible);
    }

    [HttpGet("attributions/{pluginId}/{version}")]
    [ProducesResponseType(typeof(IEnumerable<LicenseAttribution>), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Generate license attributions for a plugin version")]
    public async Task<ActionResult<IEnumerable<LicenseAttribution>>> GenerateAttributions(
        string pluginId,
        string version,
        CancellationToken ct = default)
    {
        try
        {
            var attributions = await _license.GenerateAttributionsAsync(pluginId, version, ct);
            return Ok(attributions);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("sbom/{pluginId}/{version}")]
    [ProducesResponseType(typeof(SBOM), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Generate Software Bill of Materials (SBOM) for a plugin version")]
    public async Task<ActionResult<SBOM>> GenerateSBOM(
        string pluginId,
        string version,
        CancellationToken ct = default)
    {
        try
        {
            var sbom = await _license.GenerateSBOMAsync(pluginId, version, ct);
            return Ok(sbom);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("policy/evaluate")]
    [ProducesResponseType(typeof(LicensePolicyEvaluation), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Evaluate plugin against a license policy")]
    public async Task<ActionResult<LicensePolicyEvaluation>> EvaluatePolicy(
        [FromBody] EvaluatePolicyRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var evaluation = await _license.EvaluateAgainstPolicyAsync(
                request.PluginId, request.Version, request.PolicyId, ct);
            return Ok(evaluation);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("policies")]
    [ProducesResponseType(typeof(IEnumerable<LicensePolicy>), 200)]
    [SwaggerOperation("List all license policies")]
    public async Task<ActionResult<IEnumerable<LicensePolicy>>> GetPolicies(CancellationToken ct)
    {
        // Would need to add GetPolicies method to service
        return Ok(new List<LicensePolicy>());
    }

    [HttpPost("policies")]
    [Authorize(Policy = "RequirePermission:license.manage")]
    [ProducesResponseType(typeof(LicensePolicy), 201)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Create a new license policy")]
    public async Task<ActionResult<LicensePolicy>> CreatePolicy(
        [FromBody] CreatePolicyRequest request,
        CancellationToken ct = default)
    {
        // Would need to add CreatePolicy method to service
        return StatusCode(501, new { error = "Not implemented" });
    }

    [HttpGet("licenses")]
    [ProducesResponseType(typeof(IEnumerable<LicenseInfo>), 200)]
    [SwaggerOperation("List all known licenses")]
    public async Task<ActionResult<IEnumerable<LicenseInfo>>> GetLicenses(CancellationToken ct)
    {
        // Would need to add GetLicenses method to service
        return Ok(new List<LicenseInfo>());
    }

    [HttpGet("licenses/{licenseId}")]
    [ProducesResponseType(typeof(LicenseDetail), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get detailed license information")]
    public async Task<ActionResult<LicenseDetail>> GetLicense(string licenseId, CancellationToken ct)
    {
        // Would need to add GetLicense method to service
        return NotFound();
    }
}

public sealed record EvaluatePolicyRequest(string PluginId, string Version, string PolicyId);
public sealed record CreatePolicyRequest(string Name, string Description, string[] AllowedLicenses, string[] DisallowedLicenses, bool RequireCopyleftApproval, bool RequirePatentGrant, bool BlockOnConflict, LicensePolicyRule[] Rules);

public sealed record LicenseInfo(string SpdxId, string Name, string Url, bool IsOsiApproved, bool IsCopyleft);
public sealed record LicenseDetail(string SpdxId, string Name, string FullText, string Url, bool IsOsiApproved, bool IsCopyleft, bool HasPatentGrant, LicenseObligations Obligations);