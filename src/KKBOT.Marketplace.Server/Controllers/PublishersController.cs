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
[Route("api/v1/publishers")]
[Authorize]
[Produces("application/json")]
[SwaggerTag("Publisher Management")]
public sealed class PublishersController : ControllerBase
{
    private readonly IMarketplaceService _marketplace;
    private readonly ILogger<PublishersController> _logger;

    public PublishersController(IMarketplaceService marketplace, ILogger<PublishersController> logger)
    {
        _marketplace = marketplace;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(typeof(Publisher), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    [SwaggerOperation("Register as a publisher")]
    public async Task<ActionResult<Publisher>> CreatePublisher([FromBody] CreatePublisherRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var publisher = await _marketplace.CreatePublisherAsync(request, userId, CancellationToken.None);
            return CreatedAtAction(nameof(GetPublisher), new { publisherId = publisher.Id }, publisher);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(Publisher), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get current user's publisher profile")]
    public async Task<ActionResult<Publisher>> GetMyPublisher(CancellationToken ct)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var publisher = await _marketplace.GetPublisherByUserIdAsync(userId, ct);
        return publisher == null ? NotFound() : Ok(publisher);
    }

    [HttpGet("{publisherId}")]
    [ProducesResponseType(typeof(Publisher), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get publisher by ID")]
    public async Task<ActionResult<Publisher>> GetPublisher(Guid publisherId, CancellationToken ct)
    {
        var publisher = await _marketplace.GetPublisherAsync(publisherId, ct);
        return publisher == null ? NotFound() : Ok(publisher);
    }

    [HttpPut("me")]
    [ProducesResponseType(typeof(Publisher), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Update publisher profile")]
    public async Task<ActionResult<Publisher>> UpdatePublisher([FromBody] UpdatePublisherRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var publisher = await _marketplace.GetPublisherByUserIdAsync(userId, ct);
            if (publisher == null)
                return NotFound();

            var updated = await _marketplace.UpdatePublisherAsync(publisher.Id, request, userId, ct);
            return Ok(updated);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("me/payouts")]
    [ProducesResponseType(typeof(PagedResult<PublisherPayout>), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get publisher payouts")]
    public async Task<ActionResult<PagedResult<PublisherPayout>>> GetPayouts(
        [FromQuery] PayoutStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var publisher = await _marketplace.GetPublisherByUserIdAsync(userId, ct);
        if (publisher == null)
            return NotFound();

        var query = new PayoutQuery(status, page, pageSize);
        var result = await _marketplace.GetPayoutsAsync(publisher.Id, query, ct);
        return Ok(result);
    }

    [HttpGet("{publisherId}/payouts")]
    [Authorize(Policy = "RequirePermission:marketplace.moderate")]
    [ProducesResponseType(typeof(PagedResult<PublisherPayout>), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get publisher payouts (admin)")]
    public async Task<ActionResult<PagedResult<PublisherPayout>>> GetPublisherPayouts(
        Guid publisherId,
        [FromQuery] PayoutStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new PayoutQuery(status, page, pageSize);
        var result = await _marketplace.GetPayoutsAsync(publisherId, query, ct);
        return Ok(result);
    }

    [HttpGet("{publisherId}/plugins")]
    [ProducesResponseType(typeof(PagedResult<PluginPackage>), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get plugins by publisher")]
    public async Task<ActionResult<PagedResult<PluginPackage>>> GetPublisherPlugins(
        Guid publisherId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        // Would need to add GetPublisherPlugins method to service
        return NotFound();
    }
}

[ApiController]
[Route("api/v1/marketplace")]
[Authorize]
[Produces("application/json")]
[SwaggerTag("Marketplace Core")]
public sealed class MarketplaceController : ControllerBase
{
    private readonly IMarketplaceService _marketplace;
    private readonly ILogger<MarketplaceController> _logger;

    public MarketplaceController(IMarketplaceService marketplace, ILogger<MarketplaceController> logger)
    {
        _marketplace = marketplace;
        _logger = logger;
    }

    [HttpGet("tags")]
    [ProducesResponseType(typeof(IReadOnlyList<PluginTag>), 200)]
    [SwaggerOperation("Get popular tags")]
    public async Task<ActionResult<IReadOnlyList<PluginTag>>> GetTags([FromQuery] int limit = 20, CancellationToken ct = default)
    {
        var tags = await _marketplace.GetPopularTagsAsync(limit, ct);
        return Ok(tags);
    }

    [HttpGet("categories")]
    [ProducesResponseType(typeof(IReadOnlyList<PluginCategory>), 200)]
    [SwaggerOperation("Get plugin categories")]
    public async Task<ActionResult<IReadOnlyList<PluginCategory>>> GetCategories(CancellationToken ct)
    {
        var categories = await _marketplace.GetCategoriesAsync(ct);
        return Ok(categories);
    }

    [HttpGet("licenses")]
    [ProducesResponseType(typeof(IReadOnlyList<License>), 200)]
    [SwaggerOperation("Get available licenses")]
    public async Task<ActionResult<IReadOnlyList<License>>> GetLicenses(CancellationToken ct)
    {
        var licenses = await _marketplace.GetLicensesAsync(ct);
        return Ok(licenses);
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedResult<PluginPackage>), 200)]
    [SwaggerOperation("Advanced search")]
    public async Task<ActionResult<PagedResult<PluginPackage>>> Search(
        [FromQuery] string? keyword,
        [FromQuery] Guid? categoryId,
        [FromQuery] bool verifiedOnly = false,
        [FromQuery] int? licenseId,
        [FromQuery] string sortBy = "downloads",
        [FromQuery] bool sortDescending = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new PluginSearchQuery(keyword, categoryId, verifiedOnly, licenseId, sortBy, sortDescending, page, pageSize);
        var result = await _marketplace.SearchPluginsAsync(query, ct);
        return Ok(result);
    }
}

[ApiController]
[Route("api/v1/moderation")]
[Authorize(Policy = "RequirePermission:marketplace.moderate")]
[Produces("application/json")]
[SwaggerTag("Content Moderation")]
public sealed class ModerationController : ControllerBase
{
    private readonly IMarketplaceService _marketplace;
    private readonly ILogger<ModerationController> _logger;

    public ModerationController(IMarketplaceService marketplace, ILogger<ModerationController> logger)
    {
        _marketplace = marketplace;
        _logger = logger;
    }

    [HttpGet("queue")]
    [ProducesResponseType(typeof(PagedResult<ModerationQueue>), 200)]
    [SwaggerOperation("Get moderation queue")]
    public async Task<ActionResult<PagedResult<ModerationQueue>>> GetQueue(
        [FromQuery] ModerationStatus? status,
        [FromQuery] string? entityType,
        [FromQuery] ModerationPriority? priority,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new ModerationQuery(status, entityType, priority, page, pageSize);
        var result = await _marketplace.GetModerationQueueAsync(query, ct);
        return Ok(result);
    }

    [HttpPost("queue/{moderationId}/review")]
    [ProducesResponseType(typeof(ModerationQueue), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Review a moderation item")]
    public async Task<ActionResult<ModerationQueue>> Review(Guid moderationId, [FromBody] ModerateRequest request, CancellationToken ct)
    {
        var moderatorId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(moderatorId))
            return Unauthorized();

        try
        {
            var item = await _marketplace.ModerateItemAsync(moderationId, request, moderatorId, ct);
            return Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("submit")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ModerationQueue), 201)]
    [SwaggerOperation("Submit content for moderation")]
    public async Task<ActionResult<ModerationQueue>> Submit([FromBody] SubmitModerationRequest request, CancellationToken ct)
    {
        var item = await _marketplace.SubmitForModerationAsync(request, ct);
        return CreatedAtAction(nameof(GetModerationItem), new { moderationId = item.Id }, item);
    }

    [HttpGet("queue/{moderationId}")]
    [ProducesResponseType(typeof(ModerationQueue), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get moderation item details")]
    public async Task<ActionResult<ModerationQueue>> GetModerationItem(Guid moderationId, CancellationToken ct)
    {
        // Would need to add GetModerationItem method to service
        return NotFound();
    }
}