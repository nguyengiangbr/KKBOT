using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KKBOT.Identity;
using KKBOT.Identity.Models;
using KKBOT.Identity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KKBOT.Identity.Tests;

public class IdentityTests
{
    private static ServiceProvider CreateProvider()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("Jwt:Key", "super-secret-key-change-in-production-min-32-chars"),
                new KeyValuePair<string, string?>("Jwt:Issuer", "KKBOT"),
                new KeyValuePair<string, string?>("Jwt:Audience", "KKBOT"),
                new KeyValuePair<string, string?>("Jwt:ExpiryMinutes", "60")
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging(b => b.AddConsole());
        
        services.AddDbContext<IdentityDbContext>(options => 
            options.UseSqlite("DataSource=:memory:"));
        
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
        })
        .AddEntityFrameworkStores<IdentityDbContext>()
        .AddDefaultTokenProviders();

        services.AddScoped<IdentityService>();

        var provider = services.BuildServiceProvider();
        
        // Ensure database created
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        
        return provider;
    }

    [Fact]
    public async Task CreateUser_Success()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IdentityService>();

        var request = new CreateUserRequest(
            "test@example.com",
            "Password123!",
            "Test",
            "User",
            null,
            "Viewer"
        );

        var result = await identityService.CreateUserAsync(request, CancellationToken.None);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task CreateUser_DuplicateEmail_Fails()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IdentityService>();

        var request = new CreateUserRequest(
            "duplicate@example.com",
            "Password123!",
            "Test",
            "User",
            null,
            "Viewer"
        );

        await identityService.CreateUserAsync(request, CancellationToken.None);
        var result = await identityService.CreateUserAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == "DuplicateUserName");
    }

    [Fact]
    public async Task Login_Success()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IdentityService>();

        var request = new CreateUserRequest(
            "login@example.com",
            "Password123!",
            "Login",
            "User",
            null,
            "Viewer"
        );

        await identityService.CreateUserAsync(request, CancellationToken.None);

        var loginRequest = new LoginRequest("login@example.com", "Password123!");
        var result = await identityService.LoginAsync(loginRequest, CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result!.AccessToken));
        Assert.Equal("Bearer", result.TokenType);
        Assert.NotNull(result.User);
        Assert.Equal("login@example.com", result.User.Email);
        Assert.Contains("Viewer", result.User.Roles);
    }

    [Fact]
    public async Task Login_WrongPassword_Fails()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IdentityService>();

        var request = new CreateUserRequest(
            "wrongpass@example.com",
            "Password123!",
            "Wrong",
            "Pass",
            null,
            "Viewer"
        );

        await identityService.CreateUserAsync(request, CancellationToken.None);

        var loginRequest = new LoginRequest("wrongpass@example.com", "WrongPassword123!");
        var result = await identityService.LoginAsync(loginRequest, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Login_InactiveUser_Fails()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IdentityService>();

        var request = new CreateUserRequest(
            "inactive@example.com",
            "Password123!",
            "Inactive",
            "User",
            null,
            "Viewer"
        );

        var createResult = await identityService.CreateUserAsync(request, CancellationToken.None);
        Assert.True(createResult.Succeeded);

        // Deactivate user
        var user = await identityService.GetUserByEmailAsync("inactive@example.com", CancellationToken.None);
        Assert.NotNull(user);
        
        user!.IsActive = false;
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await userManager.UpdateAsync(user);

        var loginRequest = new LoginRequest("inactive@example.com", "Password123!");
        var result = await identityService.LoginAsync(loginRequest, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task UserRoles_AssignedCorrectly()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IdentityService>();

        var request = new CreateUserRequest(
            "roles@example.com",
            "Password123!",
            "Roles",
            "Test",
            null,
            "Operator"
        );

        await identityService.CreateUserAsync(request, CancellationToken.None);

        var user = await identityService.GetUserByEmailAsync("roles@example.com", CancellationToken.None);
        Assert.NotNull(user);

        var roles = await identityService.GetUserRolesAsync(user!.Id, CancellationToken.None);
        Assert.Contains("Operator", roles);
    }

    [Fact]
    public async Task AddRemoveRole_Works()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IdentityService>();

        var request = new CreateUserRequest(
            "roleremove@example.com",
            "Password123!",
            "Role",
            "Remove",
            null,
            "Viewer"
        );

        await identityService.CreateUserAsync(request, CancellationToken.None);

        var user = await identityService.GetUserByEmailAsync("roleremove@example.com", CancellationToken.None);
        Assert.NotNull(user);

        // Add role
        var addResult = await identityService.AddToRoleAsync(user!.Id, "Operator", CancellationToken.None);
        Assert.True(addResult.Succeeded);

        var rolesAfterAdd = await identityService.GetUserRolesAsync(user.Id, CancellationToken.None);
        Assert.Contains("Operator", rolesAfterAdd);

        // Remove role
        var removeResult = await identityService.RemoveFromRoleAsync(user.Id, "Viewer", CancellationToken.None);
        Assert.True(removeResult.Succeeded);

        var rolesAfterRemove = await identityService.GetUserRolesAsync(user.Id, CancellationToken.None);
        Assert.DoesNotContain("Viewer", rolesAfterRemove);
    }

    [Fact]
    public async Task Tenant_CRUD_Works()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IdentityService>();

        var request = new CreateTenantRequest(
            "test-tenant",
            "Test Tenant",
            "test.example.com",
            null,
            SubscriptionTier.Professional
        );

        var tenant = await identityService.CreateTenantAsync(request, CancellationToken.None);

        Assert.NotNull(tenant);
        Assert.Equal("test-tenant", tenant.Name);
        Assert.Equal("Test Tenant", tenant.DisplayName);
        Assert.Equal("test.example.com", tenant.Domain);
        Assert.Equal(SubscriptionTier.Professional, tenant.Subscription.Tier);

        var retrieved = await identityService.GetTenantAsync(tenant.Id, CancellationToken.None);
        Assert.NotNull(retrieved);
        Assert.Equal(tenant.Id, retrieved!.Id);

        var allTenants = await identityService.GetTenantsAsync(CancellationToken.None);
        Assert.Contains(allTenants, t => t.Id == tenant.Id);
    }

    [Fact]
    public async Task JwtToken_ContainsPermissions()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IdentityService>();

        var request = new CreateUserRequest(
            "token@example.com",
            "Password123!",
            "Token",
            "Test",
            null,
            "TenantAdmin"
        );

        await identityService.CreateUserAsync(request, CancellationToken.None);

        var user = await identityService.GetUserByEmailAsync("token@example.com", CancellationToken.None);
        Assert.NotNull(user);

        var roles = await identityService.GetUserRolesAsync(user!.Id, CancellationToken.None);
        var token = identityService.GenerateJwtToken(user, roles);

        Assert.False(string.IsNullOrEmpty(token));
        
        // Decode and verify
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        
        Assert.Contains(jwtToken.Claims, c => c.Type == "tenant_id");
        Assert.Contains(jwtToken.Claims, c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "TenantAdmin");
    }
}

