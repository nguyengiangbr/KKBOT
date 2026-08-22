// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.AI.Optimization.Abstractions;
using KKBOT.AI.Optimization.Models;
using Microsoft.Extensions.Logging;

namespace KKBOT.AI.Optimization.Services;

public sealed class ModelRegistry : IModelRegistry
{
    private readonly ConcurrentDictionary<string, ModelSpec> _models = new();
    private readonly ILogger<ModelRegistry> _logger;
    private readonly string _storagePath;

    public ModelRegistry(ILogger<ModelRegistry> logger, string? storagePath = null)
    {
        _logger = logger;
        _storagePath = storagePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "registry");
        Directory.CreateDirectory(_storagePath);
        LoadRegistry();
    }

    public async Task<ModelSpec> RegisterModelAsync(ModelSpec spec, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(spec.ModelId))
            spec.ModelId = Guid.NewGuid().ToString("N");

        if (string.IsNullOrWhiteSpace(spec.Version))
            spec.Version = "1.0.0";

        if (_models.ContainsKey(GetKey(spec.ModelId, spec.Version)))
            throw new InvalidOperationException($"Model {spec.ModelId} v{spec.Version} already exists");

        spec.CreatedAt = DateTime.UtcNow;
        spec.UpdatedAt = DateTime.UtcNow;

        _models[GetKey(spec.ModelId, spec.Version)] = spec;
        await SaveRegistryAsync(ct);

        _logger.LogInformation("Registered model {ModelId} v{Version}", spec.ModelId, spec.Version);
        return spec;
    }

    public async Task<ModelSpec?> GetModelAsync(string modelId, string? version = null, CancellationToken ct = default)
    {
        if (version != null)
        {
            _models.TryGetValue(GetKey(modelId, version), out var model);
            return model;
        }

        // Return latest version
        return _models.Values
            .Where(m => m.ModelId == modelId)
            .OrderByDescending(m => m.Version)
            .FirstOrDefault();
    }

    public async Task<List<ModelSpec>> ListModelsAsync(ModelListFilter filter, CancellationToken ct = default)
    {
        var query = _models.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filter.Framework))
            query = query.Where(m => m.Framework.Equals(filter.Framework, StringComparison.OrdinalIgnoreCase));

        if (filter.TargetPlatform.HasValue)
            query = query.Where(m => m.Metadata.TryGetValue("targetPlatform", out var tp) && tp.ToString() == filter.TargetPlatform.ToString());

        if (filter.Precision.HasValue)
            query = query.Where(m => m.Metadata.TryGetValue("precision", out var p) && p.ToString() == filter.Precision.ToString());

        if (filter.Stage.HasValue)
            query = query.Where(m => m.Metadata.TryGetValue("stage", out var s) && s.ToString() == filter.Stage.ToString());

        if (!string.IsNullOrWhiteSpace(filter.Tag))
        {
            query = query.Where(m => m.Metadata.TryGetValue("tags", out var tags) && 
                (tags as List<string>)?.Contains(filter.Tag, StringComparer.OrdinalIgnoreCase) == true);
        }

        var items = query.OrderByDescending(m => m.UpdatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        return items;
    }

    public async Task<bool> UpdateModelAsync(ModelSpec spec, CancellationToken ct = default)
    {
        var key = GetKey(spec.ModelId, spec.Version);
        if (!_models.ContainsKey(key))
            return false;

        spec.UpdatedAt = DateTime.UtcNow;
        _models[key] = spec;
        await SaveRegistryAsync(ct);
        return true;
    }

    public async Task<bool> DeleteModelAsync(string modelId, string version, CancellationToken ct = default)
    {
        var removed = _models.TryRemove(GetKey(modelId, version), out _);
        if (removed)
            await SaveRegistryAsync(ct);
        return removed;
    }

    public async Task<ModelSpec> PromoteModelAsync(string modelId, string version, ModelStage stage, CancellationToken ct = default)
    {
        var model = await GetModelAsync(modelId, version, CancellationToken.None);
        if (model == null)
            throw new InvalidOperationException($"Model {modelId} v{version} not found");

        model.Metadata["stage"] = stage.ToString();
        model.UpdatedAt = DateTime.UtcNow;

        await UpdateModelAsync(model, ct);
        _logger.LogInformation("Promoted model {ModelId} v{Version} to {Stage}", modelId, version, stage);
        return model;
    }

    private void LoadRegistry()
    {
        var indexFile = Path.Combine(_storagePath, "registry-index.json");
        if (File.Exists(indexFile))
        {
            try
            {
                var json = File.ReadAllText(indexFile);
                var models = JsonSerializer.Deserialize<List<ModelSpec>>(json);
                if (models != null)
                {
                    foreach (var model in models)
                    {
                        _models[GetKey(model.ModelId, model.Version)] = model;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load model registry");
            }
        }
    }

    private async Task SaveRegistryAsync(CancellationToken ct)
    {
        try
        {
            var indexFile = Path.Combine(_storagePath, "registry-index.json");
            var json = JsonSerializer.Serialize(_models.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(indexFile, json, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save model registry");
        }
    }

    private static string GetKey(string modelId, string version) => $"{modelId}@{version}";
}