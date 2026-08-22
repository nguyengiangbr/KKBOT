// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Enterprise.Abstractions;
using KKBOT.Enterprise.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace KKBOT.Enterprise.Services;

public sealed class IdentityProviderService : IIdentityProviderService
{
    private readonly EnterpriseDbContext _dbContext;
    private readonly ILogger<IdentityProviderService> _logger;
    private readonly EnterpriseOptions _options;

    public IdentityProviderService(
        EnterpriseDbContext dbContext,
        ILogger<IdentityProviderService> logger,
        IOptions<EnterpriseOptions> options)
    {
        _dbContext = dbContext;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<IdentityProvider> CreateIdentityProviderAsync(CreateIdentityProviderRequest request, string userId, CancellationToken ct = default)
    {
        var provider = new IdentityProvider
        {
            Name = request.Name,
            DisplayName = request.DisplayName,
            Type = request.Type,
            IssuerUri = request.IssuerUri,
            ClientId = request.ClientId,
            ClientSecret = EncryptSecret(request.ClientSecret),
            CallbackPath = request.CallbackPath,
            Scopes = request.Scopes,
            ResponseType = request.ResponseType,
            ResponseMode = request.ResponseMode,
            UsePkce = request.UsePkce,
            MetadataUrl = request.MetadataUrl,
            Authority = request.Authority,
            SigningAlgorithm = request.SigningAlgorithm,
            ClaimMapping = request.ClaimMapping,
            AutoProvisionUsers = request.AutoProvisionUsers,
            DefaultRole = request.DefaultRole,
            RoleMapping = request.RoleMapping,
            AllowIdpInitiatedLogin = request.AllowIdpInitiatedLogin,
            AllowIdpInitiatedLogout = request.AllowIdpInitiatedLogout,
            SloUrl = request.SloUrl,
            TokenLifetimeMinutes = request.TokenLifetimeMinutes,
            RefreshTokenLifetimeDays = request.RefreshTokenLifetimeDays,
            RequireMfa = request.RequireMfa,
            AllowedDomains = request.AllowedDomains,
            BlockedDomains = request.BlockedDomains,
            CreatedBy = userId
        };

        _dbContext.IdentityProviders.Add(provider);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created identity provider {ProviderId} ({Name}) by user {UserId}", provider.Id, provider.Name, userId);
        return provider;
    }

    public async Task<IdentityProvider?> GetIdentityProviderAsync(Guid id, string userId, CancellationToken ct = default)
    {
        return await _dbContext.IdentityProviders
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<IdentityProvider?> GetIdentityProviderByNameAsync(string name, CancellationToken ct = default)
    {
        return await _dbContext.IdentityProviders
            .FirstOrDefaultAsync(p => p.Name == name, ct);
    }

    public async Task<List<IdentityProvider>> GetIdentityProvidersAsync(string userId, IdentityProviderFilter filter, CancellationToken ct = default)
    {
        var query = _dbContext.IdentityProviders.AsQueryable();

        if (filter.Type.HasValue)
            query = query.Where(p => p.Type == filter.Type.Value);

        if (filter.IsEnabled.HasValue)
            query = query.Where(p => p.IsEnabled == filter.IsEnabled.Value);

        if (filter.IsDefault.HasValue)
            query = query.Where(p => p.IsDefault == filter.IsDefault.Value);

        if (!string.IsNullOrWhiteSpace(filter.TenantId))
            query = query.Where(p => p.TenantId == filter.TenantId);

        query = query.OrderByDescending(p => p.CreatedAt);

        return await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);
    }

    public async Task<IdentityProvider> UpdateIdentityProviderAsync(Guid id, UpdateIdentityProviderRequest request, string userId, CancellationToken ct = default)
    {
        var provider = await _dbContext.IdentityProviders.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (provider == null)
            throw new InvalidOperationException("Identity provider not found");

        if (request.Name != null) provider.Name = request.Name;
        if (request.DisplayName != null) provider.DisplayName = request.DisplayName;
        if (request.IsEnabled.HasValue) provider.IsEnabled = request.IsEnabled.Value;
        if (request.IsDefault.HasValue) provider.IsDefault = request.IsDefault.Value;
        if (request.Priority.HasValue) provider.Priority = request.Priority.Value;
        if (request.IssuerUri != null) provider.IssuerUri = request.IssuerUri;
        if (request.ClientId != null) provider.ClientId = request.ClientId;
        if (request.ClientSecret != null) provider.ClientSecret = EncryptSecret(request.ClientSecret);
        if (request.CallbackPath != null) provider.CallbackPath = request.CallbackPath;
        if (request.Scopes != null) provider.Scopes = request.Scopes;
        if (request.ResponseType != null) provider.ResponseType = request.ResponseType;
        if (request.ResponseMode != null) provider.ResponseMode = request.ResponseMode;
        if (request.UsePkce.HasValue) provider.UsePkce = request.UsePkce.Value;
        if (request.MetadataUrl != null) provider.MetadataUrl = request.MetadataUrl;
        if (request.Authority != null) provider.Authority = request.Authority;
        if (request.SigningAlgorithm != null) provider.SigningAlgorithm = request.SigningAlgorithm;
        if (request.CertificateThumbprint != null) provider.CertificateThumbprint = request.CertificateThumbprint;
        if (request.PublicKeyXml != null) provider.PublicKeyXml = request.PublicKeyXml;
        if (request.ClaimMapping != null) provider.ClaimMapping = request.ClaimMapping;
        if (request.AutoProvisionUsers.HasValue) provider.AutoProvisionUsers = request.AutoProvisionUsers.Value;
        if (request.DefaultRole != null) provider.DefaultRole = request.DefaultRole;
        if (request.RoleMapping != null) provider.RoleMapping = request.RoleMapping;
        if (request.AllowIdpInitiatedLogin.HasValue) provider.AllowIdpInitiatedLogin = request.AllowIdpInitiatedLogin.Value;
        if (request.AllowIdpInitiatedLogout.HasValue) provider.AllowIdpInitiatedLogout = request.AllowIdpInitiatedLogout.Value;
        if (request.SloUrl != null) provider.SloUrl = request.SloUrl;
        if (request.TokenLifetimeMinutes.HasValue) provider.TokenLifetimeMinutes = request.TokenLifetimeMinutes.Value;
        if (request.RefreshTokenLifetimeDays.HasValue) provider.RefreshTokenLifetimeDays = request.RefreshTokenLifetimeDays.Value;
        if (request.RequireMfa.HasValue) provider.RequireMfa = request.RequireMfa.Value;
        if (request.AllowedDomains != null) provider.AllowedDomains = request.AllowedDomains;
        if (request.BlockedDomains != null) provider.BlockedDomains = request.BlockedDomains;

        provider.UpdatedAt = DateTime.UtcNow;
        provider.UpdatedBy = userId;

        await _dbContext.SaveChangesAsync(ct);
        return provider;
    }

    public async Task<bool> DeleteIdentityProviderAsync(Guid id, string userId, CancellationToken ct = default)
    {
        var provider = await _dbContext.IdentityProviders.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (provider == null) return false;

        provider.IsDeleted = true;
        provider.UpdatedAt = DateTime.UtcNow;
        provider.UpdatedBy = userId;

        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> TestIdentityProviderAsync(Guid id, CancellationToken ct = default)
    {
        var provider = await _dbContext.IdentityProviders.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (provider == null) return false;

        try
        {
            // Test connection based on provider type
            switch (provider.Type)
            {
                case IdentityProviderType.OIDC:
                    return await TestOidcProviderAsync(provider, ct);
                case IdentityProviderType.SAML2:
                    return await TestSamlProviderAsync(provider, ct);
                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test identity provider {ProviderId}", provider.Id);
            return false;
        }
    }

    public async Task<string> GetLoginUrlAsync(Guid providerId, string returnUrl, CancellationToken ct = default)
    {
        var provider = await _dbContext.IdentityProviders.FirstOrDefaultAsync(p => p.Id == providerId, ct);
        if (provider == null)
            throw new InvalidOperationException("Identity provider not found");

        if (provider.Type == IdentityProviderType.OIDC)
        {
            return BuildOidcLoginUrl(provider, returnUrl);
        }
        else if (provider.Type == IdentityProviderType.SAML2)
        {
            return BuildSamlLoginUrl(provider, returnUrl);
        }

        throw new NotSupportedException($"Provider type {provider.Type} not supported");
    }

    public async Task<string> GetLogoutUrlAsync(Guid providerId, string returnUrl, CancellationToken ct = default)
    {
        var provider = await _dbContext.IdentityProviders.FirstOrDefaultAsync(p => p.Id == providerId, ct);
        if (provider == null)
            throw new InvalidOperationException("Identity provider not found");

        if (provider.Type == IdentityProviderType.OIDC)
        {
            return BuildOidcLogoutUrl(provider, returnUrl);
        }
        else if (provider.Type == IdentityProviderType.SAML2)
        {
            return BuildSamlLogoutUrl(provider, returnUrl);
        }

        throw new NotSupportedException($"Provider type {provider.Type} not supported");
    }

    private string EncryptSecret(string secret)
    {
        // In production, use proper encryption (DPAPI, Azure Key Vault, etc.)
        // For now, simple base64 encoding as placeholder
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(secret));
    }

    private string DecryptSecret(string encrypted)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(encrypted));
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task<bool> TestOidcProviderAsync(IdentityProvider provider, CancellationToken ct)
    {
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var metadataUrl = $"{provider.Authority.TrimEnd('/')}/.well-known/openid-configuration";
            var response = await httpClient.GetAsync(metadataUrl, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> TestSamlProviderAsync(IdentityProvider provider, CancellationToken ct)
    {
        // SAML metadata validation
        if (!string.IsNullOrEmpty(provider.MetadataUrl))
        {
            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var response = await httpClient.GetAsync(provider.MetadataUrl, ct);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
        return false;
    }

    private string BuildOidcLoginUrl(IdentityProvider provider, string returnUrl)
    {
        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = provider.ClientId,
            ["redirect_uri"] = returnUrl,
            ["response_type"] = provider.ResponseType,
            ["scope"] = provider.Scopes,
            ["response_mode"] = provider.ResponseMode,
            ["state"] = Guid.NewGuid().ToString("N"),
            ["nonce"] = Guid.NewGuid().ToString("N")
        };

        if (provider.UsePkce)
        {
            var codeVerifier = GenerateCodeVerifier();
            var codeChallenge = GenerateCodeChallenge(codeVerifier);
            // Store code_verifier in session/cache for later validation
        }

        var queryString = string.Join("&", parameters.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
        return $"{provider.Authority.TrimEnd('/')}/authorize?{queryString}";
    }

    private string BuildSamlLoginUrl(IdentityProvider provider, string returnUrl)
    {
        // SAML AuthnRequest generation
        // Simplified - in reality would generate proper SAML AuthnRequest XML
        var samlRequest = GenerateSamlAuthnRequest(provider, returnUrl);
        var encodedRequest = Convert.ToBase64String(Encoding.UTF8.GetBytes(samlRequest));
        return $"{provider.IdpSsoUrl}?SAMLRequest={Uri.EscapeDataString(encodedRequest)}&RelayState={Uri.EscapeDataString("/")}";
    }

    private string GenerateSamlAuthnRequest(IdentityProvider provider, string returnUrl)
    {
        // Simplified SAML AuthnRequest generation
        return $@"<samlp:AuthnRequest xmlns:samlp='urn:oasis:names:tc:SAML:2.0:protocol' 
            ID='{Guid.NewGuid()}' Version='2.0' IssueInstant='{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}' 
            Destination='{provider.IdpSsoUrl}' AssertionConsumerServiceUrl='{returnUrl}'
            ProtocolBinding='urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST'>
            <saml:Issuer xmlns:saml='urn:oasis:names:tc:SAML:2.0:assertion'>{provider.SpEntityId}</saml:Issuer>
            <samlp:NameIDPolicy Format='{provider.NameIdPolicyFormat}' AllowCreate='{provider.AllowCreate.ToString().ToLower()}'/>
            </samlp:AuthnRequest>";
    }

    private string GenerateCodeVerifier()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private string GenerateCodeChallenge(string codeVerifier)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(hash);
    }

    private string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private string BuildOidcLogoutUrl(IdentityProvider provider, string returnUrl)
    {
        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = provider.ClientId,
            ["post_logout_redirect_uri"] = returnUrl
        };

        var queryString = string.Join("&", parameters.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
        return $"{provider.Authority.TrimEnd('/')}/logout?{queryString}";
    }

    private string BuildSamlLogoutUrl(IdentityProvider provider, string returnUrl)
    {
        var logoutRequest = $@"<samlp:LogoutRequest xmlns:samlp='urn:oasis:names:tc:SAML:2.0:protocol'
            ID='{Guid.NewGuid()}' Version='2.0' IssueInstant='{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}'
            Destination='{provider.IdpSloUrl}'>
            <saml:Issuer xmlns:saml='urn:oasis:names:tc:SAML:2.0:assertion'>{provider.SpEntityId}</saml:Issuer>
            <saml:NameID Format='urn:oasis:names:tc:SAML:2.0:nameid-format:transient'>{Guid.NewGuid()}</saml:NameID>
            </samlp:LogoutRequest>";

        var encodedRequest = Convert.ToBase64String(Encoding.UTF8.GetBytes(logoutRequest));
        return $"{provider.IdpSloUrl}?SAMLRequest={Uri.EscapeDataString(encodedRequest)}&RelayState={Uri.EscapeDataString("/")}";
    }

    private string GenerateCodeVerifier()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}