public class PermissionTests
{
    [Fact]
    public void KKBOTPermissions_AllPermissionsDefined()
    {
        Assert.True(KKBOTPermissions.All.Length > 0);
    }

    [Fact]
    public void RolePermissions_AllRolesHavePermissions()
    {
        foreach (var kvp in KKBOTPermissions.RolePermissions)
        {
            Assert.NotEmpty(kvp.Value);
            foreach (var perm in kvp.Value)
            {
                Assert.Contains(perm, KKBOTPermissions.All);
            }
        }
    }

    [Fact]
    public void SuperAdmin_HasAllPermissions()
    {
        var superAdminPerms = KKBOTPermissions.RolePermissions["SuperAdmin"];
        foreach (var perm in KKBOTPermissions.All)
        {
            Assert.Contains(perm, superAdminPerms);
        }
    }

    [Fact]
    public void Viewer_HasOnlyReadPermissions()
    {
        var viewerPerms = KKBOTPermissions.RolePermissions["Viewer"];
        
        // All viewer permissions should end with .view or be view-related
        foreach (var perm in viewerPerms)
        {
            Assert.True(perm.EndsWith(".view") || perm == "Logs_View" || perm == "Metrics_View" || perm == "Audit_View" || perm == "Settings_View");
        }
    }

    [Fact]
    public void Operator_HasOperationalPermissions()
    {
        var operatorPerms = KKBOTPermissions.RolePermissions["Operator"];
        
        Assert.Contains("Instances_Start", operatorPerms);
        Assert.Contains("Instances_Stop", operatorPerms);
        Assert.Contains("Jobs_Create", operatorPerms);
        Assert.Contains("Jobs_Cancel", operatorPerms);
        Assert.DoesNotContain("Instances_Delete", operatorPerms);
        Assert.DoesNotContain("Users_Delete", operatorPerms);
    }
}