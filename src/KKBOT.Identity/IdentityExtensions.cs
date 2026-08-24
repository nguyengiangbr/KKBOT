// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using KKBOT.Identity;
using KKBOT.Identity.Authorization;
using KKBOT.Identity.Middleware;
using KKBOT.Identity.Services;

namespace KKBOT.Identity;

public static class IdentityExtensions
{
    public static IServiceCollection AddKkbotIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        // Identity DbContext
        services.AddDbContext<IdentityDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("KkbotIdentity") 
                ?? configuration.GetConnectionString("KkbotDb") 
                ?? "DataSource=kkbot-identity.db";
            options.UseSqlite(connectionString);
        });

        // Identity
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            options.SignIn.RequireConfirmedEmail = false;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;
            options.Password.RequiredUniqueChars = 1;
            options.User.RequireUniqueEmail = true;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;
        })
        .AddEntityFrameworkStores<IdentityDbContext>()
        .AddDefaultTokenProviders();

        // JWT Authentication
        var jwtSettings = configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            jwtSettings["Key"] ?? "super-secret-key-change-in-production-min-32-chars"));

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings["Issuer"] ?? "KKBOT",
                ValidAudience = jwtSettings["Audience"] ?? "KKBOT",
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromMinutes(1),
                RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                NameClaimType = System.Security.Claims.ClaimTypes.Name
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    // Add permission claims based on roles
                    var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                    var userId = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    
                    if (Guid.TryParse(userId, out var id))
                    {
                        var user = await userManager.FindByIdAsync(id.ToString());
                        if (user != null && user.IsActive)
                        {
                            var roles = await userManager.GetRolesAsync(user);
                            var permissions = roles
                                .SelectMany(r => KKBOTPermissions.RolePermissions.GetValueOrDefault(r, System.Array.Empty<string>()))
                                .Distinct();
                            
                            var identity = context.Principal?.Identity as System.Security.Claims.ClaimsIdentity;
                            foreach (var permission in permissions)
                            {
                                identity?.AddClaim(new System.Security.Claims.Claim("permission", permission));
                            }
                            
                            // Add tenant claims
                            var tenantId = user.TenantId?.ToString();
                            if (!string.IsNullOrEmpty(tenantId))
                            {
                                identity?.AddClaim(new System.Security.Claims.Claim("tenant_id", tenantId));
                                var tenantRole = await GetUserTenantRoleAsync(userManager, user, tenantId);
                                if (!string.IsNullOrEmpty(tenantRole))
                                {
                                    identity?.AddClaim(new System.Security.Claims.Claim($"tenant_role:{tenantId}", tenantRole));
                                }
                            }
                        }
                        else if (user != null && !user.IsActive)
                        {
                            context.Fail("User is deactivated");
                        }
                    }
                }
            };
        });

        // Authorization: Register PermissionAuthorizationHandler and policies
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            // Register all KKBOT permission policies using the extension
            options.AddKkbotPermissionPolicies();
            
            // Role-based policies (backward compatibility)
            options.AddPolicy("SuperAdmin", policy => policy.RequireRole("SuperAdmin"));
            options.AddPolicy("TenantAdmin", policy => policy.RequireRole("SuperAdmin", "TenantAdmin"));
            options.AddPolicy("Operator", policy => policy.RequireRole("SuperAdmin", "TenantAdmin", "Operator"));
            options.AddPolicy("Viewer", policy => policy.RequireRole("SuperAdmin", "TenantAdmin", "Operator", "Viewer"));
            options.AddPolicy("Developer", policy => policy.RequireRole("SuperAdmin", "TenantAdmin", "Developer"));
        });

        // Register services
        services.AddScoped<IdentityService>();
        services.AddScoped<ITenantResolver, HeaderTenantResolver>();
        services.AddScoped<ITenantProvider, EFTenantProvider>();

        // Middleware
        services.AddTransient<TenantResolutionMiddleware>();

        return services;
    }

    private static async Task<string?> GetUserTenantRoleAsync(UserManager<ApplicationUser> userManager, ApplicationUser user, string tenantId)
    {
        // In a real implementation, this would query user-tenant roles
        // For now, return a default based on global role
        var roles = await userManager.GetRolesAsync(user);
        if (roles.Contains("SuperAdmin") || roles.Contains("SystemAdmin"))
            return "TenantAdmin";
        if (roles.Contains("TenantAdmin"))
            return "TenantAdmin";
        if (roles.Contains("Operator"))
            return "Operator";
        if (roles.Contains("Developer"))
            return "Developer";
        return "Viewer";
    }
}

// EF-based tenant provider
public sealed class EFTenantProvider : ITenantProvider
{
    private readonly IdentityDbContext _dbContext;

    public EFTenantProvider(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TenantContext?> GetTenantAsync(string tenantId, CancellationToken ct)
    {
        if (!Guid.TryParse(tenantId, out var id)) return null;
        
        var tenant = await _dbContext.Tenants.FindAsync(new object[] { id }, ct);
        return tenant == null ? null : new TenantContext
        {
            TenantId = tenant.Id.ToString(),
            TenantName = tenant.DisplayName,
            Settings = tenant.Settings
        };
    }

    public async Task<IReadOnlyList<TenantContext>> GetAllTenantsAsync(CancellationToken ct)
    {
        var tenants = await _dbContext.Tenants.Where(t => t.IsActive).ToListAsync(ct);
        return tenants.Select(t => new TenantContext
        {
            TenantId = t.Id.ToString(),
            TenantName = t.DisplayName,
            Settings = t.Settings
        }).ToList();
    }

    public Task<TenantContext> CreateTenantAsync(string tenantId, string tenantName, TenantSettings? settings = null, CancellationToken ct = default)
    {
        throw new NotImplementedException("Use IdentityService.CreateTenantAsync");
    }

    public Task UpdateTenantAsync(TenantContext tenant, CancellationToken ct)
    {
        throw new NotImplementedException("Use IdentityService.UpdateTenantAsync");
    }

    public Task DeleteTenantAsync(string tenantId, CancellationToken ct)
    {
        throw new NotImplementedException("Use IdentityService.DeleteTenantAsync");
    }
}