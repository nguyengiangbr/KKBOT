// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Minio;
using Minio.DataModel.Args;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KKBOT.Marketplace.Server.Services;

public sealed class StorageOptions
{
    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin";
    public string BucketName { get; set; } = "kkbot-plugins";
    public bool UseSsl { get; set; } = false;
    public string Region { get; set; } = "us-east-1";
    public string CdnBaseUrl { get; set; } = string.Empty;
}

public interface IStorageService
{
    Task<string> UploadPluginAsync(string pluginId, string version, Stream fileStream, string contentType, CancellationToken ct = default);
    Task<string> UploadIconAsync(string pluginId, Stream fileStream, string contentType, CancellationToken ct = default);
    Task<Stream> DownloadPluginAsync(string pluginId, string version, CancellationToken ct = default);
    Task<bool> DeletePluginAsync(string pluginId, string version, CancellationToken ct = default);
    Task<bool> DeleteIconAsync(string pluginId, CancellationToken ct = default);
    Task<bool> ExistsAsync(string objectName, CancellationToken ct = default);
    Task<long> GetSizeAsync(string objectName, CancellationToken ct = default);
    string GetPublicUrl(string objectName);
    Task<string> GeneratePresignedUrlAsync(string objectName, TimeSpan expiry, CancellationToken ct = default);
}

