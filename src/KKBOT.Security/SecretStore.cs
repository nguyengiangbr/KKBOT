using Microsoft.Extensions.Logging;
using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using CredentialManagement;

namespace KKBOT.Security;

/// <summary>
/// Simple secret store using Windows Credential Manager.
/// </summary>
public sealed class SecretStore
{
    private readonly ILogger<SecretStore> _logger;
    private readonly string _applicationName;

    public SecretStore(ILogger<SecretStore> logger, string applicationName)
    {
        _logger = logger;
        _applicationName = applicationName;
    }

    public void Save(string key, string secret)
    {
        var credential = new Credential
{
    Target = $"{_applicationName}:{key}",
    Username = "KKBOT",
    Password = secret,
    PersistanceType = PersistanceType.LocalMachine,
    Type = CredentialType.Generic
};
CredentialManager.SaveCredential(credential);
_logger.LogInformation("Secret '{Key}' saved to credential manager.", key);
    }
}
