// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using KKBOT.Identity;

namespace KKBOT.Identity.Services;

public sealed class IdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IdentityDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IdentityService> _logger;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IdentityDbContext dbContext,
        IConfiguration configuration,
        ILogger<IdentityService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    // User Management
    public async Task<IdentityResult> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            TenantId = request.TenantId,
            EmailConfirmed = true,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        
        if (result.Succeeded && !string.IsNullOrEmpty(request.Role))
        {
            await _userManager.AddToRoleAsync(user, request.Role);
        }

        if (result.Succeeded)
        {
            _logger.LogInformation("Created user {UserId} ({Email})", user.Id, user.Email);
        }

        return result;
    }

    public async Task<ApplicationUser?> GetUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await _userManager.FindByIdAsync(userId.ToString());
    }

    public async Task<ApplicationUser?> GetUserByEmailAsync(string email, CancellationToken ct = default)
    {
        return await _userManager.FindByEmailAsync(email);
    }

    public async Task<IReadOnlyList<ApplicationUser>> GetUsersAsync(Guid? tenantId = null, CancellationToken ct = default)
    {
        var query = _userManager.Users.AsQueryable();
        
        if (tenantId.HasValue)
        {
            query = query.Where(u => u.TenantId == tenantId.Value);
        }

        return await query.Where(u => u.IsActive).ToListAsync(ct);
    }

    public async Task<IdentityResult> UpdateUserAsync(Guid userId, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return IdentityResult.Failed(new IdentityError { Description = "User not found" });

        user.FirstName = request.FirstName ?? user.FirstName;
        user.LastName = request.LastName ?? user.LastName;
        user.AvatarUrl = request.AvatarUrl ?? user.AvatarUrl;
        user.TimeZone = request.TimeZone ?? user.TimeZone;
        user.Language = request.Language ?? user.Language;
        user.IsActive = request.IsActive ?? user.IsActive;

        if (!string.IsNullOrEmpty(request.Email) && request.Email != user.Email)
        {
            user.Email = request.Email;
            user.UserName = request.Email;
        }

        return await _userManager.UpdateAsync(user);
    }

    public async Task<IdentityResult> DeleteUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return IdentityResult.Failed(new IdentityError { Description = "User not found" });

        // Soft delete
        user.IsActive = false;
        user.Email = $"deleted_{userId}@deleted.local";
        user.UserName = user.Email;
        return await _userManager.UpdateAsync(user);
    }

    public async Task<IdentityResult> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return IdentityResult.Failed(new IdentityError { Description = "User not found" });

        return await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
    }

    public async Task<IdentityResult> ResetPasswordAsync(Guid userId, string newPassword, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return IdentityResult.Failed(new IdentityError { Description = "User not found" });

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        return await _userManager.ResetPasswordAsync(user, token, newPassword);
    }

    // Role Management
    public async Task<IdentityResult> AddToRoleAsync(Guid userId, string role, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return IdentityResult.Failed(new IdentityError { Description = "User not found" });

        return await _userManager.AddToRoleAsync(user, role);
    }

    public async Task<IdentityResult> RemoveFromRoleAsync(Guid userId, string role, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return IdentityResult.Failed(new IdentityError { Description = "User not found" });

        return await _userManager.RemoveFromRoleAsync(user, role);
    }

    public async Task<IList<string>> GetUserRolesAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return new List<string>();

        return await _userManager.GetRolesAsync(user);
    }

    public async Task<IReadOnlyList<ApplicationRole>> GetRolesAsync(CancellationToken ct = default)
    {
        return await _roleManager.Roles.Where(r => !r.IsSystemRole || r.Name != "SuperAdmin").ToListAsync(ct);
    }

    public async Task<IdentityResult> CreateRoleAsync(string name, string description, string[]? permissions = null, CancellationToken ct = default)
    {
        var role = new ApplicationRole
        {
            Name = name,
            Description = description,
            PermissionsJson = permissions != null ? System.Text.Json.JsonSerializer.Serialize(permissions) : null
        };

        return await _roleManager.CreateAsync(role);
    }

    // JWT Token Generation
    public string GenerateJwtToken(ApplicationUser user, IList<string> roles)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"] ?? "super-secret-key-change-in-production-min-32-chars"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.GivenName, user.FirstName),
            new(ClaimTypes.Surname, user.LastName),
            new("tenant_id", user.TenantId?.ToString() ?? string.Empty)
        };

        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(int.Parse(jwtSettings["ExpiryMinutes"] ?? "60")),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<TokenResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !user.IsActive)
        {
            _logger.LogWarning("Login failed for {Email}: user not found or inactive", request.Email);
            return null;
        }

        var isValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isValid)
        {
            _logger.LogWarning("Login failed for {Email}: invalid password", request.Email);
            return null;
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = GenerateJwtToken(user, roles);
        
        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var permissions = roles
            .SelectMany(r => KKBOTPermissions.RolePermissions.GetValueOrDefault(r, Array.Empty<string>()))
            .Distinct()
            .ToArray();

        return new TokenResponse
        {
            AccessToken = token,
            ExpiresIn = int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "60") * 60,
            TokenType = "Bearer",
            User = new UserInfo
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName,
                LastName = user.LastName,
                TenantId = user.TenantId,
                Roles = roles.ToArray(),
                Permissions = permissions
            }
        };
    }

    public async Task<TokenResponse?> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        // In production, validate refresh token from database
        // For now, return null to indicate not implemented
        return null;
    }

    // Tenant Management
    public async Task<ApplicationTenant?> GetTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _dbContext.Tenants.FindAsync(new object[] { tenantId }, ct);
    }

    public async Task<IReadOnlyList<ApplicationTenant>> GetTenantsAsync(CancellationToken ct = default)
    {
        return await _dbContext.Tenants.Where(t => t.IsActive).ToListAsync(ct);
    }

    public async Task<ApplicationTenant> CreateTenantAsync(CreateTenantRequest request, CancellationToken ct = default)
    {
        var tenant = new ApplicationTenant
        {
            Name = request.Name,
            DisplayName = request.DisplayName,
            Domain = request.Domain,
            Settings = request.Settings ?? new TenantSettings(),
            Subscription = new TenantSubscription { Tier = request.Tier }
        };

        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created tenant {TenantId} ({Name})", tenant.Id, tenant.Name);
        return tenant;
    }

    public async Task<ApplicationTenant?> UpdateTenantAsync(Guid tenantId, UpdateTenantRequest request, CancellationToken ct = default)
    {
        var tenant = await _dbContext.Tenants.FindAsync(new object[] { tenantId }, ct);
        if (tenant == null) return null;

        tenant.DisplayName = request.DisplayName ?? tenant.DisplayName;
        tenant.Domain = request.Domain ?? tenant.Domain;
        tenant.LogoUrl = request.LogoUrl ?? tenant.LogoUrl;
        tenant.Settings = request.Settings ?? tenant.Settings;
        tenant.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        return tenant;
    }

    public async Task<bool> DeleteTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await _dbContext.Tenants.FindAsync(new object[] { tenantId }, ct);
        if (tenant == null) return false;

        tenant.IsActive = false;
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }
}

// Request/Response Models
public sealed record CreateUserRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    Guid? TenantId = null,
    string? Role = "Viewer"
);

public sealed record UpdateUserRequest(
    string? Email = null,
    string? FirstName = null,
    string? LastName = null,
    string? AvatarUrl = null,
    string? TimeZone = null,
    string? Language = null,
    bool? IsActive = null
);

public sealed record LoginRequest(
    string Email,
    string Password,
    bool RememberMe = false
);

public sealed record TokenResponse(
    string AccessToken,
    int ExpiresIn,
    string TokenType,
    UserInfo User
);

public sealed record UserInfo(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    Guid? TenantId,
    string[] Roles,
    string[] Permissions
);

public sealed record CreateTenantRequest(
    string Name,
    string DisplayName,
    string? Domain = null,
    TenantSettings? Settings = null,
    SubscriptionTier Tier = SubscriptionTier.Free
);

public sealed record UpdateTenantRequest(
    string? DisplayName = null,
    string? Domain = null,
    string? LogoUrl = null,
    TenantSettings? Settings = null
);