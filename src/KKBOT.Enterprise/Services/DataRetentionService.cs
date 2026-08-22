// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Enterprise.Abstractions;
using KKBOT.Enterprise.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KKBOT.Enterprise.Services;

public sealed class DataRetentionService : IDataRetentionService
{
    private readonly EnterpriseDbContext _dbContext;
    private readonly ILogger<DataRetentionService> _logger;

    public DataRetentionService(EnterpriseDbContext dbContext, ILogger<DataRetentionService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<DataRetentionPolicy> CreatePolicyAsync(CreateRetentionPolicyRequest request, string userId, CancellationToken ct = default)
    {
        var policy = new DataRetentionPolicy
        {
            Name = request.Name,
            Description = request.Description,
            DataCategory = request.DataCategory,
            DataType = request.DataType,
            RetentionDays = request.RetentionDays,
            ArchiveAfterDays = request.ArchiveAfterDays,
            ActionAfterRetention = request.ActionAfterRetention,
            ArchiveLocation = request.ArchiveLocation,
            ApplicableRegulations = request.ApplicableRegulations,
            ExemptedDataTypes = request.ExemptedDataTypes,
            LegalHoldTag = request.LegalHoldTag,
            RequireLegalHoldApproval = request.RequireLegalHoldApproval,
            LegalHoldContact = request.LegalHoldContact,
            CreatedBy = userId
        };

        _dbContext.RetentionPolicies.Add(policy);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created retention policy {PolicyId} by user {UserId}", policy.Id, userId);
        return policy;
    }

    public async Task<DataRetentionPolicy?> GetPolicyAsync(Guid id, string userId, CancellationToken ct = default)
    {
        return await _dbContext.RetentionPolicies
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<List<DataRetentionPolicy>> GetPoliciesAsync(string userId, RetentionPolicyFilter filter, CancellationToken ct = default)
    {
        var query = _dbContext.RetentionPolicies.AsQueryable();

        if (filter.IsEnabled.HasValue)
            query = query.Where(p => p.IsEnabled == filter.IsEnabled.Value);

        if (filter.IsDefault.HasValue)
            query = query.Where(p => p.IsDefault == filter.IsDefault.Value);

        if (!string.IsNullOrWhiteSpace(filter.DataCategory))
            query = query.Where(p => p.DataCategory == filter.DataCategory);

        if (!string.IsNullOrWhiteSpace(filter.DataType))
            query = query.Where(p => p.DataType == filter.DataType);

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);
    }

    public async Task<DataRetentionPolicy> UpdatePolicyAsync(Guid id, UpdateRetentionPolicyRequest request, string userId, CancellationToken ct = default)
    {
        var policy = await _dbContext.RetentionPolicies.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (policy == null)
            throw new InvalidOperationException("Policy not found");

        if (request.Name != null) policy.Name = request.Name;
        if (request.Description != null) policy.Description = request.Description;
        if (request.RetentionDays.HasValue) policy.RetentionDays = request.RetentionDays.Value;
        if (request.ArchiveAfterDays.HasValue) policy.ArchiveAfterDays = request.ArchiveAfterDays.Value;
        if (request.ActionAfterRetention.HasValue) policy.ActionAfterRetention = request.ActionAfterRetention.Value;
        if (request.ArchiveLocation != null) policy.ArchiveLocation = request.ArchiveLocation;
        if (request.ApplicableRegulations != null) policy.ApplicableRegulations = request.ApplicableRegulations;
        if (request.ExemptedDataTypes != null) policy.ExemptedDataTypes = request.ExemptedDataTypes;
        if (request.LegalHoldTag != null) policy.LegalHoldTag = request.LegalHoldTag;
        if (request.RequireLegalHoldApproval.HasValue) policy.RequireLegalHoldApproval = request.RequireLegalHoldApproval.Value;
        if (request.LegalHoldContact != null) policy.LegalHoldContact = request.LegalHoldContact;
        if (request.IsEnabled.HasValue) policy.IsEnabled = request.IsEnabled.Value;

        policy.UpdatedAt = DateTime.UtcNow;
        policy.UpdatedBy = userId;

        await _dbContext.SaveChangesAsync(ct);
        return policy;
    }

    public async Task<bool> DeletePolicyAsync(Guid id, string userId, CancellationToken ct = default)
    {
        var policy = await _dbContext.RetentionPolicies.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (policy == null) return false;

        _dbContext.RetentionPolicies.Remove(policy);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<RetentionExecutionResult> ExecutePolicyAsync(Guid policyId, CancellationToken ct = default)
    {
        var policy = await _dbContext.RetentionPolicies.FirstOrDefaultAsync(p => p.Id == policyId, ct);
        if (policy == null)
            throw new InvalidOperationException("Policy not found");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new RetentionExecutionResult
        {
            PolicyId = policyId,
            ExecutedAt = DateTime.UtcNow
        };

        try
        {
            // In a real implementation, this would query the actual data tables
            // based on policy.DataCategory and policy.DataType
            // For now, simulate execution
            await Task.Delay(100, ct);

            // Simulate results
            result.RecordsProcessed = 1000;
            result.RecordsDeleted = policy.ActionAfterRetention == RetentionAction.Delete ? 100 : 0;
            result.RecordsArchived = policy.ActionAfterRetention == RetentionAction.Archive ? 500 : 0;
            result.RecordsAnonymized = policy.ActionAfterRetention == RetentionAction.Anonymize ? 200 : 0;
            result.RecordsMovedToColdStorage = policy.ActionAfterRetention == RetentionAction.MoveToColdStorage ? 300 : 0;
            result.RecordsOnLegalHold = 50;
            result.FreedSpaceBytes = 1024 * 1024 * 50; // 50 MB
            result.Status = RetentionExecutionStatus.Completed;

            _logger.LogInformation("Executed retention policy {PolicyId}: processed {Count} records", policy.Id, result.RecordsProcessed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute retention policy {PolicyId}", policy.Id);
            result.Status = RetentionExecutionStatus.Failed;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            result.Duration = System.Diagnostics.Stopwatch.GetTimestamp();
        }

        return result;
    }

    public async Task<RetentionExecutionResult> ExecuteAllPoliciesAsync(string tenantId, CancellationToken ct = default)
    {
        var policies = await _dbContext.RetentionPolicies
            .Where(p => p.IsEnabled && p.TenantId == tenantId)
            .ToListAsync(ct);

        var totalResult = new RetentionExecutionResult
        {
            PolicyId = Guid.Empty,
            ExecutedAt = DateTime.UtcNow
        };

        foreach (var policy in policies)
        {
            var result = await ExecutePolicyAsync(policy.Id, CancellationToken.None);
            totalResult.RecordsProcessed += result.RecordsProcessed;
            totalResult.RecordsDeleted += result.RecordsDeleted;
            totalResult.RecordsArchived += result.RecordsArchived;
            totalResult.RecordsAnonymized += result.RecordsAnonymized;
            totalResult.RecordsMovedToColdStorage += result.RecordsMovedToColdStorage;
            totalResult.RecordsOnLegalHold += result.RecordsOnLegalHold;
            totalResult.FreedSpaceBytes += result.FreedSpaceBytes;
        }

        totalResult.Status = RetentionExecutionStatus.Completed;
        return totalResult;
    }

    public async Task<List<RetentionExecutionResult>> GetExecutionHistoryAsync(Guid policyId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        // In a real implementation, this would query an execution history table
        return new List<RetentionExecutionResult>();
    }
}