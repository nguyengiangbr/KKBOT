// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KKBOT.Identity;
using KKBOT.Identity.Services;
using KKBOT.Identity.Authorization;
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
    [RequirePermission(KKBOTPermissions.TenantRead)]
    [ProducesResponseType(typeof(IEnumerable<ApplicationTenant>), 200)]
    [SwaggerOperation("Get all tenants (SuperAdmin only) or current tenant", "Requires tenant.read permission")]
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
    [RequirePermission(KKBOTPermissions.TenantRead, "tenant", "{id}")]
    [ProducesResponseType(typeof(ApplicationTenant), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get tenant by ID", "Requires tenant.read permission")]
    public async Task<ActionResult<ApplicationTenant>> GetTenant(Guid id, CancellationToken ct)
    {
        var tenant = await _identityService.GetTenantAsync(id, ct);
        return tenant == null ? NotFound() : Ok(tenant);
    }

    [HttpPost]
    [RequirePermission(KKBOTPermissions.TenantAdmin)]
    [ProducesResponseType(typeof(ApplicationTenant), 201)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Create a new tenant", "Requires tenant.admin permission (SuperAdmin only)")]
    public async Task<ActionResult<ApplicationTenant>> CreateTenant([FromBody] CreateTenantRequest request, CancellationToken ct)
    {
        var tenant = await _identityService.CreateTenantAsync(request.Name, request.DisplayName, request.Settings, ct);
        return CreatedAtAction(nameof(GetTenant), new { id = tenant.Id }, tenant);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(KKBOTPermissions.TenantWrite, "tenant", "{id}")]
    [ProducesResponseType(typeof(ApplicationTenant), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Update a tenant", "Requires tenant.write permission")]
    public async Task<ActionResult<ApplicationTenant>> UpdateTenant(Guid id, [FromBody] UpdateTenantRequest request, CancellationToken ct)
    {
        var tenant = await _identityService.GetTenantAsync(id, ct);
        if (tenant == null) return NotFound();

        tenant = tenant with
        {
            DisplayName = request.DisplayName ?? tenant.DisplayName,
            Settings = request.Settings ?? tenant.Settings,
            IsActive = request.IsActive ?? tenant.IsActive
        };

        await _identityService.UpdateTenantAsync(tenant, ct);
        return Ok(tenant);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(KKBOTPermissions.TenantManage, "tenant", "{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Delete a tenant", "Requires tenant.manage permission (SuperAdmin only)")]
    public async Task<IActionResult> DeleteTenant(Guid id, CancellationToken ct)
    {
        var tenant = await _identityService.GetTenantAsync(id, ct);
        if (tenant == null) return NotFound();

        await _identityService.DeleteTenantAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/users")]
    [RequirePermission(KKBOTPermissions.TenantAdmin, "tenant", "{id}")]
    [ProducesResponseType(typeof(ApplicationUser), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Add user to tenant", "Requires tenant.admin permission")]
    public async Task<ActionResult<ApplicationUser>> AddUserToTenant(Guid id, [FromBody] AddUserToTenantRequest request, CancellationToken ct)
    {
        var user = await _identityService.AddUserToTenantAsync(id, request.UserId, request.Role, ct);
        return user == null ? NotFound() : CreatedAtAction(nameof(GetTenant), new { id }, user);
    }

    [HttpDelete("{tenantId:guid}/users/{userId:guid}")]
    [RequirePermission(KKBOTPermissions.TenantAdmin, "tenant", "{tenantId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Remove user from tenant", "Requires tenant.admin permission")]
    public async Task<IActionResult> RemoveUserFromTenant(Guid tenantId, Guid userId, CancellationToken ct)
    {
        var result = await _identityService.RemoveUserFromTenantAsync(tenantId, userId, ct);
        return result ? NoContent() : NotFound();
    }

    [HttpGet("{id:guid}/users")]
    [RequirePermission(KKBOTPermissions.TenantRead, "tenant", "{id}")]
    [ProducesResponseType(typeof(IEnumerable<ApplicationUser>), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get users in tenant", "Requires tenant.read permission")]
    public async Task<ActionResult<IEnumerable<ApplicationUser>>> GetTenantUsers(Guid id, CancellationToken ct)
    {
        var users = await _identityService.GetTenantUsersAsync(id, ct);
        return Ok(users);
    }
}

public record CreateTenantRequest(string Name, string DisplayName, TenantSettings? Settings = null);
public record UpdateTenantRequest(string? DisplayName = null, TenantSettings? Settings = null, bool? IsActive = null);
public record AddUserToTenantRequest(Guid UserId, string Role);