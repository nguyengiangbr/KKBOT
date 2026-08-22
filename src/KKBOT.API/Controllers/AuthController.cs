// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using KKBOT.Identity;
using KKBOT.Identity.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace KKBOT.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
[SwaggerTag("Authentication & Authorization")]
public sealed class AuthController : ControllerBase
{
    private readonly IdentityService _identityService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IdentityService identityService,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<AuthController> logger)
    {
        _identityService = identityService;
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(TokenResponse), 200)]
    [ProducesResponseType(401)]
    [SwaggerOperation("User login with email and password")]
    public async Task<ActionResult<TokenResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _identityService.LoginAsync(request, ct);
        
        if (result == null)
        {
            return Unauthorized(new { error = "Invalid credentials" });
        }

        // Set refresh token as HttpOnly cookie
        // Response.Cookies.Append("refresh_token", result.RefreshToken, new CookieOptions
        // {
        //     HttpOnly = true,
        //     Secure = true,
        //     SameSite = SameSiteMode.Strict,
        //     Expires = DateTimeOffset.UtcNow.AddDays(7)
        // });

        return Ok(result);
    }

    [HttpPost("register")]
    [Authorize(Policy = "RequirePermission:users.create")]
    [ProducesResponseType(typeof(ApplicationUser), 201)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Register a new user (requires users.create permission)")]
    public async Task<ActionResult<ApplicationUser>> Register([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var result = await _identityService.CreateUserAsync(request, ct);
        
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        return CreatedAtAction(nameof(GetCurrentUser), new { }, user);
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserInfo), 200)]
    [SwaggerOperation("Get current user info")]
    public async Task<ActionResult<UserInfo>> GetCurrentUser(CancellationToken ct)
    {
        var userId = HttpContext.GetUserId();
        var user = await _identityService.GetUserAsync(userId, ct);
        
        if (user == null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var permissions = roles
            .SelectMany(r => KKBOTPermissions.RolePermissions.GetValueOrDefault(r, System.Array.Empty<string>()))
            .Distinct()
            .ToArray();

        return Ok(new UserInfo
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            TenantId = user.TenantId,
            Roles = roles.ToArray(),
            Permissions = permissions
        });
    }

    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Change current user's password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var userId = HttpContext.GetUserId();
        var result = await _identityService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, ct);
        
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }

        return Ok(new { message = "Password changed successfully" });
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(200)]
    [SwaggerOperation("Logout current user")]
    public async Task<IActionResult> Logout(Cancellation ct)
    {
        await _signInManager.SignOutAsync();
        // Response.Cookies.Delete("refresh_token");
        return Ok(new { message = "Logged out successfully" });
    }
}

