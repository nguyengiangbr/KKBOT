// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using KKBOT.Marketplace.Server.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KKBOT.Marketplace.Server.Services;

public interface ISigningService
{
    Task<PluginSignature> SignPluginAsync(PluginVersion version, byte[] pluginData, string signerId, string signerName, CancellationToken ct = default);
    Task<bool> VerifySignatureAsync(PluginVersion version, byte[] pluginData, CancellationToken ct = default);
    Task<PluginSignature?> GetSignatureAsync(Guid pluginVersionId, CancellationToken ct = default);
    Task<bool> ValidateCertificateChainAsync(X509Certificate2 certificate, CancellationToken ct = default);
}

public sealed class SigningService : ISigningService
{
    private readonly MarketplaceDbContext _dbContext;
    private readonly ILogger<SigningService> _logger;
    private readonly SigningOptions _options;

    public SigningService(
        MarketplaceDbContext dbContext,
        ILogger<SigningService> logger,
        IOptions<SigningOptions> options)
    {
        _dbContext = dbContext;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<PluginSignature> SignPluginAsync(PluginVersion version, byte[] pluginData, string signerId, string signerName, CancellationToken ct = default)
    {
        // Compute hash of plugin data
        var hash = SHA256.HashData(pluginData);
        var hashBase64 = Convert.ToBase64String(hash);

        // Load signing key (in production, use HSM or Azure Key Vault)
        var signingKey = LoadSigningKey();
        
        // Create signature using RSA-SHA256
        using var rsa = signingKey.GetRSAPrivateKey() ?? throw new InvalidOperationException("No private key available");
        var signatureBytes = rsa.SignData(pluginData, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var signatureBase64 = Convert.ToBase64String(signatureBytes);

        // Get certificate for verification
        var certificate = signingKey.CopyWithPrivateKey(rsa);
        var certPem = ExportCertificateToPem(certificate);
        var publicKeyPem = ExportPublicKeyToPem(rsa);

        var signature = new PluginSignature
        {
            PluginVersionId = version.Id,
            Algorithm = "RSA-SHA256",
            PublicKey = publicKeyPem,
            Signature = signatureBase64,
            Certificate = certPem,
            SignedAt = DateTime.UtcNow,
            SignerId = signerId,
            SignerName = signerName,
            IsVerified = true,
            VerifiedAt = DateTime.UtcNow
        };

        _dbContext.PluginSignatures.Add(signature);
        await _dbContext.SaveChangesAsync(ct);

        // Also store hash in version for quick verification
        version.ManifestJson = UpdateManifestWithHash(version.ManifestJson, hashBase64);
        
        _logger.LogInformation("Signed plugin version {VersionId} by {SignerName}", version.Id, signerName);
        
        return signature;
    }

    public async Task<bool> VerifySignatureAsync(PluginVersion version, byte[] pluginData, CancellationToken ct = default)
    {
        var signature = await _dbContext.PluginSignatures
            .FirstOrDefaultAsync(s => s.PluginVersionId == version.Id, ct);

        if (signature == null)
        {
            _logger.LogWarning("No signature found for plugin version {VersionId}", version.Id);
            return false;
        }

        try
        {
            // Load public key from signature
            var rsa = RSA.Create();
            rsa.ImportFromPem(signature.PublicKey);
            
            var signatureBytes = Convert.FromBase64String(signature.Signature);
            var isValid = rsa.VerifyData(pluginData, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            if (isValid)
            {
                // Also verify hash matches manifest
                var currentHash = SHA256.HashData(pluginData);
                var manifestHash = ExtractHashFromManifest(version.ManifestJson);
                var currentHashBase64 = Convert.ToBase64String(currentHash);
                
                if (!string.IsNullOrEmpty(manifestHash) && manifestHash != currentHashBase64)
                {
                    _logger.LogWarning("Plugin hash mismatch for version {VersionId}", version.Id);
                    return false;
                }

                return true;
            }

            _logger.LogWarning("Signature verification failed for plugin version {VersionId}", version.Id);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying signature for version {VersionId}", version.Id);
            return false;
        }
    }

    public async Task<PluginSignature?> GetSignatureAsync(Guid pluginVersionId, CancellationToken ct = default)
    {
        return await _dbContext.PluginSignatures
            .FirstOrDefaultAsync(s => s.PluginVersionId == pluginVersionId, ct);
    }

    public async Task<bool> ValidateCertificateChainAsync(X509Certificate2 certificate, CancellationToken ct = default)
    {
        // In production, validate against trusted root CAs
        // For now, basic validation
        var now = DateTime.UtcNow;
        if (certificate.NotBefore > now || certificate.NotAfter < now)
        {
            return false;
        }

        // Check basic constraints
        var basicConstraints = certificate.Extensions["2.5.29.19"] as X509BasicConstraintsExtension;
        if (basicConstraints?.CertificateAuthority == true)
        {
            // This is a CA cert, not an end-entity cert
            return false;
        }

        // Verify key usage includes digital signature
        var keyUsage = certificate.Extensions["2.5.29.15"] as X509KeyUsageExtension;
        if (keyUsage != null && !keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature))
        {
            return false;
        }

        return true;
    }

    private RSA LoadSigningKey()
    {
        // In production, load from HSM, Azure Key Vault, or AWS KMS
        // For development, generate or load from file
        if (!string.IsNullOrEmpty(_options.PrivateKeyPath) && File.Exists(_options.PrivateKeyPath))
        {
            var pem = File.ReadAllText(_options.PrivateKeyPath);
            var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            return rsa;
        }

        // Generate ephemeral key for development
        return RSA.Create(2048);
    }

    private string ExportCertificateToPem(X509Certificate2 certificate)
    {
        var builder = new StringBuilder();
        builder.AppendLine("-----BEGIN CERTIFICATE-----");
        builder.AppendLine(Convert.ToBase64String(certificate.RawData, Base64FormattingOptions.InsertLineBreaks));
        builder.AppendLine("-----END CERTIFICATE-----");
        return builder.ToString();
    }

    private string ExportPublicKeyToPem(RSA rsa)
    {
        var builder = new StringBuilder();
        builder.AppendLine("-----BEGIN PUBLIC KEY-----");
        builder.AppendLine(Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo(), Base64FormattingOptions.InsertLineBreaks));
        builder.AppendLine("-----END PUBLIC KEY-----");
        return builder.ToString();
    }

    private string UpdateManifestWithHash(string manifestJson, string hashBase64)
    {
        try
        {
            var doc = JsonDocument.Parse(manifestJson);
            var root = doc.RootElement;
            
            var newRoot = JsonSerializer.Serialize(new
            {
                root.GetProperty("pluginId"),
                root.GetProperty("name"),
                root.GetProperty("version"),
                root.GetProperty("author"),
                root.GetProperty("description"),
                root.GetProperty("entryAssembly"),
                root.GetProperty("assets"),
                root.GetProperty("dependencies"),
                root.GetProperty("configSchema"),
                root.GetProperty("defaultConfig"),
                root.GetProperty("supportedResolutions"),
                root.GetProperty("requiredPermissions"),
                fileHash = hashBase64,
                signedAt = DateTime.UtcNow
            });

            return newRoot;
        }
        catch
        {
            return manifestJson;
        }
    }

    private string? ExtractHashFromManifest(string manifestJson)
    {
        try
        {
            var doc = JsonDocument.Parse(manifestJson);
            if (doc.RootElement.TryGetProperty("fileHash", out var hashProp))
            {
                return hashProp.GetString();
            }
        }
        catch { }
        return null;
    }
}

public sealed class SigningOptions
{
    public string PrivateKeyPath { get; set; } = string.Empty;
    public string PublicKeyPath { get; set; } = string.Empty;
    public string CertificatePath { get; set; } = string.Empty;
    public string KeyVaultUrl { get; set; } = string.Empty;
    public string KeyVaultKeyName { get; set; } = string.Empty;
    public bool RequireSigning { get; set; } = true;
    public string[] AllowedSigners { get; set; } = Array.Empty<string>();
}