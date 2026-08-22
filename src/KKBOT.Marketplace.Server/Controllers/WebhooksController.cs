// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Marketplace.Server.Models;
using KKBOT.Marketplace.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace KKBOT.Marketplace.Server.Controllers;

[ApiController]
[Route("api/v1/webhooks")]
[Authorize]
[Produces("application/json")]
[SwaggerTag("Webhook Management")]
public sealed class WebhooksController : ControllerBase
{
    private readonly MarketplaceDbContext _dbContext;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(MarketplaceDbContext dbContext, ILogger<WebhooksController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(typeof(WebhookSubscription), 201)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Create a webhook subscription")]
    public async Task<ActionResult<WebhookSubscription>> Create([FromBody] CreateWebhookRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var subscription = new WebhookSubscription
        {
            UserId = userId,
            Url = request.Url,
            Secret = request.Secret ?? GenerateSecret(),
            EventTypes = JsonSerializer.Serialize(request.EventTypes),
            IsActive = true
        };

        _dbContext.WebhookSubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = subscription.Id }, subscription);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WebhookSubscription>), 200)]
    [SwaggerOperation("List webhook subscriptions")]
    public async Task<ActionResult<IReadOnlyList<WebhookSubscription>>> List(CancellationToken ct)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var subscriptions = await _dbContext.WebhookSubscriptions
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);

        return Ok(subscriptions);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(WebhookSubscription), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get webhook subscription")]
    public async Task<ActionResult<WebhookSubscription>> Get(Guid id, CancellationToken ct)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var subscription = await _dbContext.WebhookSubscriptions
            .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, ct);
        return subscription == null ? NotFound() : Ok(subscription);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(WebhookSubscription), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Update a webhook subscription")]
    public async Task<ActionResult<WebhookSubscription>> Update(Guid id, [FromBody] UpdateWebhookRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var subscription = await _dbContext.WebhookSubscriptions
            .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, ct);
        if (subscription == null)
            return NotFound();

        subscription.Url = request.Url ?? subscription.Url;
        subscription.Secret = request.Secret ?? subscription.Secret;
        subscription.EventTypes = request.EventTypes != null ? JsonSerializer.Serialize(request.EventTypes) : subscription.EventTypes;
        subscription.IsActive = request.IsActive ?? subscription.IsActive;

        await _dbContext.SaveChangesAsync(ct);
        return Ok(subscription);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Delete a webhook subscription")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var subscription = await _dbContext.WebhookSubscriptions
            .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, ct);
        if (subscription == null)
            return NotFound();

        _dbContext.WebhookSubscriptions.Remove(subscription);
        await _dbContext.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("{id}/deliveries")]
    [ProducesResponseType(typeof(PagedResult<WebhookDelivery>), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get webhook delivery history")]
    public async Task<ActionResult<PagedResult<WebhookDelivery>>> GetDeliveries(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var subscription = await _dbContext.WebhookSubscriptions
            .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, ct);
        if (subscription == null)
            return NotFound();

        var q = _dbContext.WebhookDeliveries
            .Where(d => d.WebhookSubscriptionId == id)
            .OrderByDescending(d => d.CreatedAt);

        var totalCount = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return Ok(new PagedResult<WebhookDelivery>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    [HttpPost("{id}/test")]
    [ProducesResponseType(typeof(WebhookDelivery), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Send a test webhook")]
    public async Task<ActionResult<WebhookDelivery>> Test(Guid id, CancellationToken ct)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var subscription = await _dbContext.WebhookSubscriptions
            .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, ct);
        if (subscription == null)
            return NotFound();

        var payload = new { test = true, timestamp = DateTime.UtcNow, message = "Test webhook from KKBOT Marketplace" };
        var delivery = await DeliverWebhookAsync(subscription, "test", payload, ct);
        
        return Ok(delivery);
    }

    private async Task<WebhookDelivery> DeliverWebhookAsync(WebhookSubscription subscription, string eventType, object payload, CancellationToken ct)
    {
        var delivery = new WebhookDelivery
        {
            WebhookSubscriptionId = subscription.Id,
            EventType = eventType,
            Payload = JsonSerializer.Serialize(payload),
            AttemptCount = 1
        };

        _dbContext.WebhookDeliveries.Add(delivery);
        await _dbContext.SaveChangesAsync(ct);

        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var request = new HttpRequestMessage(HttpMethod.Post, subscription.Url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            // Add signature header
            if (!string.IsNullOrEmpty(subscription.Secret))
            {
                var signature = ComputeHmacSha256(JsonSerializer.Serialize(payload), subscription.Secret);
                request.Headers.Add("X-KKBOT-Signature", signature);
            }

            request.Headers.Add("X-KKBOT-Event", eventType);
            request.Headers.Add("X-KKBOT-Delivery", delivery.Id.ToString());

            var response = await httpClient.SendAsync(request, ct);
            
            delivery.ResponseStatus = ((int)response.StatusCode).ToString();
            delivery.ResponseBody = await response.Content.ReadAsStringAsync(ct);
            delivery.DeliveredAt = DateTime.UtcNow;

            if (!response.IsSuccessStatusCode)
            {
                delivery.ErrorMessage = $"HTTP {response.StatusCode}: {delivery.ResponseBody}";
            }
        }
        catch (Exception ex)
        {
            delivery.ErrorMessage = ex.Message;
        }

        await _dbContext.SaveChangesAsync(ct);
        return delivery;
    }

    private string ComputeHmacSha256(string data, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash);
    }

    private string GenerateSecret()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}

[ApiController]
[Route("api/v1/webhooks/public")]
[AllowAnonymous]
[Produces("application/json")]
[SwaggerTag("Public Webhook Endpoints")]
public sealed class PublicWebhooksController : ControllerBase
{
    private readonly MarketplaceDbContext _dbContext;
    private readonly ILogger<PublicWebhooksController> _logger;

    public PublicWebhooksController(MarketplaceDbContext dbContext, ILogger<PublicWebhooksController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpPost("receive")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [SwaggerOperation("Receive webhook from external service")]
    public async Task<IActionResult> Receive([FromHeader(Name = "X-KKBOT-Signature")] string signature, [FromHeader(Name = "X-KKBOT-Event")] string eventType, [FromBody] object payload, CancellationToken ct)
    {
        // In a real implementation, verify signature and process event
        // This endpoint would be called by external services (GitHub, Stripe, etc.)
        
        _logger.LogInformation("Received webhook: {EventType}", eventType);
        return Ok(new { received = true });
    }
}

[ApiController]
[Route("api/v1/downloads")]
[Authorize]
[Produces("application/json")]
[SwaggerTag("Download Management")]
public sealed class DownloadsController : ControllerBase
{
    private readonly IMarketplaceService _marketplace;
    private readonly IStorageService _storage;
    private readonly ILogger<DownloadsController> _logger;

    public DownloadsController(IMarketplaceService marketplace, IStorageService storage, ILogger<DownloadsController> logger)
    {
        _marketplace = marketplace;
        _storage = storage;
        _logger = logger;
    }

    [HttpGet("{pluginId}/versions/{version}/download")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Download a plugin version")]
    public async Task<IActionResult> Download(string pluginId, string version, CancellationToken ct)
    {
        var v = await _marketplace.GetVersionAsync(pluginId, version, ct);
        if (v == null)
            return NotFound();

        // Record download
        var userId = User.FindFirst("sub")?.Value ?? "anonymous";
        var userAgent = Request.Headers.UserAgent.ToString();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        await _marketplace.RecordDownloadAsync(v.Id, userId, userAgent, ipAddress, ct);

        // Generate presigned URL from storage
        var objectName = $"plugins/{pluginId}/{version}/package.dll";
        var presignedUrl = await _storage.GeneratePresignedUrlAsync(objectName, TimeSpan.FromHours(1), ct);
        
        return Redirect(presignedUrl);
    }

    [HttpGet("{pluginId}/versions/{version}/manifest")]
    [ProducesResponseType(typeof(string), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get plugin manifest")]
    public async Task<ActionResult<string>> GetManifest(string pluginId, string version, CancellationToken ct)
    {
        var v = await _marketplace.GetVersionAsync(pluginId, version, ct);
        if (v == null)
            return NotFound();

        return Content(v.ManifestJson, "application/json");
    }

    [HttpGet("stats")]
    [ProducesResponseType(typeof(DownloadStats), 200)]
    [SwaggerOperation("Get download statistics for current user")]
    public async Task<ActionResult<DownloadStats>> GetUserStats(CancellationToken ct)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        // Would need to add GetUserDownloadStats method to service
        return Ok(new DownloadStats());
    }
}