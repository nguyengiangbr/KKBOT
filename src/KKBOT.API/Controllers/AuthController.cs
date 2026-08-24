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
using KKBOT.Identity.Authorization;
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
    [AllowAnonymous]
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

        return Ok(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponse), 200)]
    [ProducesResponseType(401)]
    [SwaggerOperation("Refresh access token using refresh token")]
    public async Task<ActionResult<TokenResponse>> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var result = await _identityService.RefreshTokenAsync(request.RefreshToken, ct);
        
        if (result == null)
        {
            return Unauthorized(new { error = "Invalid refresh token" });
        }

        return Ok(result);
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RegisterResponse), 201)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Register a new user (public registration if enabled)")]
    public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await _identityService.RegisterAsync(request, ct);
        
        if (!result.Success)
        {
            return BadRequest(new { error = result.Message });
        }

        return CreatedAtAction(nameof(GetCurrentUser), new { }, result);
    }

    [HttpGet("me")]
    [Authorize]
    [RequirePermission(KKBOTPermissions.TenantRead)]
    [ProducesResponseType(typeof(UserProfile), 200)]
    [SwaggerOperation("Get current user profile", "Requires tenant.read permission")]
    public async Task<ActionResult<UserProfile>> GetCurrentUser(CancellationToken ct)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userId, out var id))
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        var permissions = roles
            .SelectMany(r => KKBOTPermissions.RolePermissions.GetValueOrDefault(r, System.Array.Empty<string>()))
            .Distinct()
            .ToList();

        return Ok(new UserProfile
        {
            Id = user.Id,
            Email = user.Email ?? "",
            Username = user.UserName ?? "",
            DisplayName = user.DisplayName,
            Roles = roles.ToList(),
            Permissions = permissions,
            TenantId = user.TenantId?.ToString(),
            IsActive = user.IsActive,
            LastLoginAt = user.LastLoginAt
        });
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(200)]
    [SwaggerOperation("Logout current user")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await _signInManager.SignOutAsync();
        return Ok(new { message = "Logged out successfully" });
    }

    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Change current user password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userId, out var id))
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound();

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }

        return Ok(new { message = "Password changed successfully" });
    }
}

public record LoginRequest(string Email, string Password, bool RememberMe = false);
public record RefreshTokenRequest(string RefreshToken);
public record RegisterRequest(string Email, string Password, string DisplayName, string? TenantId = null);
public record RegisterResponse(bool Success, string Message, Guid? UserId = null);
public record TokenResponse(string AccessToken, string RefreshToken, int ExpiresIn, string TokenType = "Bearer");
public record UserProfile(
    Guid Id,
    string Email,
    string Username,
    string DisplayName,
    List<string> Roles,
    List<string> Permissions,
    string? TenantId,
    bool IsActive,
    DateTimeOffset? LastLoginAt
);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);