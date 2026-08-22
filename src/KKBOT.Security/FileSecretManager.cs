// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Security.Abstractions;
using Microsoft.Extensions.Logging;

namespace KKBOT.Security;

/// <summary>
/// Simple file‑based secret store (JSON file). Not for production; replace with Vault/Azure Key Vault.
/// </summary>
public sealed class FileSecretManager : ISecretManager
{
    private readonly string _filePath;
    private readonly ILogger<FileSecretManager> _logger;
    private readonly ConcurrentDictionary<string, string> _cache = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FileSecretManager(string filePath, ILogger<FileSecretManager> logger)
    {
        _filePath = filePath;
        _logger = logger;
        Load();
    }

    private void Load()
    {
        if (!File.Exists(_filePath))
        {
            _cache.Clear();
            return;
        }
        try
        {
            var json = File.ReadAllText(_filePath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            _cache.Clear();
            foreach (var kv in dict) _cache[kv.Key] = kv.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load secrets from {File}", _filePath);
            _cache.Clear();
        }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_cache);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save secrets to {File}", _filePath);
        }
    }

    public Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        _cache.TryGetValue(key, out var value);
        return Task.FromResult(value);
    }

    public Task SetSecretAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        _lock.Wait(cancellationToken);
        try
        {
            _cache[key] = value;
            Save();
        }
        finally { _lock.Release(); }
        return Task.CompletedTask;
    }

    public Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        _lock.Wait(cancellationToken);
        try
        {
            _cache.TryRemove(key, out _);
            Save();
        }
        finally { _lock.Release(); }
        return Task.CompletedTask;
    }
}