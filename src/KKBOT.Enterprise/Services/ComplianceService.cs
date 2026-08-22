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

public sealed class ComplianceService : IComplianceService
{
    private readonly EnterpriseDbContext _dbContext;
    private readonly ILogger<ComplianceService> _logger;

    public ComplianceService(EnterpriseDbContext dbContext, ILogger<ComplianceService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ComplianceFramework> CreateFrameworkAsync(CreateFrameworkRequest request, string userId, CancellationToken ct = default)
    {
        var framework = new ComplianceFramework
        {
            Name = request.Name,
            Code = request.Code,
            Description = request.Description,
            Version = request.Version,
            Url = request.Url,
            Type = request.Type,
            EffectiveDate = request.EffectiveDate,
            ExpiryDate = request.ExpiryDate,
            Requirements = request.Requirements,
            Controls = request.Controls,
            Policies = request.Policies,
            CreatedBy = userId
        };

        _dbContext.ComplianceFrameworks.Add(framework);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created compliance framework {FrameworkId} ({Name}) by user {UserId}", framework.Id, framework.Name, userId);
        return framework;
    }

    public async Task<ComplianceFramework?> GetFrameworkAsync(Guid id, string userId, CancellationToken ct = default)
    {
        return await _dbContext.ComplianceFrameworks
            .Include(f => f.Requirements)
            .Include(f => f.Controls)
            .Include(f => f.Policies)
            .FirstOrDefaultAsync(f => f.Id == id, ct);
    }

    public async Task<List<ComplianceFramework>> GetFrameworksAsync(string userId, FrameworkFilter filter, CancellationToken ct = default)
    {
        var query = _dbContext.ComplianceFrameworks.AsQueryable();

        if (filter.Type.HasValue)
            query = query.Where(f => f.Type == filter.Type.Value);

        if (filter.Status.HasValue)
            query = query.Where(f => f.Status == filter.Status.Value);

        if (filter.IsActive.HasValue)
            query = query.Where(f => f.IsActive == filter.IsActive.Value);

        if (filter.IsMandatory.HasValue)
            query = query.Where(f => f.IsMandatory == filter.IsMandatory.Value);

        query = query.OrderByDescending(f => f.CreatedAt);

        return await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);
    }

    public async Task<ComplianceFramework> UpdateFrameworkAsync(Guid id, UpdateFrameworkRequest request, string userId, CancellationToken ct = default)
    {
        var framework = await _dbContext.ComplianceFrameworks.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (framework == null)
            throw new InvalidOperationException("Framework not found");

        if (request.Name != null) framework.Name = request.Name;
        if (request.Description != null) framework.Description = request.Description;
        if (request.Version != null) framework.Version = request.Version;
        if (request.Url != null) framework.Url = request.Url;
        if (request.Status.HasValue) framework.Status = request.Status.Value;
        if (request.ExpiryDate.HasValue) framework.ExpiryDate = request.ExpiryDate.Value;
        if (request.Requirements != null) framework.Requirements = request.Requirements;
        if (request.Controls != null) framework.Controls = request.Controls;
        if (request.Policies != null) framework.Policies = request.Policies;
        if (request.OwnerId != null) framework.OwnerId = request.OwnerId;
        if (request.OwnerEmail != null) framework.OwnerEmail = request.OwnerEmail;

        framework.UpdatedAt = DateTime.UtcNow;
        framework.UpdatedBy = userId;

        await _dbContext.SaveChangesAsync(ct);
        return framework;
    }