public sealed class MinioStorageService : IStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly ILogger<MinioStorageService> _logger;
    private readonly StorageOptions _options;

    public MinioStorageService(
        ILogger<MinioStorageService> logger,
        IOptions<StorageOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        _minioClient = new MinioClient()
            .WithEndpoint(_options.Endpoint)
            .WithCredentials(_options.AccessKey, _options.SecretKey)
            .WithRegion(_options.Region)
            .WithSSL(_options.UseSsl)
            .Build();

        EnsureBucketExistsAsync().GetAwaiter().GetResult();
    }

    private async Task EnsureBucketExistsAsync()
    {
        try
        {
            var args = new BucketExistsArgs().WithBucket(_options.BucketName);
            var exists = await _minioClient.BucketExistsAsync(args);
            if (!exists)
            {
                var makeArgs = new MakeBucketArgs().WithBucket(_options.BucketName).WithRegion(_options.Region);
                await _minioClient.MakeBucketAsync(makeArgs);
                
                // Set bucket policy for public read access to icons
                var policy = $@"
                {{
                    ""Version"": ""2012-10-17"",
                    ""Statement"": [
                        {{
                            ""Effect"": ""Allow"",
                            ""Principal"": {{""AWS"": [""*""]}},
                            ""Action"": [""s3:GetObject""],
                            ""Resource"": [""arn:aws:s3:::{_options.BucketName}/icons/*""]
                        }}
                    ]
                }}";
                var policyArgs = new SetPolicyArgs().WithBucket(_options.BucketName).WithPolicy(policy);
                await _minioClient.SetPolicyAsync(policyArgs);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure bucket exists");
            throw;
        }
    }

    public async Task<string> UploadPluginAsync(string pluginId, string version, Stream fileStream, string contentType, CancellationToken ct = default)
    {
        var objectName = $"plugins/{pluginId}/{version}/package.dll";
        return await UploadAsync(objectName, fileStream, contentType, ct);
    }

    public async Task<string> UploadIconAsync(string pluginId, Stream fileStream, string contentType, CancellationToken ct = default)
    {
        var objectName = $"icons/{pluginId}/icon.png";
        return await UploadAsync(objectName, fileStream, contentType, ct);
    }

    private async Task<string> UploadAsync(string objectName, Stream fileStream, string contentType, CancellationToken ct)
    {
        try
        {
            var args = new PutObjectArgs()
                .WithBucket(_options.BucketName)
                .WithObject(objectName)
                .WithStreamData(fileStream)
                .WithObjectSize(fileStream.Length)
                .WithContentType(contentType);

            await _minioClient.PutObjectAsync(args, ct);
            
            _logger.LogInformation("Uploaded {ObjectName} ({Size} bytes)", objectName, fileStream.Length);
            return objectName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload {ObjectName}", objectName);
            throw;
        }
    }

    public async Task<Stream> DownloadPluginAsync(string pluginId, string version, CancellationToken ct = default)
    {
        var objectName = $"plugins/{pluginId}/{version}/package.dll";
        var memoryStream = new MemoryStream();
        
        var args = new GetObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(objectName)
            .WithCallbackStream((stream, ct) => stream.CopyToAsync(memoryStream, ct));

        await _minioClient.GetObjectAsync(args, ct);
        memoryStream.Position = 0;
        return memoryStream;
    }

    public async Task<bool> DeletePluginAsync(string pluginId, string version, CancellationToken ct = default)
    {
        var objects = new[]
        {
            $"plugins/{pluginId}/{version}/package.dll",
            $"plugins/{pluginId}/{version}/manifest.json",
            $"plugins/{pluginId}/{version}/changelog.md"
        };

        return await DeleteObjectsAsync(objects, ct);
    }

    public async Task<bool> DeleteIconAsync(string pluginId, CancellationToken ct = default)
    {
        return await DeleteObjectAsync($"icons/{pluginId}/icon.png", ct);
    }

    public async Task<bool> ExistsAsync(string objectName, CancellationToken ct = default)
    {
        try
        {
            var args = new StatObjectArgs().WithBucket(_options.BucketName).WithObject(objectName);
            await _minioClient.StatObjectAsync(args, ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<long> GetSizeAsync(string objectName, CancellationToken ct = default)
    {
        var args = new StatObjectArgs().WithBucket(_options.BucketName).WithObject(objectName);
        var stat = await _minioClient.StatObjectAsync(args, ct);
        return stat.Size;
    }

    public string GetPublicUrl(string objectName)
    {
        if (!string.IsNullOrEmpty(_options.CdnBaseUrl))
        {
            return $"{_options.CdnBaseUrl.TrimEnd('/')}/{objectName}";
        }
        
        var protocol = _options.UseSsl ? "https" : "http";
        return $"{protocol}://{_options.Endpoint}/{_options.BucketName}/{objectName}";
    }

    public async Task<string> GeneratePresignedUrlAsync(string objectName, TimeSpan expiry, CancellationToken ct = default)
    {
        var args = new PresignedGetObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(objectName)
            .WithExpiry((int)expiry.TotalSeconds);

        return await _minioClient.PresignedGetObjectAsync(args, ct);
    }

    private async Task<bool> DeleteObjectAsync(string objectName, CancellationToken ct)
    {
        try
        {
            var args = new RemoveObjectArgs().WithBucket(_options.BucketName).WithObject(objectName);
            await _minioClient.RemoveObjectAsync(args, ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> DeleteObjectsAsync(string[] objectNames, CancellationToken ct)
    {
        try
        {
            foreach (var obj in objectNames)
            {
                await DeleteObjectAsync(obj, ct);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}

// Alternative: Local file storage for development
public sealed class LocalStorageService : IStorageService
{
    private readonly string _basePath;
    private readonly string _baseUrl;
    private readonly ILogger<LocalStorageService> _logger;

    public LocalStorageService(ILogger<LocalStorageService> logger, IOptions<StorageOptions> options)
    {
        _logger = logger;
        _basePath = Path.Combine(Directory.GetCurrentDirectory(), "storage", "plugins");
        _baseUrl = "http://localhost:8080/api/v1/storage";
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> UploadPluginAsync(string pluginId, string version, Stream fileStream, string contentType, CancellationToken ct = default)
    {
        var dir = Path.Combine(_basePath, pluginId, version);
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, "package.dll");
        
        using var file = File.Create(filePath);
        await fileStream.CopyToAsync(file, ct);
        
        return filePath;
    }

    public async Task<string> UploadIconAsync(string pluginId, Stream fileStream, string contentType, CancellationToken ct = default)
    {
        var dir = Path.Combine(_basePath, "icons", pluginId);
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, "icon.png");
        
        using var file = File.Create(filePath);
        await fileStream.CopyToAsync(file, ct);
        
        return filePath;
    }

    public Task<Stream> DownloadPluginAsync(string pluginId, string version, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_basePath, pluginId, version, "package.dll");
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Plugin {pluginId} v{version} not found");
        
        return Task.FromResult<Stream>(File.OpenRead(filePath));
    }

    public Task<bool> DeletePluginAsync(string pluginId, string version, CancellationToken ct = default)
    {
        var dir = Path.Combine(_basePath, pluginId, version);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, true);
        }
        return Task.FromResult(true);
    }

    public Task<bool> DeleteIconAsync(string pluginId, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_basePath, "icons", pluginId, "icon.png");
        if (File.Exists(filePath))
            File.Delete(filePath);
        return Task.FromResult(true);
    }

    public Task<bool> ExistsAsync(string objectName, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_basePath, objectName);
        return Task.FromResult(File.Exists(filePath));
    }

    public Task<long> GetSizeAsync(string objectName, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_basePath, objectName);
        return Task.FromResult(File.Exists(filePath) ? new FileInfo(filePath).Length : 0);
    }

    public string GetPublicUrl(string objectName)
    {
        return $"{_baseUrl}/{objectName}";
    }

    public Task<string> GeneratePresignedUrlAsync(string objectName, TimeSpan expiry, CancellationToken ct = default)
    {
        return Task.FromResult(GetPublicUrl(objectName));
    }
}