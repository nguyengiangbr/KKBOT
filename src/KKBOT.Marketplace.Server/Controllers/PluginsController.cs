// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Marketplace.Server.Models;
using KKBOT.Marketplace.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace KKBOT.Marketplace.Server.Controllers;

[ApiController]
[Route("api/v1/plugins")]
[Authorize]
[Produces("application/json")]
[SwaggerTag("Plugin Management")]
public sealed class PluginsController : ControllerBase
{
    private readonly IMarketplaceService _marketplace;
    private readonly IStorageService _storage;
    private readonly ILogger<PluginsController> _logger;

    public PluginsController(IMarketplaceService marketplace, IStorageService storage, ILogger<PluginsController> logger)
    {
        _marketplace = marketplace;
        _storage = storage;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PluginPackage>), 200)]
    [SwaggerOperation("Search and list plugins")]
    public async Task<ActionResult<PagedResult<PluginPackage>>> Search(
        [FromQuery] string? keyword,
        [FromQuery] Guid? categoryId,
        [FromQuery] bool verifiedOnly = false,
        [FromQuery] int? licenseId,
        [FromQuery] string sortBy = "createdAt",
        [FromQuery] bool sortDescending = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new PluginSearchQuery(keyword, categoryId, verifiedOnly, licenseId, sortBy, sortDescending, page, pageSize);
        var result = await _marketplace.SearchPluginsAsync(query, ct);
        return Ok(result);
    }

    [HttpGet("{pluginId}")]
    [ProducesResponseType(typeof(PluginPackage), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get plugin by ID")]
    public async Task<ActionResult<PluginPackage>> GetPlugin(string pluginId, CancellationToken ct)
    {
        var plugin = await _marketplace.GetPluginAsync(pluginId, ct);
        return plugin == null ? NotFound() : Ok(plugin);
    }

    [HttpPost]
    [Authorize(Policy = "RequirePermission:plugins.publish")]
    [ProducesResponseType(typeof(PluginPackage), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    [SwaggerOperation("Publish a new plugin")]
    public async Task<ActionResult<PluginPackage>> Publish([FromBody] PluginPublishRequest request, CancellationToken ct)
    {
        var publisherId = User.FindFirst("publisher_id")?.Value ?? User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(publisherId))
            return Unauthorized();

        try
        {
            var plugin = await _marketplace.PublishPluginAsync(request, publisherId, ct);
            return CreatedAtAction(nameof(GetPlugin), new { pluginId = plugin.PluginId }, plugin);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPut("{pluginId}")]
    [Authorize(Policy = "RequirePermission:plugins.publish")]
    [ProducesResponseType(typeof(PluginPackage), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Update plugin metadata")]
    public async Task<ActionResult<PluginPackage>> Update(string pluginId, [FromBody] PluginUpdateRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var plugin = await _marketplace.UpdatePluginAsync(pluginId, request, userId, ct);
            return Ok(plugin);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpDelete("{pluginId}")]
    [Authorize(Policy = "RequirePermission:plugins.publish")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Delete a plugin")]
    public async Task<IActionResult> Delete(string pluginId, CancellationToken ct)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var success = await _marketplace.DeletePluginAsync(pluginId, userId, ct);
        return success ? NoContent() : NotFound();
    }

    [HttpPost("{pluginId}/transfer")]
    [Authorize(Policy = "RequirePermission:plugins.publish")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Transfer plugin ownership")]
    public async Task<IActionResult> TransferOwnership(string pluginId, [FromBody] TransferOwnershipRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var success = await _marketplace.TransferOwnershipAsync(pluginId, request.NewOwnerId, userId, ct);
        return success ? Ok() : NotFound();
    }

    [HttpPost("{pluginId}/versions")]
    [Authorize(Policy = "RequirePermission:plugins.publish")]
    [ProducesResponseType(typeof(PluginVersion), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Create a new plugin version")]
    public async Task<ActionResult<PluginVersion>> CreateVersion(string pluginId, [FromBody] CreateVersionRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var version = await _marketplace.CreateVersionAsync(pluginId, request, userId, ct);
            return CreatedAtAction(nameof(GetVersion), new { pluginId, version = version.Version }, version);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("{pluginId}/versions/{version}")]
    [ProducesResponseType(typeof(PluginVersion), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get a specific plugin version")]
    public async Task<ActionResult<PluginVersion>> GetVersion(string pluginId, string version, CancellationToken ct)
    {
        var v = await _marketplace.GetVersionAsync(pluginId, version, ct);
        return v == null ? NotFound() : Ok(v);
    }

    [HttpGet("{pluginId}/versions")]
    [ProducesResponseType(typeof(PagedResult<PluginVersion>), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("List plugin versions")]
    public async Task<ActionResult<PagedResult<PluginVersion>>> GetVersions(
        string pluginId,
        [FromQuery] VersionStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new VersionQuery(status, page, pageSize);
        var result = await _marketplace.GetVersionsAsync(pluginId, query, ct);
        return Ok(result);
    }

    [HttpPut("{pluginId}/versions/{version}")]
    [Authorize(Policy = "RequirePermission:plugins.publish")]
    [ProducesResponseType(typeof(PluginVersion), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Update a plugin version")]
    public async Task<ActionResult<PluginVersion>> UpdateVersion(string pluginId, string version, [FromBody] UpdateVersionRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var v = await _marketplace.UpdateVersionAsync(pluginId, version, request, userId, ct);
            return Ok(v);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("{pluginId}/versions/{version}/deprecate")]
    [Authorize(Policy = "RequirePermission:plugins.publish")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Deprecate a plugin version")]
    public async Task<IActionResult> DeprecateVersion(string pluginId, string version, [FromBody] DeprecateVersionRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var success = await _marketplace.DeprecateVersionAsync(pluginId, version, request.DeprecationNotice, userId, ct);
        return success ? Ok() : NotFound();
    }

    [HttpDelete("{pluginId}/versions/{version}")]
    [Authorize(Policy = "RequirePermission:plugins.publish")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Delete a plugin version")]
    public async Task<IActionResult> DeleteVersion(string pluginId, string version, CancellationToken ct)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var success = await _marketplace.DeleteVersionAsync(pluginId, version, userId, ct);
        return success ? NoContent() : NotFound();
    }

    [HttpPost("{pluginId}/versions/{version}/download")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Download a plugin version")]
    public async Task<IActionResult> DownloadVersion(string pluginId, string version, CancellationToken ct)
    {
        var v = await _marketplace.GetVersionAsync(pluginId, version, ct);
        if (v == null)
            return NotFound();

        // Record download
        var userId = User.FindFirst("sub")?.Value ?? "anonymous";
        var userAgent = Request.Headers.UserAgent.ToString();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        await _marketplace.RecordDownloadAsync(v.Id, userId, userAgent, ipAddress, ct);

        // Generate presigned URL or stream from storage
        var url = await _marketplace.GetDownloadUrlAsync(v.Id, ct); // Would need to add this method
        
        // For now, redirect to presigned URL
        return Redirect(url);
    }

    [HttpGet("{pluginId}/stats")]
    [ProducesResponseType(typeof(DownloadStats), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get plugin download statistics")]
    public async Task<ActionResult<DownloadStats>> GetStats(string pluginId, CancellationToken ct)
    {
        var stats = await _marketplace.GetDownloadStatsAsync(pluginId, ct);
        return Ok(stats);
    }

    [HttpPost("{pluginId}/reviews")]
    [ProducesResponseType(typeof(PluginReview), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    [SwaggerOperation("Create a review for a plugin")]
    public async Task<ActionResult<PluginReview>> CreateReview(string pluginId, [FromBody] CreateReviewRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var review = await _marketplace.CreateReviewAsync(pluginId, request, userId, ct);
            return CreatedAtAction(nameof(GetReview), new { reviewId = review.Id }, review);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpGet("{pluginId}/reviews")]
    [ProducesResponseType(typeof(PagedResult<PluginReview>), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get plugin reviews")]
    public async Task<ActionResult<PagedResult<PluginReview>>> GetReviews(
        string pluginId,
        [FromQuery] int? rating,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new ReviewQuery(rating, page, pageSize);
        var result = await _marketplace.GetReviewsAsync(pluginId, query, ct);
        return Ok(result);
    }

    [HttpGet("reviews/{reviewId}")]
    [ProducesResponseType(typeof(PluginReview), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get a specific review")]
    public async Task<ActionResult<PluginReview>> GetReview(Guid reviewId, CancellationToken ct)
    {
        // Would need to add GetReviewById method to service
        return NotFound();
    }

    [HttpPut("reviews/{reviewId}")]
    [ProducesResponseType(typeof(PluginReview), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Update a review")]
    public async Task<ActionResult<PluginReview>> UpdateReview(Guid reviewId, [FromBody] UpdateReviewRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var review = await _marketplace.UpdateReviewAsync(reviewId, request, userId, ct);
            return Ok(review);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpDelete("reviews/{reviewId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Delete a review")]
    public async Task<IActionResult> DeleteReview(Guid reviewId, CancellationToken ct)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var success = await _marketplace.DeleteReviewAsync(reviewId, userId, ct);
        return success ? NoContent() : NotFound();
    }

    [HttpPost("reviews/{reviewId}/flag")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Flag a review for moderation")]
    public async Task<IActionResult> FlagReview(Guid reviewId, [FromBody] FlagReviewRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var success = await _marketplace.FlagReviewAsync(reviewId, request.Reason, userId, ct);
        return success ? Ok() : NotFound();
    }
}

public sealed record TransferOwnershipRequest(string NewOwnerId);
public sealed record DeprecateVersionRequest(string DeprecationNotice);
public sealed record FlagReviewRequest(string Reason);