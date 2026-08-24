// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using KKBOT.Licensing.Models;
using KKBOT.LicenseServer.Services;
using KKBOT.Observability;

namespace KKBOT.LicenseServer.Controllers;

[ApiController]
[Route("api/license")]
[Authorize]
[EnableRateLimiting("LicenseValidation")]
public sealed class LicenseController : ControllerBase
{
    private readonly ILicenseService _licenseService;
    private readonly IActivationService _activationService;
    private readonly IAnalyticsService _analyticsService;
    private readonly IAuditService _auditService;
    private readonly ILogger<LicenseController> _logger;

    public LicenseController(
        ILicenseService licenseService,
        IActivationService activationService,
        IAnalyticsService analyticsService,
        IAuditService auditService,
        ILogger<LicenseController> logger)
    {
        _licenseService = licenseService;
        _activationService = activationService;
        _analyticsService = analyticsService;
        _auditService = auditService;
        _logger = logger;
    }

    [HttpPost("validate")]
    [AllowAnonymous]
    [DisableRateLimiting]
    public async Task<ActionResult<LicenseValidationResult>> ValidateAsync(
        [FromBody] LicenseValidationRequest request,
        CancellationToken ct = default)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await _licenseService.ValidateAsync(request, ct);

        await _auditService.LogAsync("license_validated", request.Key, null, new
        {
            hardwareId = request.HardwareId,
            isValid = result.IsValid,
            error = result.ErrorMessage,
            isOffline = result.IsOfflineValidation,
            durationMs = result.ValidationDuration.TotalMilliseconds
        }, clientIp, userAgent);

