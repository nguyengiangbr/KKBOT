// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Licensing.Models;

namespace KKBOT.Licensing.Client;

/// <summary>
/// License validation client library for KKBOT applications.
/// Handles license validation, offline caching, hardware fingerprinting, and auto-sync.
/// </summary>
public sealed class LicenseValidator : IDisposable
{
    private readonly LicenseValidatorOptions _options;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private readonly Timer? _syncTimer;
    private readonly CancellationTokenSource _shutdownCts = new();
    private OfflineLicenseCache? _cachedLicense;
    private bool _disposed;

    public LicenseValidator(LicenseValidatorOptions options, HttpClient? httpClient = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        // Configure HTTP client
        _httpClient.BaseAddress = new Uri(_options.LicenseServerUrl);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", $"KKBOT-LicenseValidator/{_options.ClientVersion}");

        // Load cached license on startup
        _ = LoadCacheAsync();

        // Start background sync timer
        if (_options.EnableAutoSync)
        {
            _syncTimer = new Timer(
                async _ => await SyncLicenseAsync(CancellationToken.None),
                null,
                TimeSpan.FromMinutes(_options.SyncIntervalMinutes),
                TimeSpan.FromMinutes(_options.SyncIntervalMinutes));
        }
    }

    /// <summary>
    /// Validates the license (offline-first, then online if needed).
    /// </summary>
    public async Task<LicenseValidationResult> ValidateAsync(
        string licenseKey,
        string hardwareId,
        bool forceOnline = false,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Validate key format first
            if (!LicenseKeyFormatValidator.IsValidFormat(licenseKey))
            {
                return LicenseValidationResult.Invalid(
                    LicenseValidationError.InvalidKeyFormat,
                    "Invalid license key format",
                    stopwatch.Elapsed);
            }

            // Try offline validation first
            if (!forceOnline)
            {
                var offlineResult = await ValidateOfflineAsync(licenseKey, hardwareId, cancellationToken);
                if (offlineResult.IsValid)
                {
                    stopwatch.Stop();
                    return offlineResult with { ValidationDuration = stopwatch.Elapsed, IsOfflineValidation = true };
                }

                // If offline validation failed due to grace expiry, try online
                if (offlineResult.Error == LicenseValidationError.OfflineGraceExpired && !_options.AllowOfflineFallback)
                {
                    stopwatch.Stop();
                    return offlineResult with { ValidationDuration = stopwatch.Elapsed };
                }
            }

            // Online validation
            return await ValidateOnlineAsync(licenseKey, hardwareId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return LicenseValidationResult.Invalid(
                LicenseValidationError.ServerUnreachable,
                $"Validation failed: {ex.Message}",
                stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Validates license offline using cached data.
    /// </summary>
    public async Task<LicenseValidationResult> ValidateOfflineAsync(
        string licenseKey,
        string hardwareId,
        CancellationToken cancellationToken = default)
    {
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedLicense == null || _cachedLicense.LicenseKey != licenseKey)
            {
                return LicenseValidationResult.Invalid(
                    LicenseValidationError.InvalidKeyFormat,
                    "No cached license found",
                    TimeSpan.Zero);
            }

            if (_cachedLicense.IsGraceExpired)
            {
                return LicenseValidationResult.Invalid(
                    LicenseValidationError.OfflineGraceExpired,
                    "Offline grace period expired",
                    TimeSpan.Zero);
            }

            // Verify hardware ID matches
            if (!string.IsNullOrEmpty(_cachedLicense.EncryptedLicenseData.Length.ToString()))
            {
                // We'll decrypt and check hardware ID
                var license = await DecryptLicenseAsync(_cachedLicense);
                if (license != null && !license.AllowedHardwareIds.Contains("*") && 
                    license.AllowedHardwareIds.Length > 0 && !license.AllowedHardwareIds.Contains(hardwareId))
                {
                    return LicenseValidationResult.Invalid(
                        LicenseValidationError.HardwareMismatch,
                        "Hardware ID does not match license",
                        TimeSpan.Zero);
                }

                if (license != null && license.IsExpired)
                {
                    return LicenseValidationResult.Invalid(
                        LicenseValidationError.Expired,
                        "License has expired",
                        TimeSpan.Zero);
                }
            }

            _cachedLicense.ValidationCount++;
            _cachedLicense.LastValidationAt = DateTimeOffset.UtcNow;
            await SaveCacheAsync();

            return LicenseValidationResult.Valid(
                await DecryptLicenseAsync(_cachedLicense) ?? new License { Key = licenseKey },
                TimeSpan.Zero,
                true);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Validates license online with the license server.
    /// </summary>
    public async Task<LicenseValidationResult> ValidateOnlineAsync(
        string licenseKey,
        string hardwareId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var request = new LicenseValidationRequest
            {
                Key = licenseKey,
                HardwareId = hardwareId,
                ForceOnlineCheck = true,
                ClientVersion = _options.ClientVersion
            };

            var response = await _httpClient.PostAsJsonAsync("/api/license/validate", request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<LicenseValidationResponse>(cancellationToken);
                stopwatch.Stop();
                return LicenseValidationResult.Invalid(
                    error?.ErrorCode ?? LicenseValidationError.ServerUnreachable,
                    error?.ErrorMessage ?? $"Server returned {response.StatusCode}",
                    stopwatch.Elapsed);
            }

            var responseData = await response.Content.ReadFromJsonAsync<LicenseActivationResponse>(cancellationToken);
            
            if (!responseData.Success || responseData.License == null)
            {
                stopwatch.Stop();
                return LicenseValidationResult.Invalid(
                    responseData.ErrorCode,
                    responseData.ErrorMessage ?? "Invalid license",
                    stopwatch.Elapsed);
            }

            // Update cache with fresh license data
            await CacheLicenseAsync(responseData.License, hardwareId);

            stopwatch.Stop();
            return LicenseValidationResult.Valid(
                responseData.License,
                stopwatch.Elapsed,
                false);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            return LicenseValidationResult.Invalid(
                LicenseValidationError.ServerUnreachable,
                $"Network error: {ex.Message}",
                stopwatch.Elapsed);
        }
        catch (TaskCanceledException)
        {
            stopwatch.Stop();
            return LicenseValidationResult.Invalid(
                LicenseValidationError.ServerUnreachable,
                "Request timeout",
                stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Activates a license key on the server.
    /// </summary>
    public async Task<LicenseActivationResponse> ActivateAsync(
        string licenseKey,
        string hardwareId,
        CancellationToken cancellationToken = default)
    {
        var request = new LicenseActivationRequest
        {
            Key = _options.LicenseKey,
            HardwareId = hardwareId,
            MachineName = Environment.MachineName,
            OsVersion = Environment.OSVersion.VersionString,
            ClientVersion = _options.ClientVersion
        };

        var response = await _httpClient.PostAsJsonAsync("/api/license/activate", request, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<LicenseActivationResponse>(cancellationToken);
            return new LicenseActivationResponse
            {
                Success = false,
                ErrorMessage = error?.ErrorMessage ?? $"Server returned {response.StatusCode}",
                ErrorCode = error?.ErrorCode ?? LicenseValidationError.ServerUnreachable
            };
        }

        var responseData = await response.Content.ReadFromJsonAsync<LicenseActivationResponse>(cancellationToken);
        
        if (responseData.Success && responseData.License != null)
        {
            await CacheLicenseAsync(responseData.License, hardwareId);
        }

        return responseData;
    }

    /// <summary>
    /// Deactivates the license on the server (releases seat).
    /// </summary>
    public async Task<bool> DeactivateAsync(string licenseKey, string hardwareId, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new { Key = licenseKey, HardwareId = hardwareId };
            var response = await _httpClient.PostAsJsonAsync("/api/license/deactivate", request, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                // Clear local cache
                await _cacheLock.WaitAsync(cancellationToken);
                try
                {
                    if (_cachedLicense?.LicenseKey == _options.LicenseKey)
                    {
                        _cachedLicense = null;
                        await SaveCacheAsync();
                    }
                }
                finally
                {
                    _cacheLock.Release();
                }
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the currently cached license (offline).
    /// </summary>
    public async Task<License?> GetCachedLicenseAsync(CancellationToken cancellationToken = default)
    {
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedLicense == null) return null;
            return await DecryptLicenseAsync(_cachedLicense);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Checks if we have a valid cached license.
    /// </summary>
    public async Task<bool> HasValidCachedLicenseAsync(CancellationToken cancellationToken = default)
    {
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            return _cachedLicense != null && !_cachedLicense.IsGraceExpired;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Forces a license sync with the server.
    /// </summary>
    public async Task<LicenseValidationResult> ForceSyncAsync(CancellationToken cancellationToken = default)
    {
        return await ValidateAsync(_options.LicenseKey, _options.HardwareId, true, cancellationToken);
    }

    /// <summary>
    /// Gets license features for the current license.
    /// </summary>
    public async Task<LicenseFeature> GetFeaturesAsync(CancellationToken cancellationToken = default)
    {
        var license = await GetCachedLicenseAsync(cancellationToken);
        return license?.Features ?? LicenseFeature.Core;
    }

    /// <summary>
    /// Checks if a specific feature is licensed.
    /// </summary>
    public async Task<bool> HasFeatureAsync(LicenseFeature feature, CancellationToken cancellationToken = default)
    {
        var features = await GetFeaturesAsync(cancellationToken);
        return (features & feature) == feature;
    }

    // Private methods

    private async Task CacheLicenseAsync(License license, string hardwareId)
    {
        await _cacheLock.WaitAsync();
        try
        {
            var encrypted = EncryptLicense(license);
            var graceDays = license.OfflineGraceDays > 0 ? license.OfflineGraceDays : 7;
            
            _cachedLicense = new OfflineLicenseCache
            {
                LicenseKey = license.Key,
                EncryptedLicenseData = encrypted,
                CachedAt = DateTimeOffset.UtcNow,
                GraceExpiresAt = DateTimeOffset.UtcNow.AddDays(graceDays),
                ValidationCount = 0,
                LastValidationAt = DateTimeOffset.UtcNow
            };

            await SaveCacheAsync();
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private async Task LoadCacheAsync()
    {
        await _cacheLock.WaitAsync();
        try
        {
            var cachePath = GetCachePath();
            if (!File.Exists(cachePath)) return;

            var encryptedData = await File.ReadAllBytesAsync(cachePath);
            if (encryptedData.Length == 0) return;

            // Decrypt and verify
            var json = DecryptCache(encryptedData);
            if (string.IsNullOrEmpty(json)) return;

            _cachedLicense = JsonSerializer.Deserialize<OfflineLicenseCache>(json);
        }
        catch (Exception ex)
        {
            // Corrupted cache - delete it
            try { File.Delete(GetCachePath()); } catch { }
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private async Task SaveCacheAsync()
    {
        if (_cachedLicense == null)
        {
            try { File.Delete(GetCachePath()); } catch { }
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(_cachedLicense, new JsonSerializerOptions { WriteIndented = true });
            var encrypted = EncryptCache(json);
            
            var cachePath = GetCachePath();
            var directory = Path.GetDirectoryName(cachePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory!);
            }

            await File.WriteAllBytesAsync(cachePath, encrypted);
        }
        catch (Exception ex)
        {
            // Log error but don't throw
            System.Diagnostics.Debug.WriteLine($"Failed to save license cache: {ex.Message}");
        }
    }

    private byte[] EncryptLicense(License license)
    {
        var json = JsonSerializer.Serialize(license);
        return EncryptData(Encoding.UTF8.GetBytes(json), _options.EncryptionKey);
    }

    private async Task<License?> DecryptLicenseAsync(OfflineLicenseCache cache)
    {
        try
        {
            var decrypted = DecryptData(cache.EncryptedLicenseData, _options.EncryptionKey);
            var json = Encoding.UTF8.GetString(decrypted);
            return JsonSerializer.Deserialize<License>(json);
        }
        catch
        {
            return null;
        }
    }

    private byte[] EncryptCache(string json)
    {
        return EncryptData(Encoding.UTF8.GetBytes(json), _options.CacheEncryptionKey);
    }

    private string DecryptCache(byte[] encrypted)
    {
        var decrypted = DecryptData(encrypted, _options.CacheEncryptionKey);
        return Encoding.UTF8.GetString(decrypted);
    }

    private byte[] EncryptData(byte[] data, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length);
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        {
            cs.Write(data, 0, data.Length);
        }
        return ms.ToArray();
    }

    private byte[] DecryptData(byte[] encryptedData, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        var iv = new byte[16];
        Array.Copy(encryptedData, 0, iv, 0, 16);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream(encryptedData, 16, encryptedData.Length - 16);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var result = new MemoryStream();
        cs.CopyTo(result);
        return result.ToArray();
    }

    private string GetCachePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "KKBOT", "Licenses");
        return Path.Combine(dir, $"license_{_options.LicenseKey.Replace("-", "")}.cache");
    }

    private byte[] EncryptData(byte[] data, string key)
    {
        var keyBytes = DeriveKey(key);
        return EncryptData(data, keyBytes);
    }

    private byte[] DecryptData(byte[] encryptedData, string key)
    {
        var keyBytes = DeriveKey(key);
        return DecryptData(encryptedData, keyBytes);
    }

    private byte[] DeriveKey(string password)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, _options.KeyDerivationSalt, 100000, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(32); // 256-bit key
    }

    private async void SyncLicenseAsync(object? state)
    {
        if (_shutdownCts.Token.IsCancellationRequested) return;

        try
        {
            if (!string.IsNullOrEmpty(_options.LicenseKey))
            {
                await ValidateAsync(_options.LicenseKey, _options.HardwareId, true, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            // Log but don't throw in background sync
            System.Diagnostics.Debug.WriteLine($"License sync failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _syncTimer?.Dispose();
        _shutdownCts.Cancel();
        _shutdownCts.Dispose();
        _httpClient?.Dispose();
        _cacheLock.Dispose();
    }
}

/// <summary>
/// Configuration options for LicenseValidator.
/// </summary>
public sealed class LicenseValidatorOptions
{
    public string LicenseServerUrl { get; set; } = "https://license.kkbot.io";
    public string LicenseKey { get; set; } = string.Empty;
    public string HardwareId { get; set; } = string.Empty;
    public string ClientVersion { get; set; } = "1.0.0";
    public string EncryptionKey { get; set; } = "KKBOT-License-Encryption-Key-2024";
    public string CacheEncryptionKey { get; set; } = "KKBOT-Cache-Encryption-Key-2024";
    public byte[] KeyDerivationSalt { get; set; } = Encoding.UTF8.GetBytes("KKBOT-License-Salt-2024");
    public bool EnableAutoSync { get; set; } = true;
    public int SyncIntervalMinutes { get; set; } = 30;
    public bool AllowOfflineFallback { get; set; } = true;
    public string ClientVersion { get; set; } = "1.0.0";
}

/// <summary>
/// License key format validator.
/// </summary>
public static class LicenseKeyFormatValidator
{
    private static readonly System.Text.RegularExpressions.Regex LicenseKeyRegex = 
        new(@"^KKBOT-[A-Z2-7]{4}-[A-Z2-7]{4}-[A-Z2-7]{4}-[A-Z2-7]{4}$", 
            System.Text.RegularExpressions.RegexOptions.Compiled);

    public static bool IsValidFormat(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        if (key.Length != 29) return false;
        return LicenseKeyRegex.IsMatch(key);
    }

    public static bool TryParse(string key, out string normalizedKey)
    {
        normalizedKey = string.Empty;
        if (!IsValidFormat(key)) return false;
        
        normalizedKey = key.ToUpperInvariant();
        return true;
    }

    public static string GenerateKey()
    {
        // Base32 encoding (Crockford's Base32) without padding
        var random = new byte[20]; // 160 bits = 26 chars in Base32
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(random);
        
        var base32 = Base32Encode(random);
        return $"KKBOT-{base32.Substring(0, 4)}-{base32.Substring(4, 4)}-{base32.Substring(8, 4)}-{base32.Substring(12, 4)}";
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"; // Crockford's Base32
        var result = new StringBuilder();
        var buffer = 0UL;
        var bits = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                result.Append(alphabet[(int)((buffer >> bits) & 0x1F)]);
            }
        }

        if (bits > 0)
        {
            result.Append(alphabet[(int)((buffer << (5 - bits)) & 0x1F)]);
        }

        return result.ToString().PadRight(26, '0');
    }
}