[ApiController]
[Route("api/v1/users")]
[Authorize]
[Produces("application/json")]
[SwaggerTag("User Management")]
public sealed class UsersController : ControllerBase
{
    private readonly IdentityService _identityService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IdentityService identityService,
        UserManager<ApplicationUser> userManager,
        ILogger<UsersController> logger)
    {
        _identityService = identityService;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "RequirePermission:users.view")]
    [ProducesResponseType(typeof(IEnumerable<ApplicationUser>), 200)]
    [SwaggerOperation("Get all users (optionally filtered by tenant)")]
    public async Task<ActionResult<IEnumerable<ApplicationUser>>> GetUsers(
        [FromQuery] Guid? tenantId,
        CancellationToken ct)
    {
        var effectiveTenantId = tenantId ?? HttpContext.GetTenantId();
        var users = await _identityService.GetUsersAsync(effectiveTenantId, ct);
        
        // Don't expose password hashes
        var safeUsers = users.Select(u => new
        {
            u.Id,
            u.UserName,
            u.Email,
            u.FirstName,
            u.LastName,
            u.AvatarUrl,
            u.CreatedAt,
            u.LastLoginAt,
            u.IsActive,
            u.TenantId,
            u.TimeZone,
            u.Language
        });

        return Ok(safeUsers);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "RequirePermission:users.view")]
    [ProducesResponseType(typeof(ApplicationUser), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get user by ID")]
    public async Task<ActionResult<ApplicationUser>> GetUser(Guid id, CancellationToken ct)
    {
        var user = await _identityService.GetUserAsync(id, ct);
        return user == null ? NotFound() : Ok(user);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequirePermission:users.edit")]
    [ProducesResponseType(typeof(ApplicationUser), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Update user")]
    public async Task<ActionResult<ApplicationUser>> UpdateUser(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var result = await _identityService.UpdateUserAsync(id, request, ct);
        
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }

        var user = await _identityService.GetUserAsync(id, ct);
        return Ok(user);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequirePermission:users.delete")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Delete user (soft delete)")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
    {
        var result = await _identityService.DeleteUserAsync(id, ct);
        
        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors.Select(e => e.Description) });
        }

        return NoContent();
    }

    [HttpPost("{id:guid}/roles")]
    [Authorize(Policy = "RequirePermission:users.manage_roles")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Add role to user")]
    public async Task<IActionResult> AddRole(Guid id, [FromBody] RoleRequest request, CancellationToken ct)
    {
        var result = await _identityService.AddToRoleAsync(id, request.Role, ct);
        
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }

        return Ok(new { message = $"Role {request.Role} added to user" });
    }

    [HttpDelete("{id:guid}/roles/{role}")]
    [Authorize(Policy = "RequirePermission:users.manage_roles")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Remove role from user")]
    public async Task<IActionResult> RemoveRole(Guid id, string role, CancellationToken ct)
    {
        var result = await _identityService.RemoveFromRoleAsync(id, role, ct);
        
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }

        return Ok(new { message = $"Role {role} removed from user" });
    }

    [HttpGet("{id:guid}/roles")]
    [Authorize(Policy = "RequirePermission:users.view")]
    [ProducesResponseType(typeof(IEnumerable<string>), 200)]
    [SwaggerOperation("Get user roles")]
    public async Task<ActionResult<IEnumerable<string>>> GetUserRoles(Guid id, CancellationToken ct)
    {
        var roles = await _identityService.GetUserRolesAsync(id, ct);
        return Ok(roles);
    }
}

[ApiController]
[Route("api/v1/roles")]
[Authorize(Policy = "RequirePermission:users.manage_roles")]
[Produces("application/json")]
[SwaggerTag("Role Management")]
public sealed class RolesController : ControllerBase
{
    private readonly IdentityService _identityService;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ILogger<RolesController> _logger;

    public RolesController(
        IdentityService identityService,
        RoleManager<ApplicationRole> roleManager,
        ILogger<RolesController> logger)
    {
        _identityService = identityService;
        _roleManager = roleManager;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ApplicationRole>), 200)]
    [SwaggerOperation("Get all roles")]
    public async Task<ActionResult<IEnumerable<ApplicationRole>>> GetRoles(CancellationToken ct)
    {
        var roles = await _identityService.GetRolesAsync(ct);
        return Ok(roles);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApplicationRole), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get role by ID")]
    public async Task<ActionResult<ApplicationRole>> GetRole(Guid id, CancellationToken ct)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());
        return role == null ? NotFound() : Ok(role);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApplicationRole), 201)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Create a new role")]
    public async Task<ActionResult<ApplicationRole>> CreateRole([FromBody] CreateRoleRequest request, CancellationToken ct)
    {
        var result = await _identityService.CreateRoleAsync(request.Name, request.Description, request.Permissions, ct);
        
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }

        var role = await _roleManager.FindByNameAsync(request.Name);
        return CreatedAtAction(nameof(GetRole), new { id = role!.Id }, role);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApplicationRole), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Update role")]
    public async Task<ActionResult<ApplicationRole>> UpdateRole(Guid id, [FromBody] UpdateRoleRequest request, CancellationToken ct)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());
        if (role == null) return NotFound();

        role.Description = request.Description ?? role.Description;
        
        if (request.Permissions != null)
        {
            role.PermissionsJson = System.Text.Json.JsonSerializer.Serialize(request.Permissions);
        }

        var result = await _roleManager.UpdateAsync(role);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }

        return Ok(role);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Delete role")]
    public async Task<IActionResult> DeleteRole(Guid id, CancellationToken ct)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());
        if (role == null) return NotFound();

        if (role.IsSystemRole)
        {
            return BadRequest(new { error = "Cannot delete system role" });
        }

        var result = await _roleManager.DeleteAsync(role);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }

        return NoContent();
    }
}

// Request Models
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record RoleRequest(string Role);
public sealed record CreateRoleRequest(string Name, string Description, string[]? Permissions = null);
public sealed record UpdateRoleRequest(string? Description = null, string[]? Permissions = null);