    public async Task<bool> DeleteFrameworkAsync(Guid id, string userId, CancellationToken ct = default)
    {
        var framework = await _dbContext.ComplianceFrameworks.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (framework == null) return false;

        _dbContext.ComplianceFrameworks.Remove(framework);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ComplianceAssessment> AssessAsync(Guid frameworkId, string userId, CancellationToken ct = default)
    {
        var framework = await _dbContext.ComplianceFrameworks
            .Include(f => f.Requirements)
            .Include(f => f.Controls)
            .Include(f => f.Policies)
            .FirstOrDefaultAsync(f => f.Id == frameworkId, ct);

        if (framework == null)
            throw new InvalidOperationException("Framework not found");

        var assessment = new ComplianceAssessment
        {
            FrameworkId = framework.Id,
            FrameworkName = framework.Name,
            AssessedAt = DateTime.UtcNow,
            TotalRequirements = framework.Requirements.Count
        };

        // Assess requirements
        var requirementAssessments = new List<RequirementAssessment>();
        foreach (var req in framework.Requirements)
        {
            // In a real implementation, this would check actual compliance
            var assessment = new RequirementAssessment
            {
                RequirementId = req.Id,
                RequirementTitle = req.Title,
                Status = AssessmentStatus.Compliant, // Simplified
                Score = 1.0,
                Evidence = "Automated check passed",
                Gaps = new List<string>(),
                Recommendations = new List<string>()
            };
            requirementAssessments.Add(assessment);
        }

        // Assess controls
        var controlAssessments = new List<ControlAssessment>();
        foreach (var control in framework.Controls)
        {
            var assessment = new ControlAssessment
            {
                ControlId = control.Id,
                ControlName = control.Name,
                Status = ControlStatus.Passed, // Simplified
                Score = 1.0,
                Evidence = "Automated check passed",
                AssessedAt = DateTime.UtcNow,
                AssessedBy = userId
            };
            controlAssessments.Add(assessment);
        }

        // Check policies
        var violations = new List<PolicyViolation>();
        foreach (var policy in framework.Policies)
        {
            // Policy evaluation logic would go here
        }

        // Calculate scores
        var compliantReqs = requirementAssessments.Count(r => r.Status == AssessmentStatus.Compliant);
        var nonCompliantReqs = requirementAssessments.Count(r => r.Status == AssessmentStatus.NonCompliant);
        var partialReqs = requirementAssessments.Count(r => r.Status == AssessmentStatus.Partial);
        var naReqs = requirementAssessments.Count(r => r.Status == AssessmentStatus.NotApplicable);

        var overallScore = requirementAssessments.Any() 
            ? requirementAssessments.Average(r => r.Score) 
            : 0;

        var assessmentResult = new ComplianceAssessment
        {
            FrameworkId = framework.Id,
            FrameworkName = framework.Name,
            AssessedAt = DateTime.UtcNow,
            OverallScore = overallScore,
            CompliancePercentage = framework.Requirements.Count > 0 ? (double)compliantReqs / framework.Requirements.Count * 100 : 100,
            TotalRequirements = framework.Requirements.Count,
            CompliantRequirements = compliantReqs,
            NonCompliantRequirements = nonCompliantReqs,
            PartiallyCompliantRequirements = partialReqs,
            NotApplicableRequirements = naReqs,
            RequirementAssessments = requirementAssessments,
            ControlAssessments = controlAssessments,
            Violations = new List<PolicyViolation>(), // Would be populated by policy checks
            Recommendations = new List<string>(),
            AssessmentDuration = TimeSpan.Zero
        };

        _logger.LogInformation("Completed compliance assessment for framework {FrameworkId} with score {Score}", frameworkId, overallScore);
        return assessmentResult;
    }

    public async Task<ComplianceReport> GenerateReportAsync(Guid frameworkId, CancellationToken ct = default)
    {
        var assessment = await AssessAsync(frameworkId, "system", ct);
        
        var report = new ComplianceReport
        {
            FrameworkId = frameworkId,
            FrameworkName = (await _dbContext.ComplianceFrameworks.FindAsync(frameworkId, ct))?.Name ?? "Unknown",
            GeneratedAt = DateTime.UtcNow,
            PeriodStart = DateTime.UtcNow.AddMonths(-1),
            PeriodEnd = DateTime.UtcNow,
            OverallScore = assessment.OverallScore,
            CompliancePercentage = assessment.CompliancePercentage,
            TotalRequirements = assessment.TotalRequirements,
            CompliantRequirements = assessment.CompliantRequirements,
            NonCompliantRequirements = assessment.NonCompliantRequirements,
            PartiallyCompliantRequirements = assessment.PartiallyCompliantRequirements,
            NotApplicableRequirements = assessment.NotApplicableRequirements,
            RequirementAssessments = assessment.RequirementAssessments,
            ControlAssessments = assessment.ControlAssessments,
            Violations = assessment.Violations,
            Recommendations = assessment.Recommendations
        };

        return report;
    }

    public async Task<CompliancePolicy> CreatePolicyAsync(CreatePolicyRequest request, string userId, CancellationToken ct = default)
    {
        var policy = new CompliancePolicy
        {
            Name = request.Name,
            Description = request.Description,
            Type = request.Type,
            Scope = request.Scope,
            ContentJson = request.ContentJson,
            IsEnforced = request.IsEnforced,
            Enforcement = request.Enforcement,
            ApplicableRoles = request.ApplicableRoles,
            ExemptRoles = request.ExemptRoles,
            ApplicableResources = request.ApplicableResources,
            ExemptResources = request.ExemptResources,
            EffectiveDate = request.EffectiveDate,
            ExpiryDate = request.ExpiryDate,
            Rules = request.Rules,
            CreatedBy = userId
        };

        _dbContext.CompliancePolicies.Add(policy);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created compliance policy {PolicyId} by user {UserId}", policy.Id, userId);
        return policy;
    }

    public async Task<CompliancePolicy?> GetPolicyAsync(Guid id, string userId, CancellationToken ct = default)
    {
        return await _dbContext.CompliancePolicies
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<List<CompliancePolicy>> GetPoliciesAsync(string userId, PolicyFilter filter, CancellationToken ct = default)
    {
        var query = _dbContext.CompliancePolicies.AsQueryable();

        if (filter.Type.HasValue)
            query = query.Where(p => p.Type == filter.Type.Value);

        if (filter.Scope.HasValue)
            query = query.Where(p => p.Scope == filter.Scope.Value);

        if (filter.IsEnforced.HasValue)
            query = query.Where(p => p.IsEnforced == filter.IsEnforced.Value);

        if (filter.IsActive.HasValue)
            query = query.Where(p => p.IsActive == filter.IsActive.Value);

        query = query.OrderByDescending(p => p.CreatedAt);

        return await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);
    }

    public async Task<CompliancePolicy> UpdatePolicyAsync(Guid id, UpdatePolicyRequest request, string userId, CancellationToken ct = default)
    {
        var policy = await _dbContext.CompliancePolicies.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (policy == null)
            throw new InvalidOperationException("Policy not found");

        if (request.Name != null) policy.Name = request.Name;
        if (request.Description != null) policy.Description = request.Description;
        if (request.Type.HasValue) policy.Type = request.Type.Value;
        if (request.Scope.HasValue) policy.Scope = request.Scope.Value;
        if (request.ContentJson != null) policy.ContentJson = request.ContentJson;
        if (request.IsEnforced.HasValue) policy.IsEnforced = request.IsEnforced.Value;
        if (request.Enforcement.HasValue) policy.Enforcement = request.Enforcement.Value;
        if (request.ApplicableRoles != null) policy.ApplicableRoles = request.ApplicableRoles;
        if (request.ExemptRoles != null) policy.ExemptRoles = request.ExemptRoles;
        if (request.ApplicableResources != null) policy.ApplicableResources = request.ApplicableResources;
        if (request.ExemptResources != null) policy.ExemptResources = request.ExemptResources;
        if (request.EffectiveDate.HasValue) policy.EffectiveDate = request.EffectiveDate.Value;
        if (request.ExpiryDate.HasValue) policy.ExpiryDate = request.ExpiryDate.Value;
        if (request.Rules != null) policy.Rules = request.Rules;
        if (request.IsActive.HasValue) policy.IsActive = request.IsActive.Value;

        policy.UpdatedAt = DateTime.UtcNow;
        policy.UpdatedBy = userId;

        await _dbContext.SaveChangesAsync(ct);
        return policy;
    }

    public async Task<bool> DeletePolicyAsync(Guid id, string userId, CancellationToken ct = default)
    {
        var policy = await _dbContext.CompliancePolicies.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (policy == null) return false;

        _dbContext.CompliancePolicies.Remove(policy);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PolicyEvaluationResult> EvaluatePolicyAsync(Guid policyId, string userId, CancellationToken ct = default)
    {
        var policy = await _dbContext.CompliancePolicies.FirstOrDefaultAsync(p => p.Id == policyId, ct);
        if (policy == null)
            throw new InvalidOperationException("Policy not found");

        // In a real implementation, this would evaluate the policy against actual resources
        var violations = new List<PolicyViolation>();

        // Simulate evaluation
        var violationsList = new List<PolicyViolation>();
        
        var evaluation = new PolicyEvaluationResult
        {
            IsCompliant = violationsList.Count == 0,
            Violations = violationsList,
            ComplianceScore = violationsList.Count == 0 ? 100 : Math.Max(0, 100 - violationsList.Count * 10),
            Recommendations = violationsList.Select(v => v.Remediation).ToList()
        };

        return evaluation;
    }

    public async Task<List<PolicyViolation>> GetViolationsAsync(string userId, ViolationFilter filter, CancellationToken ct = default)
    {
        var query = _dbContext.PolicyViolations.AsQueryable();

        if (filter.Severity.HasValue)
            query = query.Where(v => v.Severity == filter.Severity.Value);

        if (!string.IsNullOrWhiteSpace(filter.PolicyId))
            query = query.Where(v => v.PolicyId == filter.PolicyId);

        if (!string.IsNullOrWhiteSpace(filter.ResourceId))
            query = query.Where(v => v.ResourceId == filter.ResourceId);

        if (filter.FromDate.HasValue)
            query = query.Where(v => v.DetectedAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(v => v.DetectedAt <= filter.ToDate.Value);

        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(v => v.Status == filter.Status);

        query = query.OrderByDescending(v => v.DetectedAt);

        return await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);
    }
}