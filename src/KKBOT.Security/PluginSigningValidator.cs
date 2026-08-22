// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace KKBOT.Security;

public sealed class PluginSigningValidator
{
    private readonly ILogger<PluginSigningValidator> _logger;
    public PluginSigningValidator(ILogger<PluginSigningValidator> logger) => _logger = logger;
    public bool Validate(byte[] assemblyBytes, byte[] signature, RSA publicKey)
    {
        try
        {
            var hash = SHA256.Create().ComputeHash(assemblyBytes);
            var verified = publicKey.VerifyHash(hash, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            _logger.LogInformation("Plugin signature verification result: {Result}", verified);
            return verified;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plugin signature verification failed");
            return false;
        }
    }
}