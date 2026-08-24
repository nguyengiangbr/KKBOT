// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KKBOT.Security.Secrets;

/// <summary>
/// Secret manager using Windows DPAPI for encryption.
/// Falls back to AES-256-GCM on non-Windows platforms.
/// </summary>
public sealed class DpapiSecretManager : ISecretManager
{
    private readonly ILogger<DpapiSecretManager> _logger;
    private readonly DpapiSecretManagerOptions _options;
    private readonly byte[] _entropy;
    private readonly bool _isWindows;

    public DpapiSecretManager(
        ILogger<DpapiSecretManager> logger,
        IOptions<DpapiSecretManagerOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        _isWindows = OperatingSystem.IsWindows();

        // Generate entropy from machine-specific data
        _entropy = GenerateEntropy();

        _logger.LogInformation("DPAPI Secret Manager initialized. Windows: {IsWindows}", OperatingSystem.IsWindows());
    }

    /// <summary>
    /// Stores a secret securely.
    /// </summary>
    public async Task StoreAsync(string key, string secret, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be empty", nameof(key));

        if (secret == null)
            throw new ArgumentNullException(nameof(secret));

        try
        {
            var encrypted = await EncryptAsync(secret);
            var keyPath = GetKeyPath(key);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(keyPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(keyPath, encrypted, cancellationToken);

            _logger.LogDebug("Stored secret for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store secret for key: {Key}", key);
            throw new SecretManagementException($"Failed to store secret for key: {key}", ex);
        }
    }

    /// <summary>
    /// Retrieves a secret.
    /// </summary>
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be empty", nameof(key));

        try
        {
            var keyPath = GetKeyPath(key);

            if (!File.Exists(keyPath))
            {
                return null;
            }

            var encrypted = await File.ReadAllBytesAsync(keyPath, cancellationToken);
            var secret = await DecryptAsync(encrypted);

            return secret;
        }
        catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret for key: {Key}", key);
            throw new SecretManagementException($"Failed to retrieve secret for key: {key}", ex);
        }
    }

    /// <summary>
    /// Deletes a secret.
    /// </summary>
    public async Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be empty", nameof(key));

        try
        {
            var keyPath = GetKeyPath(key);

            if (File.Exists(keyPath))
            {
                File.Delete(keyPath);
                _logger.LogInformation("Deleted secret for key: {Key}", key);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete secret for key: {Key}", key);
            return false;
        }
    }

    /// <summary>
    /// Checks if a secret exists.
    /// </summary>
    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be empty", nameof(key));

        var keyPath = GetKeyPath(key);
        return Task.FromResult(File.Exists(keyPath));
    }

    /// <summary>
    /// Lists all stored secret keys.
    /// </summary>
    public Task<string[]> ListKeysAsync(CancellationToken cancellationToken = default)
    {
        var directory = _options.StoragePath;
        if (!Directory.Exists(directory))
        {
            return Task.FromResult(Array.Empty<string>());
        }

        var files = Directory.GetFiles(directory, "*" + _options.FileExtension);
        var keys = files.Select(f => Path.GetFileNameWithoutExtension(f)).ToArray();
        return Task.FromResult(keys);
    }

    /// <summary>
    /// Rotates a secret by generating a new value and storing it.
    /// </summary>
    public async Task<string> RotateAsync(string key, Func<string, Task<string>> generator, CancellationToken cancellationToken = default)
    {
        var currentSecret = await GetAsync(key, cancellationToken);
        var newSecret = await generator(currentSecret);
        await StoreAsync(key, newSecret, cancellationToken);
        return newSecret;
    }

    private async Task<byte[]> EncryptAsync(string plaintext)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

        if (_isWindows)
        {
            // Use Windows DPAPI
            var encrypted = ProtectedData.Protect(plaintextBytes, _entropy, DataProtectionScope.CurrentUser);
            return encrypted;
        }
        else
        {
            // Use AES-256-GCM on non-Windows platforms
            return EncryptAesGcm(plaintextBytes);
        }
    }

    private async Task<string> DecryptAsync(byte[] ciphertext)
    {
        if (_isWindows)
        {
            // Use Windows DPAPI
            var plaintextBytes = ProtectedData.Unprotect(ciphertext, _entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plaintextBytes);
        }
        else
        {
            // Use AES-256-GCM on non-Windows platforms
            var plaintextBytes = DecryptAesGcm(ciphertext);
            return Encoding.UTF8.GetString(plaintextBytes);
        }
    }

    private byte[] EncryptAesGcm(byte[] plaintext)
    {
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.GCM;
        aes.Key = _options.MasterKey ?? DeriveKey(_options.MasterPassword);
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var ciphertext = new byte[plaintext.Length + 16]; // +16 for auth tag
        
        var ciphertextBytes = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
        var authTag = aes.GetAuthenticationTag();

        // Combine IV + Ciphertext + AuthTag
        var result = new byte[aes.IV.Length + ciphertextBytes.Length + 16];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(ciphertextBytes, 0, result, aes.IV.Length, ciphertextBytes.Length);
        Buffer.BlockCopy(aes.GetAuthenticationTag(), 0, result, aes.IV.Length + ciphertextBytes.Length, 16);

        return result;
    }

    private byte[] DecryptAesGcm(byte[] ciphertext)
    {
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.GCM;
        aes.Key = _options.MasterKey ?? DeriveKey(_options.MasterPassword);
        aes.GenerateIV();

        // Extract IV (first 12 bytes for GCM)
        var iv = new byte[12];
        Buffer.BlockCopy(ciphertext, 0, iv, 0, 12);
        aes.IV = iv;

        // Extract auth tag (last 16 bytes)
        var authTag = new byte[16];
        Buffer.BlockCopy(ciphertext, ciphertext.Length - 16, authTag, 0, 16);
        aes.SetAuthenticationTag(authTag);

        // Extract ciphertext
        var ciphertextLength = ciphertext.Length - 12 - 16;
        var ciphertextBytes = new byte[ciphertextLength];
        Buffer.BlockCopy(ciphertext, 12, ciphertextBytes, 0, ciphertextLength);

        using var decryptor = aes.CreateDecryptor();
        var plaintext = decryptor.TransformFinalBlock(ciphertextBytes, 0, ciphertextBytes.Length);
        return plaintext;
    }

    private byte[] DeriveKey(string password)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, _options.KeyDerivationSalt, 100000, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(32); // 256-bit key
    }

    private byte[] GenerateEntropy()
    {
        // Generate entropy from machine-specific data
        var components = new List<string>
        {
            Environment.MachineName,
            Environment.UserName,
            Environment.OSVersion.VersionString,
            Environment.ProcessorCount.ToString(),
            Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "unknown"
        };

        var combined = string.Join("|", components);
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
    }

    private string GetKeyPath(string key)
    {
        var safeKey = string.Join("_", key.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_options.StoragePath, safeKey + _options.FileExtension);
    }

    public void Dispose() { }
}

