// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KKBOT.Security.Abstractions;
using KKBOT.Security;

namespace KKBOT.Security;

public static class SecurityExtensions
{
    public static IServiceCollection AddKkbotSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        // Secret manager: file‑based by default (path from config or default)
        var secretsPath = configuration["Security:SecretsPath"] ?? "secrets.json";
        services.AddSingleton<ISecretManager>(sp => new FileSecretManager(secretsPath, sp.GetRequiredService<ILogger<FileSecretManager>>()));

        // Encryption service: passphrase & salt from config (should be stored securely!)
        var passphrase = configuration["Security:EncryptionPassphrase"] ?? "dev-passphrase-change-me";
        var salt = configuration["Security:EncryptionSalt"] ?? "dev-salt-change-me";
        services.AddSingleton<IEncryptionService>(new AesEncryptionService(passphrase, salt));

        // Audit logger
        services.AddSingleton<IAuditLogger, AuditLogger>();

        return services;
    }
}