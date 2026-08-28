// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace KKBOT.Security;

/// <summary>
/// Simple secret store using Windows DPAPI for encryption, persisted to the local file system.
/// </summary>
public sealed class SecretStore
{
    private readonly ILogger<SecretStore> _logger;
    private readonly string _applicationName;
    private readonly string _storeDirectory;

    public SecretStore(ILogger<SecretStore> logger, string applicationName)
    {
        _logger = logger;
        _applicationName = applicationName;
        _storeDirectory = Path.Combine(Path.GetTempPath(), "KKBOT", "secrets");
    }

    public void Save(string key, string secret)
    {
        var bytes = Encoding.UTF8.GetBytes(secret);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        Directory.CreateDirectory(_storeDirectory);
        var path = Path.Combine(_storeDirectory, $"{Sanitize(_applicationName)}_{Sanitize(key)}.bin");
        File.WriteAllBytes(path, protectedBytes);
        _logger.LogInformation("Secret '{Key}' saved.", key);
    }

    private static string Sanitize(string value) => new(value.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray());
}
