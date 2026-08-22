// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace KKBOT.Security.Abstractions;

public interface ISecretManager
{
    Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default);
    Task SetSecretAsync(string key, string value, CancellationToken cancellationToken = default);
    Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default);
}

public interface IEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}

public interface IAuditLogger
{
    Task LogAsync(string action, string userId, string details, CancellationToken cancellationToken = default);
}