/// <summary>
/// Options for DPAPI secret manager.
/// </summary>
public sealed class DpapiSecretManagerOptions
{
    public string StoragePath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KKBOT", "Secrets");
    public string FileExtension { get; set; } = ".secret";
    public string MasterPassword { get; set; } = "KKBOT-Default-Master-Password-Change-In-Production";
    public byte[] MasterKey { get; set; } = Array.Empty<byte>();
    public byte[] KeyDerivationSalt { get; set; } = Encoding.UTF8.GetBytes("KKBOT-Secret-Salt-2024");
}

/// <summary>
/// Exception for secret management errors.
/// </summary>
public class SecretManagementException : Exception
{
    public SecretManagementException(string message) : base(message) { }
    public SecretManagementException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Interface for secret management.
/// </summary>
public interface ISecretManager
{
    Task StoreAsync(string key, string secret, CancellationToken cancellationToken = default);
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
    Task<string[]> ListKeysAsync(CancellationToken cancellationToken = default);
    Task<string> RotateAsync(string key, Func<string, Task<string>> generator, CancellationToken cancellationToken = default);
}

/// <summary>
/// Extension methods for registering DPAPI secret manager.
/// </summary>
public static class DpapiSecretManagerExtensions
{
    public static IServiceCollection AddDpapiSecretManager(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DpapiSecretManagerOptions>(configuration.GetSection("DpapiSecretManager"));
        services.AddSingleton<ISecretManager, DpapiSecretManager>();
        return services;
    }
}