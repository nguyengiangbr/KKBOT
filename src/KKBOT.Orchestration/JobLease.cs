// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KKBOT.Orchestration;

/// <summary>
/// Job lease management for multi-worker coordination.
/// Prevents duplicate processing of jobs across multiple workers.
/// </summary>
public sealed class JobLeaseManager : IDisposable
{
    private readonly ILogger<JobLeaseManager> _logger;
    private readonly JobLeaseOptions _options;
    private readonly ConcurrentDictionary<string, JobLease> _leases = new();
    private readonly Timer _cleanupTimer;
    private readonly CancellationTokenSource _shutdownCts = new();
    private bool _disposed;

    public JobLeaseManager(
        ILogger<JobLeaseManager> logger,
        IOptions<JobLeaseOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        // Start cleanup timer for expired leases
        _cleanupTimer = new Timer(CleanupExpiredLeases, null,
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        _logger.LogInformation("Job Lease Manager initialized");
    }

    /// <summary>
    /// Attempts to acquire a lease for a job.
    /// Returns true if lease acquired, false if already leased by another worker.
    /// </summary>
    public async ValueTask<LeaseAcquisitionResult> TryAcquireLeaseAsync(
        string jobId,
        string workerId,
        TimeSpan? leaseDuration = null,
        CancellationToken cancellationToken = default)
    {
        var duration = leaseDuration ?? _options.DefaultLeaseDuration;
        var expiresAt = DateTimeOffset.UtcNow + duration;

        var newLease = new JobLease
        {
            JobId = jobId,
            WorkerId = workerId,
            AcquiredAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt,
            RenewalCount = 0
        };

        // Try to add new lease (only succeeds if no existing lease)
        var added = _leases.TryAdd(jobId, newLease);
        
        if (added)
        {
            _logger.LogDebug("Worker {WorkerId} acquired lease for job {JobId}", workerId, jobId);
            return LeaseAcquisitionResult.Succeeded(jobId, workerId, expiresAt);
        }

        // Check if existing lease is expired
        if (_leases.TryGetValue(jobId, out var existingLease))
        {
            if (existingLease.ExpiresAt < DateTimeOffset.UtcNow)
            {
                // Try to replace expired lease
                var replaced = _leases.TryUpdate(jobId, newLease, existingLease);
                if (replaced)
                {
                    _logger.LogInformation("Worker {WorkerId} stole expired lease for job {JobId} from {OldWorker}", 
                        workerId, jobId, existingLease.WorkerId);
                    return LeaseAcquisitionResult.Succeeded(jobId, workerId, expiresAt);
                }
            }
            
            // Check if same worker already owns lease (renewal)
            if (existingLease.WorkerId == workerId)
            {
                return await RenewLeaseAsync(jobId, workerId, duration, cancellationToken);
            }
        }

        _logger.LogDebug("Worker {WorkerId} failed to acquire lease for job {JobId} - owned by {Owner}", 
            workerId, jobId, existingLease?.WorkerId ?? "unknown");
        return LeaseAcquisitionResult.AlreadyLeased(jobId, existingLease?.WorkerId ?? "unknown", existingLease?.ExpiresAt);
    }

    /// <summary>
    /// Renews an existing lease for the same worker.
    /// </summary>
    public async ValueTask<LeaseAcquisitionResult> RenewLeaseAsync(
        string jobId,
        string workerId,
        TimeSpan? leaseDuration = null,
        CancellationToken cancellationToken = default)
    {
        var duration = leaseDuration ?? _options.DefaultLeaseDuration;
        var expiresAt = DateTimeOffset.UtcNow + duration;

        if (!_leases.TryGetValue(jobId, out var existingLease))
        {
            return LeaseAcquisitionResult.NotFound(jobId);
        }

        if (existingLease.WorkerId != workerId)
        {
            return LeaseAcquisitionResult.AlreadyLeased(jobId, existingLease.WorkerId, existingLease.ExpiresAt);
        }

        var renewedLease = new JobLease
        {
            JobId = jobId,
            WorkerId = workerId,
            AcquiredAt = existingLease.AcquiredAt,
            ExpiresAt = expiresAt,
            RenewalCount = existingLease.RenewalCount + 1
        };

        var renewed = _leases.TryUpdate(jobId, renewedLease, existingLease);
        
        if (renewed)
        {
            _logger.LogDebug("Worker {WorkerId} renewed lease for job {JobId} (renewal #{Count})", 
                workerId, jobId, renewedLease.RenewalCount);
            return LeaseAcquisitionResult.Succeeded(jobId, workerId, expiresAt);
        }

        return LeaseAcquisitionResult.Failed(jobId, "Failed to renew lease");
    }

    /// <summary>
    /// Releases a lease held by the worker.
    /// </summary>
    public bool ReleaseLease(string jobId, string workerId)
    {
        if (_leases.TryGetValue(jobId, out var lease) && lease.WorkerId == workerId)
        {
            var removed = _leases.TryRemove(jobId, out _);
            if (removed)
            {
                _logger.LogDebug("Worker {WorkerId} released lease for job {JobId}", workerId, jobId);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Gets lease information for a job.
    /// </summary>
    public JobLeaseInfo? GetLeaseInfo(string jobId)
    {
        if (_leases.TryGetValue(jobId, out var lease))
        {
            return new JobLeaseInfo
            {
                JobId = lease.JobId,
                WorkerId = lease.WorkerId,
                AcquiredAt = lease.AcquiredAt,
                ExpiresAt = lease.ExpiresAt,
                IsExpired = lease.ExpiresAt < DateTimeOffset.UtcNow,
                RenewalCount = lease.RenewalCount,
                TimeRemaining = lease.ExpiresAt - DateTimeOffset.UtcNow
            };
        }
        return null;
    }

    /// <summary>
    /// Gets all active leases for a worker.
    /// </summary>
    public IReadOnlyList<JobLeaseInfo> GetWorkerLeases(string workerId)
    {
        return _leases.Values
            .Where(l => l.WorkerId == workerId)
            .Select(l => new JobLeaseInfo
            {
                JobId = l.JobId,
                WorkerId = l.WorkerId,
                AcquiredAt = l.AcquiredAt,
                ExpiresAt = l.ExpiresAt,
                IsExpired = l.ExpiresAt < DateTimeOffset.UtcNow,
                RenewalCount = l.RenewalCount,
                TimeRemaining = l.ExpiresAt - DateTimeOffset.UtcNow
            })
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Forces release of a lease (admin operation).
    /// </summary>
    public bool ForceRelease(string jobId)
    {
        var removed = _leases.TryRemove(jobId, out _);
        if (removed)
        {
            _logger.LogWarning("Force released lease for job {JobId}", jobId);
        }
        return removed;
    }

    private void CleanupExpiredLeases(object? state)
    {
        if (_disposed) return;

        var now = DateTimeOffset.UtcNow;
        var expiredKeys = _leases
            .Where(kvp => kvp.Value.ExpiresAt < now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _leases.TryRemove(key, out _);
        }

        if (expiredKeys.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired job leases", expiredKeys.Count);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _shutdownCts.Cancel();
        _cleanupTimer?.Dispose();
    }
}

/// <summary>
/// Job lease data.
/// </summary>
internal sealed class JobLease
{
    public string JobId { get; init; } = string.Empty;
    public string WorkerId { get; init; } = string.Empty;
    public DateTimeOffset AcquiredAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; init; } = DateTimeOffset.UtcNow;
    public int RenewalCount { get; init; } = 0;
}

/// <summary>
/// Result of lease acquisition attempt.
/// </summary>
public sealed class LeaseAcquisitionResult
{
    public string JobId { get; init; } = string.Empty;
    public string WorkerId { get; init; } = string.Empty;
    public bool Success { get; init; }
    public LeaseAcquisitionStatus Status { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public string? CurrentOwner { get; init; }
    public DateTimeOffset? CurrentOwnerExpiresAt { get; init; }
    public string Message { get; init; } = string.Empty;

    public static LeaseAcquisitionResult Succeeded(string jobId, string workerId, DateTimeOffset expiresAt)
    {
        return new LeaseAcquisitionResult
        {
            JobId = jobId,
            WorkerId = workerId,
            Success = true,
            Status = LeaseAcquisitionStatus.Acquired,
            ExpiresAt = expiresAt,
            Message = "Lease acquired successfully"
        };
    }

    public static LeaseAcquisitionResult AlreadyLeased(string jobId, string currentOwner, DateTimeOffset? expiresAt)
    {
        return new LeaseAcquisitionResult
        {
            JobId = jobId,
            Success = false,
            Status = LeaseAcquisitionStatus.AlreadyLeased,
            CurrentOwner = currentOwner,
            CurrentOwnerExpiresAt = expiresAt,
            Message = $"Job already leased by {currentOwner}"
        };
    }

    public static LeaseAcquisitionResult NotFound(string jobId)
    {
        return new LeaseAcquisitionResult
        {
            JobId = jobId,
            Success = false,
            Status = LeaseAcquisitionStatus.NotFound,
            Message = "Lease not found"
        };
    }

    public static LeaseAcquisitionResult Failed(string jobId, string message)
    {
        return new LeaseAcquisitionResult
        {
            JobId = jobId,
            Success = false,
            Status = LeaseAcquisitionStatus.Failed,
            Message = message
        };
    }
}

/// <summary>
/// Lease acquisition status.
/// </summary>
public enum LeaseAcquisitionStatus
{
    Acquired,
    AlreadyLeased,
    NotFound,
    Failed
}

/// <summary>
/// Lease information for display.
/// </summary>
public sealed class JobLeaseInfo
{
    public string JobId { get; init; } = string.Empty;
    public string WorkerId { get; init; } = string.Empty;
    public DateTimeOffset AcquiredAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public bool IsExpired { get; init; }
    public int RenewalCount { get; init; }
    public TimeSpan TimeRemaining { get; init; }
}

/// <summary>
/// Options for job lease manager.
/// </summary>
public sealed class JobLeaseOptions
{
    public TimeSpan DefaultLeaseDuration { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan MaxLeaseDuration { get; set; } = TimeSpan.FromHours(1);
    public int MaxRenewals { get; set; } = 10;
}

/// <summary>
/// Extension methods for registering job lease manager.
/// </summary>
public static class JobLeaseExtensions
{
    public static IServiceCollection AddJobLeaseManager(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JobLeaseOptions>(configuration.GetSection("JobLease"));
        services.AddSingleton<JobLeaseManager>();
        return services;
    }
}
