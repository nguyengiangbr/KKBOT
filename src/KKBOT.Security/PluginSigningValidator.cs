// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Plugins.Abstractions;
using Microsoft.Extensions.Logging;

namespace KKBOT.Security;

/// <summary>
/// Validates plugin assembly signatures using Ed25519/RSA.
/// When no public key is configured (development mode) every assembly is considered valid.
/// </summary>
public sealed class PluginSigningValidator
{
    private readonly ILogger<PluginSigningValidator> _logger;
    private readonly RSA? _publicKey;

    public PluginSigningValidator(ILogger<PluginSigningValidator> logger)
        : this(logger, null)
    {
    }

    public PluginSigningValidator(ILogger<PluginSigningValidator> logger, RSA? publicKey)
    {
        _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
        _publicKey = publicKey;
    }

    public bool Validate(byte[] assemblyBytes, byte[] signature, RSA publicKey)
    {
        try
        {
            var hash = SHA256.Create().ComputeHash(assemblyBytes);
            var verified = publicKey.VerifyHash(hash, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            _logger.LogInformation("Plugin signature verification result: {Result}", verified);
            return verified;
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Plugin signature verification failed");
            return false;
        }
    }

    /// <summary>
    /// Validates a plugin assembly on disk. Returns <see cref="PluginValidationOutcome.IsValid"/> = true
    /// when no public key is configured (development mode) or when the sidecar <c>.sig</c> file verifies.
    /// </summary>
    public Task<PluginValidationOutcome> ValidateAsync(string assemblyPath, CancellationToken cancellationToken = default)
    {
        if (_publicKey is null)
        {
            _logger.LogDebug("No plugin public key configured; skipping signature verification for {Path}", assemblyPath);
            return Task.FromResult(new PluginValidationOutcome(true));
        }

        var signaturePath = assemblyPath + ".sig";
        if (!System.IO.File.Exists(signaturePath))
        {
            return Task.FromResult(new PluginValidationOutcome(false, "Missing .sig sidecar file."));
        }

        try
        {
            var bytes = System.IO.File.ReadAllBytes(assemblyPath);
            var signature = System.IO.File.ReadAllBytes(signaturePath);
            var isValid = Validate(bytes, signature, _publicKey);
            return Task.FromResult(isValid
                ? new PluginValidationOutcome(true)
                : new PluginValidationOutcome(false, "Signature mismatch."));
        }
        catch (System.Exception ex)
        {
            return Task.FromResult(new PluginValidationOutcome(false, ex.Message));
        }
    }
}
