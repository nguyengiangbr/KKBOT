// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KKBOT.Identity;
using KKBOT.Identity.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace KKBOT.API.Controllers;

[ApiController]
[Route("api/v1/tenants")]
[Authorize]
[Produces("application/json")]
[SwaggerTag("Tenant Management")]
public sealed class TenantsController : ControllerBase
{
    private readonly IdentityService _identityService;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(IdentityService identityService, ILogger<TenantsController> logger)
    {
        _identityService = identityService;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "RequirePermission:tenant.view")]
    [ProducesResponseType(typeof(IEnumerable<ApplicationTenant>), 200)]
    [SwaggerOperation("Get all tenants (SuperAdmin only) or current tenant")]
    public async Task<ActionResult<IEnumerable<ApplicationTenant>>> GetTenants(CancellationToken ct)
    {
        var userRoles = HttpContext.GetUserRoles();
        var isSuperAdmin = userRoles.Contains("SuperAdmin");
        
        if (!isSuperAdmin)
        {
            var tenantId = HttpContext.GetTenantId();
            if (tenantId.HasValue)
            {
                var tenant = await _identityService.GetTenantAsync(tenantId.Value, ct);
                return tenant == null ? NotFound() : Ok(new[] { tenant });
            }
            return Forbid();
        }

        var tenants = await _identityService.GetTenantsAsync(ct);
        return Ok(tenants);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "RequirePermission:tenant.view")]
    [ProducesResponseType(typeof(ApplicationTenant), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get tenant by ID")]
    public async Task<ActionResult<ApplicationTenant>> GetTenant(Guid id, CancellationToken ct)
    {
        var tenant = await _identityService.GetTenantAsync(id, ct);
        
        if (tenant == null) return NotFound();
        
        // Check access
        var userRoles = HttpContext.GetUserRoles();
        var isSuperAdmin = userRoles.Contains("SuperAdmin");
        var userTenantId = HttpContext.GetTenantId();
        
        if (!isSuperAdmin && userTenantId != id)
        {
            return Forbid();
        }

        return Ok(tenant);
    }

    [HttpPost]
    [Authorize(Policy = "RequirePermission:tenant.manage_users")] // SuperAdmin only
    [ProducesResponseType(typeof(ApplicationTenant), 201)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Create a new tenant (SuperAdmin only)")]
    public async Task<ActionResult<ApplicationTenant>> CreateTenant([FromBody] CreateTenantRequest request, CancellationToken ct)
    {
        var userRoles = HttpContext.GetUserRoles();
        if (!userRoles.Contains("SuperAdmin"))
        {
            return Forbid();
        }

        var tenant = await _identityService.CreateTenantAsync(request, ct);
        
        _logger.LogInformation("Tenant {TenantId} created by user {UserId}", tenant.Id, HttpContext.GetUserId());
        
        return CreatedAtAction(nameof(GetTenant), new { id = tenant.Id }, tenant);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequirePermission:tenant.edit")]
    [ProducesResponseType(typeof(ApplicationTenant), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Update tenant")]
    public async Task<ActionResult<ApplicationTenant>> UpdateTenant(Guid id, [FromBody] UpdateTenantRequest request, CancellationToken ct)
    {
        var tenant = await _identityService.UpdateTenantAsync(id, request, ct);
        
        if (tenant == null) return NotFound();
        
        // Check access
        var userRoles = HttpContext.GetUserRoles();
        var isSuperAdmin = userRoles.Contains("SuperAdmin");
        var userTenantId = HttpContext.GetTenantId();
        
        if (!isSuperAdmin && userTenantId != id)
        {
            return Forbid();
        }

        return Ok(tenant);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequirePermission:tenant.manage_users")] // SuperAdmin only
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Delete tenant (SuperAdmin only)")]
    public async Task<IActionResult> DeleteTenant(Guid id, CancellationToken ct)
    {
        var userRoles = HttpContext.GetUserRoles();
        if (!userRoles.Contains("SuperAdmin"))
        {
            return Forbid();
        }

        var success = await _identityService.DeleteTenantAsync(id, ct);
        
        if (!success) return NotFound();
        
        return NoContent();
    }

    [HttpGet("{id:guid}/users")]
    [Authorize(Policy = "RequirePermission:tenant.manage_users")]
    [ProducesResponseType(typeof(IEnumerable<ApplicationUser>), 200)]
    [SwaggerOperation("Get users in tenant")]
    public async Task<ActionResult<IEnumerable<ApplicationUser>>> GetTenantUsers(Guid id, CancellationToken ct)
    {
        var userRoles = HttpContext.GetUserRoles();
        var isSuperAdmin = userRoles.Contains("SuperAdmin");
        var userTenantId = HttpContext.GetTenantId();
        
        if (!isSuperAdmin && userTenantId != id)
        {
            return Forbid();
        }

        var users = await _identityService.GetUsersAsync(id, ct);
        
        var safeUsers = users.Select(u => new
        {
            u.Id,
            u.UserName,
            u.Email,
            u.FirstName,
            u.LastName,
            u.CreatedAt,
            u.LastLoginAt,
            u.IsActive
        });

        return Ok(safeUsers);
    }

    [HttpPost("{id:guid}/subscription")]
    [Authorize(Policy = "RequirePermission:tenant.manage_subscription")]
    [ProducesResponseType(typeof(ApplicationTenant), 200)]
    [SwaggerOperation("Update tenant subscription")]
    public async Task<ActionResult<ApplicationTenant>> UpdateSubscription(Guid id, [FromBody] UpdateSubscriptionRequest request, CancellationToken ct)
    {
        var tenant = await _identityService.GetTenantAsync(id, ct);
        
        if (tenant == null) return NotFound();
        
        var userRoles = HttpContext.GetUserRoles();
        var isSuperAdmin = userRoles.Contains("SuperAdmin");
        var userTenantId = HttpContext.GetTenantId();
        
        if (!isSuperAdmin && userTenantId != id)
        {
            return Forbid();
        }

        tenant.Subscription = tenant.Subscription with
        {
            Tier = request.Tier,
            ExpiresAt = request.ExpiresAt,
            MaxApiCallsPerMonth = request.MaxApiCallsPerMonth,
            MaxStorageBytes = request.MaxStorageBytes
        };

        // In real implementation, update via IdentityService
        // await _identityService.UpdateTenantAsync(id, new UpdateTenantRequest { Settings = tenant.Settings }, ct);

        return Ok(tenant);
    }
}

public sealed record UpdateSubscriptionRequest(
    SubscriptionTier Tier,
    DateTime? ExpiresAt = null,
    int? MaxApiCallsPerMonth = null,
    long? MaxStorageBytes = null
);