        if (!result.IsValid)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("activate")]
    [AllowAnonymous]
    [EnableRateLimiting("LicenseActivation")]
    public async Task<ActionResult<LicenseActivationResponse>> ActivateAsync(
        [FromBody] LicenseActivationRequest request,
        CancellationToken ct = default)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await _licenseService.ActivateAsync(request, ct);

        await _auditService.LogAsync("license_activated", request.Key, null, new
        {
            hardwareId = request.HardwareId,
            success = result.Success,
            error = result.ErrorMessage,
            seatsUsed = result.SeatsUsed,
            seatsRemaining = result.SeatsRemaining
        }, clientIp, userAgent);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("deactivate")]
    [AllowAnonymous]
    public async Task<ActionResult> DeactivateAsync(
        [FromBody] LicenseDeactivationRequest request,
        CancellationToken ct = default)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var success = await _activationService.DeactivateAsync(request.Key, request.HardwareId, request.Reason, ct);

        await _auditService.LogAsync("license_deactivated", request.Key, null, new
        {
            hardwareId = request.HardwareId,
            reason = request.Reason,
            success
        }, clientIp);

        if (!success)
        {
            return BadRequest(new { success = false, error = "Deactivation failed" });
        }

        return Ok(new { success = true });
    }

    [HttpPost("heartbeat")]
    [AllowAnonymous]
    public async Task<ActionResult> HeartbeatAsync(
        [FromBody] HeartbeatRequest request,
        CancellationToken ct = default)
    {
        var success = await _activationService.HeartbeatAsync(request.Key, request.HardwareId, request, ct);
        
        if (!success)
        {
            return NotFound(new { error = "Activation not found" });
        }

        return Ok(new { success = true });
    }

    [HttpGet("{licenseKey}")]
    [Authorize(Policy = "LicenseViewer")]
    public async Task<ActionResult<LicenseEntity>> GetLicenseAsync(
        string licenseKey,
        CancellationToken ct = default)
    {
        var license = await _licenseService.GetLicenseAsync(licenseKey, ct);

        if (license == null)
        {
            return NotFound(new { error = "License not found" });
        }

        if (!await CanAccessLicenseAsync(licenseKey, ct))
        {
            return Forbid();
        }

        return Ok(license);
    }

    [HttpPost]
    [Authorize(Policy = "LicenseManager")]
    [EnableRateLimiting("AdminApi")]
    public async Task<ActionResult<LicenseEntity>> CreateLicenseAsync(
        [FromBody] CreateLicenseRequest request,
        CancellationToken ct = default)
    {
        var publisherId = GetPublisherId();
        
        var license = await _licenseService.CreateLicenseAsync(request, publisherId, ct);

        await _auditService.LogAsync("license_created", license.Key, GetUserId(), new
        {
            tier = request.Tier,
            seatLimit = request.SeatLimit
        });

        return CreatedAtAction(nameof(GetLicenseAsync), new { licenseKey = license.Key }, license);
    }

    [HttpPut("{licenseKey}")]
    [Authorize(Policy = "LicenseManager")]
    [EnableRateLimiting("AdminApi")]
    public async Task<ActionResult<LicenseEntity>> UpdateLicenseAsync(
        string licenseKey,
        [FromBody] UpdateLicenseRequest request,
        CancellationToken ct = default)
    {
        if (!await CanAccessLicenseAsync(licenseKey, ct))
        {
            return Forbid();
        }

        var license = await _licenseService.UpdateLicenseAsync(licenseKey, request, ct);

        if (license == null)
        {
            return NotFound(new { error = "License not found" });
        }

        await _auditService.LogAsync("license_updated", licenseKey, GetUserId(), request);

        return Ok(license);
    }

    [HttpPost("{licenseKey}/revoke")]
    [Authorize(Policy = "AdminOnly")]
    [EnableRateLimiting("AdminApi")]
    public async Task<ActionResult> RevokeLicenseAsync(
        string licenseKey,
        [FromBody] RevokeLicenseRequest request,
        CancellationToken ct = default)
    {
        var success = await _licenseService.RevokeLicenseAsync(licenseKey, request.Reason, GetUserId(), ct);

        if (!success)
        {
            return NotFound(new { error = "License not found" });
        }

        await _auditService.LogAsync("license_revoked", licenseKey, GetUserId(), new
        {
            reason = request.Reason
        });

        return Ok(new { success = true });
    }

    [HttpGet]
    [Authorize(Policy = "LicenseViewer")]
    public async Task<ActionResult> ListLicensesAsync(
        [FromQuery] LicenseFilter filter,
        CancellationToken ct = default)
    {
        var userRoles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("LicenseManager");

        if (!isAdmin)
        {
            filter.PublisherId = GetPublisherId();
        }

        var licenses = await _licenseService.GetLicensesAsync(filter, ct);
        
        return Ok(new
        {
            items = licenses,
            totalCount = licenses.Count,
            offset = filter.Offset,
            limit = filter.Limit
        });
    }

    [HttpGet("{licenseKey}/analytics")]
    [Authorize(Policy = "LicenseViewer")]
    public async Task<ActionResult<LicenseAnalytics>> GetAnalyticsAsync(
        string licenseKey,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct = default)
    {
        if (!await CanAccessLicenseAsync(licenseKey, ct))
        {
            return Forbid();
        }

        var fromDate = from ?? DateTimeOffset.UtcNow.AddDays(-30);
        var toDate = to ?? DateTimeOffset.UtcNow;

        var analytics = await _analyticsService.GetLicenseAnalyticsAsync(licenseKey, fromDate, toDate, ct);

        return Ok(analytics);
    }

    [HttpGet("{licenseKey}/health")]
    [Authorize(Policy = "LicenseViewer")]
    public async Task<ActionResult<LicenseHealthReport>> GetHealthAsync(
        string licenseKey,
        CancellationToken ct = default)
    {
        if (!await CanAccessLicenseAsync(licenseKey, ct))
        {
            return Forbid();
        }

        var report = await _analyticsService.GetLicenseHealthReportAsync(licenseKey, ct);

        return Ok(report);
    }

    [HttpGet("{licenseKey}/activations")]
    [Authorize(Policy = "LicenseViewer")]
    public async Task<ActionResult<List<LicenseActivationEntity>>> GetActivationsAsync(
        string licenseKey,
        CancellationToken ct = default)
    {
        if (!await CanAccessLicenseAsync(licenseKey, ct))
        {
            return Forbid();
        }

        var activations = await _activationService.GetActiveActivationsAsync(licenseKey, ct);

        return Ok(activations);
    }

    private async Task<bool> CanAccessLicenseAsync(string licenseKey, CancellationToken ct)
    {
        var userRoles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("LicenseManager");

        if (isAdmin) return true;

        var publisherId = GetPublisherId();
        if (string.IsNullOrEmpty(publisherId)) return false;

        return true;
    }

    private string GetPublisherId()
    {
        return User.FindFirst("publisher_id")?.Value 
            ?? User.FindFirst("publisher")?.Value 
            ?? string.Empty;
    }

    private string GetUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst("sub")?.Value 
            ?? string.Empty;
    }
}

public sealed class LicenseDeactivationRequest
{
    public string Key { get; set; } = string.Empty;
    public string HardwareId { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public sealed class RevokeLicenseRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public long TotalCount { get; set; }
    public int Offset { get; set; }
    public int Limit { get; set; }
    public bool HasNextPage => Offset + Limit < TotalCount;
    public bool HasPreviousPage => Offset > 0;
}
