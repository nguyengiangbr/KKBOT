// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Marketplace.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KKBOT.Marketplace.Server.Services;

public interface ILicenseComplianceService
{
    Task<LicenseComplianceReport> CheckComplianceAsync(string pluginId, string version, CancellationToken ct = default);
    Task<LicenseCompatibilityMatrix> GetCompatibilityMatrixAsync(string licenseId, CancellationToken ct = default);
    Task<LicenseObligations> GetLicenseObligationsAsync(string licenseId, CancellationToken ct = default);
    Task<List<LicenseConflict>> DetectConflictsAsync(string pluginId, string version, CancellationToken ct = default);
    Task<bool> IsLicenseCompatibleAsync(string license1, string license2, CancellationToken ct = default);
    Task<List<LicenseAttribution>> GenerateAttributionsAsync(string pluginId, string version, CancellationToken ct = default);
    Task<SBOM> GenerateSBOMAsync(string pluginId, string version, CancellationToken ct = default);
    Task<LicensePolicyEvaluation> EvaluateAgainstPolicyAsync(string pluginId, string version, string policyId, CancellationToken ct = default);
}

public sealed class LicenseComplianceReport
{
    public string PluginId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public ComplianceStatus Status { get; set; } = ComplianceStatus.Compliant;
    public string DeclaredLicense { get; set; } = string.Empty;
    public List<DetectedLicense> DetectedLicenses { get; set; } = new();
    public List<LicenseConflict> Conflicts { get; set; } = new();
    public List<LicenseObligation> Obligations { get; set; } = new();
    public List<string> RequiredAttributions { get; set; } = new();
    public bool HasCopyleft { get; set; }
    public bool HasPatentGrant { get; set; }
    public bool RequiresSourceDistribution { get; set; }
    public bool AllowsCommercialUse { get; set; }
    public bool AllowsModification { get; set; }
    public bool AllowsDistribution { get; set; }
    public List<LicensePolicyViolation> PolicyViolations { get; set; } = new();
    public double ComplianceScore { get; set; } = 100;
}

public enum ComplianceStatus
{
    Compliant,
    NonCompliant,
    Warning,
    Unknown
}

public sealed class DetectedLicense
{
    public string SpdxId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty; // manifest, file header, dependency, binary
    public string FilePath { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string MatchedText { get; set; } = string.Empty;
}

public sealed class LicenseConflict
{
    public string ConflictId { get; set; } = Guid.NewGuid().ToString();
    public string License1 { get; set; } = string.Empty;
    public string License2 { get; set; } = string.Empty;
    public ConflictType Type { get; set; } = ConflictType.Incompatible;
    public string Description { get; set; } = string.Empty;
    public Severity Severity { get; set; } = Severity.High;
    public string Resolution { get; set; } = string.Empty;
}

public enum ConflictType
{
    Incompatible,
    CopyleftViolation,
    MissingAttribution,
    MissingSourceDistribution,
    PatentGrantConflict,
    LicenseChange,
    DualLicensingConflict,
    ConditionallyCompatible
}

public sealed class LicenseCompatibilityMatrix
{
    public string LicenseId { get; set; } = string.Empty;
    public Dictionary<string, CompatibilityLevel> Compatibility { get; set; } = new();
    public List<string> IncompatibleWith { get; set; } = new();
    public List<string> CompatibleWith { get; set; } = new();
    public List<string> ConditionallyCompatible { get; set; } = new();
}

public enum CompatibilityLevel
{
    Compatible,
    Incompatible,
    ConditionallyCompatible,
    Unknown
}

public sealed class LicenseObligations
{
    public string LicenseId { get; set; } = string.Empty;
    public string LicenseName { get; set; } = string.Empty;
    public List<LicenseObligation> Obligations { get; set; } = new();
    public List<LicensePermission> Permissions { get; set; } = new();
    public List<LicenseLimitation> Limitations { get; set; } = new();
    public List<LicenseCondition> Conditions { get; set; } = new();
}

public sealed class LicenseObligation
{
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ObligationType Type { get; set; } = ObligationType.Other;
    public bool IsMandatory { get; set; } = true;
    public string HowToComply { get; set; } = string.Empty;
}

public enum ObligationType
{
    Attribution,
    SourceDistribution,
    LicenseNotice,
    CopyrightNotice,
    ModificationNotice,
    StateChanges,
    PatentGrant,
    TrademarkNotice,
    NetworkUse,
    InstallInstructions,
    Other
}

public sealed class LicensePermission
{
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsAllowed { get; set; }
    public string Conditions { get; set; } = string.Empty;
}

public sealed class LicenseLimitation
{
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class LicenseCondition
{
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Trigger { get; set; } = string.Empty;
}

public sealed class LicenseAttribution
{
    public string LicenseId { get; set; } = string.Empty;
    public string LicenseName { get; set; } = string.Empty;
    public string CopyrightHolder { get; set; } = string.Empty;
    public string CopyrightYear { get; set; } = string.Empty;
    public string LicenseText { get; set; } = string.Empty;
    public string AttributionText { get; set; } = string.Empty;
    public string SourceFile { get; set; } = string.Empty;
    public bool IsRequired { get; set; } = true;
}

public sealed class SBOM
{
    public string SpdxId { get; set; } = string.Empty;
    public string SpdxVersion { get; set; } = "SPDX-2.3";
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Creator { get; set; } = "KKBOT Marketplace";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<SBOMPackage> Packages { get; set; } = new();
    public List<SBOMPackage> Dependencies { get; set; } = new();
    public List<SBOMLicense> Licenses { get; set; } = new();
    public List<SBOMRelationship> Relationships { get; set; } = new();
}

public sealed class SBOMPackage
{
    public string SpdxId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Supplier { get; set; } = string.Empty;
    public string DownloadLocation { get; set; } = string.Empty;
    public string Originator { get; set; } = string.Empty;
    public List<string> LicenseConcluded { get; set; } = new();
    public List<string> LicenseDeclared { get; set; } = new();
    public string CopyrightText { get; set; } = string.Empty;
    public string ChecksumSha256 { get; set; } = string.Empty;
    public bool IsRoot { get; set; }
}

public sealed class SBOMLicense
{
    public string SpdxId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool IsOsiApproved { get; set; }
    public bool IsDeprecated { get; set; }
}

public sealed class SBOMRelationship
{
    public string SpdxElementId { get; set; } = string.Empty;
    public string RelatedSpdxElementId { get; set; } = string.Empty;
    public RelationshipType Type { get; set; } = RelationshipType.Describes;
}

public enum RelationshipType
{
    Describes,
    DependsOn,
    Contains,
    BuildDependency,
    TestDependency,
    OptionalDependency
}

public sealed class LicensePolicyEvaluation
{
    public string PolicyId { get; set; } = string.Empty;
    public string PolicyName { get; set; } = string.Empty;
    public bool IsCompliant { get; set; }
    public List<LicensePolicyViolation> Violations { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public double ComplianceScore { get; set; }
}

public sealed class LicensePolicyViolation
{
    public string RuleId { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ViolationSeverity Severity { get; set; } = ViolationSeverity.High;
    public string Remediation { get; set; } = string.Empty;
}

public enum ViolationSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public sealed class LicensePolicy
{
    public string PolicyId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> AllowedLicenses { get; set; } = new();
    public List<string> DisallowedLicenses { get; set; } = new();
    public List<string> RequiredAttributions { get; set; } = new();
    public bool RequireCopyleftApproval { get; set; } = true;
    public bool RequirePatentGrant { get; set; } = false;
    public bool BlockOnConflict { get; set; } = true;
    public List<PolicyRule> Rules { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}

public sealed class PolicyRule
{
    public string RuleId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ViolationSeverity Severity { get; set; } = ViolationSeverity.Medium;
    public string Condition { get; set; } = string.Empty; // Expression to evaluate
    public string Action { get; set; } = string.Empty; // deny, warn, require_approval
}

public sealed class LicenseComplianceService : ILicenseComplianceService
{
    private readonly MarketplaceDbContext _dbContext;
    private readonly ILogger<LicenseComplianceService> _logger;
    private readonly LicenseKnowledgeBase _knowledgeBase;

    public LicenseComplianceService(
        MarketplaceDbContext dbContext,
        ILogger<LicenseComplianceService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
        _knowledgeBase = LicenseKnowledgeBase.Instance;
    }

    public async Task<LicenseComplianceReport> CheckComplianceAsync(string pluginId, string version, CancellationToken ct = default)
    {
        var pluginVersion = await _dbContext.PluginVersions
            .Include(v => v.PluginPackage)
            .FirstOrDefaultAsync(v => v.PluginPackage!.PluginId == pluginId && v.Version == version, ct);

        if (pluginVersion == null)
            throw new InvalidOperationException($"Plugin {pluginId} v{version} not found");

        var report = new LicenseComplianceReport
        {
            PluginId = pluginId,
            Version = version,
            GeneratedAt = DateTime.UtcNow
        };

        // 1. Get declared license from manifest
        report.DeclaredLicense = await GetDeclaredLicenseAsync(pluginVersion, ct);

        // 2. Detect licenses from source files
        report.DetectedLicenses = await DetectLicensesAsync(pluginVersion, ct);

        // 3. Check for conflicts
        report.Conflicts = await DetectConflictsAsync(pluginId, version, ct);

        // 4. Get obligations
        report.Obligations = await GetAllObligationsAsync(report.DetectedLicenses, ct);

        // 5. Generate attributions
        report.RequiredAttributions = (await GenerateAttributionsAsync(pluginId, version, ct))
            .Select(a => a.AttributionText)
            .ToList();

        // 6. Determine license properties
        AnalyzeLicenseProperties(report);

        // 7. Check policy violations
        report.PolicyViolations = CheckPolicyViolationsAsync(report, ct);

        // 8. Calculate compliance score
        report.ComplianceScore = CalculateComplianceScore(report);

        // 9. Determine overall status
        report.Status = DetermineStatus(report);

        return report;
    }

    public async Task<LicenseCompatibilityMatrix> GetCompatibilityMatrixAsync(string licenseId, CancellationToken ct = default)
    {
        var license = _knowledgeBase.GetLicense(licenseId);
        if (license == null)
            throw new InvalidOperationException($"License {licenseId} not found in knowledge base");

        var matrix = new LicenseCompatibilityMatrix
        {
            LicenseId = licenseId
        };

        foreach (var otherLicense in _knowledgeBase.AllLicenses.Values)
        {
            if (otherLicense.SpdxId == licenseId) continue;

            var compatibility = DetermineCompatibility(license, otherLicense);
            matrix.Compatibility[otherLicense.SpdxId] = compatibility;

            switch (compatibility)
            {
                case CompatibilityLevel.Compatible:
                    matrix.CompatibleWith.Add(otherLicense.SpdxId);
                    break;
                case CompatibilityLevel.Incompatible:
                    matrix.IncompatibleWith.Add(otherLicense.SpdxId);
                    break;
                case CompatibilityLevel.ConditionallyCompatible:
                    matrix.ConditionallyCompatible.Add(otherLicense.SpdxId);
                    break;
            }
        }

        return matrix;
    }

    public async Task<LicenseObligations> GetLicenseObligationsAsync(string licenseId, CancellationToken ct = default)
    {
        var license = _knowledgeBase.GetLicense(licenseId);
        if (license == null)
            throw new InvalidOperationException($"License {licenseId} not found");

        return new LicenseObligations
        {
            LicenseId = license.SpdxId,
            LicenseName = license.Name,
            Obligations = license.Obligations
        };
    }

    public async Task<bool> IsLicenseCompatibleAsync(string license1, string license2, CancellationToken ct = default)
    {
        var l1 = _knowledgeBase.GetLicense(license1);
        var l2 = _knowledgeBase.GetLicense(license2);

        if (l1 == null || l2 == null) return false;

        return DetermineCompatibility(l1, l2) == CompatibilityLevel.Compatible;
    }

    public async Task<List<LicenseAttribution>> GenerateAttributionsAsync(string pluginId, string version, CancellationToken ct = default)
    {
        var attributions = new List<LicenseAttribution>();
        var pluginVersion = await _dbContext.PluginVersions
            .Include(v => v.PluginPackage)
            .FirstOrDefaultAsync(v => v.PluginPackage!.PluginId == pluginId && v.Version == version, ct);

        if (pluginVersion == null) return attributions;

        var detectedLicenses = await DetectLicensesAsync(pluginVersion, ct);
        var processedLicenses = new HashSet<string>();

        foreach (var detected in detectedLicenses)
        {
            if (processedLicenses.Contains(detected.SpdxId)) continue;
            processedLicenses.Add(detected.SpdxId);

            var license = _knowledgeBase.GetLicense(detected.SpdxId);
            if (license == null) continue;

            var attribution = new LicenseAttribution
            {
                LicenseId = license.SpdxId,
                LicenseName = license.Name,
                CopyrightHolder = await ExtractCopyrightHolderAsync(pluginVersion, detected, ct),
                CopyrightYear = DateTime.UtcNow.Year.ToString(),
                LicenseText = license.FullText ?? "",
                AttributionText = GenerateAttributionText(license, detected),
                SourceFile = detected.FilePath,
                IsRequired = true
            };

            attributions.Add(attribution);
        }

        return attributions;
    }

    public async Task<SBOM> GenerateSBOMAsync(string pluginId, string version, CancellationToken ct = default)
    {
        var pluginVersion = await _dbContext.PluginVersions
            .Include(v => v.PluginPackage)
                .ThenInclude(p => p.Versions)
            .Include(v => v.Dependencies)
            .FirstOrDefaultAsync(v => v.PluginPackage!.PluginId == pluginId && v.Version == version, ct);

        if (pluginVersion == null)
            throw new InvalidOperationException($"Plugin {pluginId} v{version} not found");

        var sbom = new SBOM
        {
            SpdxId = $"SPDXRef-PACKAGE-{pluginId}-{version}",
            Name = pluginVersion.PluginPackage!.Name,
            Version = version,
            Creator = "KKBOT Marketplace",
            CreatedAt = DateTime.UtcNow
        };

        // Root package
        var rootPackage = new SBOMPackage
        {
            SpdxId = sbom.SpdxId,
            Name = pluginVersion.PluginPackage!.Name,
            Version = version,
            Supplier = pluginVersion.PluginPackage.Publisher?.Name ?? "Unknown",
            DownloadLocation = "https://marketplace.kkbot.example.com",
            Originator = pluginVersion.PluginPackage.Publisher?.Name ?? "Unknown",
            LicenseConcluded = new List<string> { pluginVersion.PluginPackage.LicenseId },
            LicenseDeclared = new List<string> { pluginVersion.PluginPackage.LicenseId },
            CopyrightText = await GetCopyrightTextAsync(pluginVersion, ct),
            ChecksumSha256 = pluginVersion.FileHash,
            IsRoot = true
        };
        sbom.Packages.Add(rootPackage);

        // Dependencies
        foreach (var dep in pluginVersion.Dependencies)
        {
            var depPackage = new SBOMPackage
            {
                SpdxId = $"SPDXRef-DEP-{dep.DependencyPluginId}-{dep.VersionRange}",
                Name = dep.DependencyPluginId,
                Version = dep.VersionRange,
                Supplier = "Unknown",
                DownloadLocation = "https://marketplace.kkbot.example.com",
                Originator = "Unknown",
                LicenseConcluded = new List<string>(), // Would need to resolve
                LicenseDeclared = new List<string>(),
                CopyrightText = "Unknown",
                ChecksumSha256 = "",
                IsRoot = false
            };
            sbom.Dependencies.Add(depPackage);
            sbom.Packages.Add(depPackage);

            // Relationship
            sbom.Relationships.Add(new SBOMRelationship
            {
                SpdxElementId = sbom.SpdxId,
                RelatedSpdxElementId = depPackage.SpdxId,
                Type = RelationshipType.DependsOn
            });
        }

        // Licenses
        var licenses = new HashSet<string>();
        foreach (var pkg in sbom.Packages)
        {
            foreach (var lic in pkg.LicenseConcluded)
                licenses.Add(lic);
            foreach (var lic in pkg.LicenseDeclared)
                licenses.Add(lic);
        }

        foreach (var licId in licenses)
        {
            var license = _knowledgeBase.GetLicense(licId);
            if (license != null)
            {
                sbom.Licenses.Add(new SBOMLicense
                {
                    SpdxId = license.SpdxId,
                    Name = license.Name,
                    Text = license.FullText ?? "",
                    IsOsiApproved = license.IsOsiApproved,
                    IsDeprecated = license.IsDeprecated
                });
            }
        }

        return sbom;
    }

    public async Task<LicensePolicyEvaluation> EvaluateAgainstPolicyAsync(string pluginId, string version, string policyId, CancellationToken ct = default)
    {
        var policy = await _dbContext.LicensePolicies
            .FirstOrDefaultAsync(p => p.PolicyId == policyId && p.IsActive, ct);

        if (policy == null)
            throw new InvalidOperationException($"Policy {policyId} not found or inactive");

        var report = await CheckComplianceAsync(pluginId, version, ct);
        var evaluation = new LicensePolicyEvaluation
        {
            PolicyId = policy.PolicyId,
            PolicyName = policy.Name
        };

        // Check allowed/denied licenses
        var detectedLicenseIds = report.DetectedLicenses.Select(d => d.SpdxId).ToHashSet();

        foreach (var disallowed in policy.DisallowedLicenses)
        {
            if (detectedLicenseIds.Contains(disallowed))
            {
                evaluation.Violations.Add(new LicensePolicyViolation
                {
                    RuleId = "DISALLOWED_LICENSE",
                    RuleName = "Disallowed License Used",
                    Description = $"Plugin uses disallowed license: {disallowed}",
                    Severity = ViolationSeverity.Critical,
                    Remediation = "Replace dependency or request license exception"
                });
            }
        }

        foreach (var allowed in policy.AllowedLicenses)
        {
            detectedLicenseIds.Remove(allowed);
        }

        // Check remaining licenses not in allowed list
        foreach (var unlisted in detectedLicenseIds)
        {
            evaluation.Violations.Add(new LicensePolicyViolation
            {
                RuleId = "UNLISTED_LICENSE",
                RuleName = "Unlisted License",
                Description = $"Plugin uses license not in allowed list: {unlisted}",
                Severity = ViolationSeverity.High,
                Remediation = "Add license to allowed list or replace dependency"
            });
        }

        // Check copyleft
        if (policy.RequireCopyleftApproval && report.HasCopyleft)
        {
            evaluation.Violations.Add(new LicensePolicyViolation
            {
                RuleId = "COPYLEFT_REQUIRES_APPROVAL",
                RuleName = "Copyleft License Requires Approval",
                Description = "Plugin uses copyleft license which requires explicit approval",
                Severity = ViolationSeverity.High,
                Remediation = "Obtain explicit approval for copyleft license usage"
            });
        }

        // Check patent grant
        if (policy.RequirePatentGrant && !report.HasPatentGrant)
        {
            evaluation.Violations.Add(new LicensePolicyViolation
            {
                RuleId = "PATENT_GRANT_REQUIRED",
                RuleName = "Patent Grant Required",
                Description = "Policy requires patent grant but detected licenses don't provide one",
                Severity = ViolationSeverity.High,
                Remediation = "Use licenses with explicit patent grants (Apache-2.0, GPL-3.0)"
            });
        }

        // Check conflicts
        if (policy.BlockOnConflict && report.Conflicts.Any(c => c.Severity >= Severity.High))
        {
            evaluation.Violations.Add(new LicensePolicyViolation
            {
                RuleId = "LICENSE_CONFLICT",
                RuleName = "License Conflict Detected",
                Description = "High-severity license conflicts detected",
                Severity = ViolationSeverity.Critical,
                Remediation = "Resolve license conflicts before publishing"
            });
        }

        // Custom rules
        foreach (var rule in policy.Rules)
        {
            // Would evaluate rule.Condition expression
            // For now, skip custom rule evaluation
        }

        evaluation.IsCompliant = !evaluation.Violations.Any(v => v.Severity >= ViolationSeverity.High);
        evaluation.ComplianceScore = 100 - evaluation.Violations.Sum(v => (int)v.Severity * 10);
        evaluation.ComplianceScore = Math.Max(0, evaluation.ComplianceScore);

        return evaluation;
    }

    // Private helper methods

    private async Task<string> GetDeclaredLicenseAsync(PluginVersion version, CancellationToken ct)
    {
        if (version.PluginPackage == null) return "Unknown";
        return version.PluginPackage.LicenseId;
    }

    private async Task<List<DetectedLicense>> DetectLicensesAsync(PluginVersion version, CancellationToken ct)
    {
        var detected = new List<DetectedLicense>();
        var processedFiles = new HashSet<string>();

        // 1. Check manifest
        if (!string.IsNullOrEmpty(version.ManifestJson))
        {
            try
            {
                var manifest = JsonDocument.Parse(version.ManifestJson);
                if (manifest.RootElement.TryGetProperty("license", out var licenseElement))
                {
                    var licenseId = licenseElement.GetString() ?? "";
                    if (!string.IsNullOrEmpty(licenseId))
                    {
                        detected.Add(new DetectedLicense
                        {
                            SpdxId = licenseId,
                            Name = _knowledgeBase.GetLicense(licenseId)?.Name ?? licenseId,
                            Source = "manifest",
                            FilePath = "manifest.json",
                            Confidence = 1.0,
                            MatchedText = licenseId
                        });
                    }
                }
            }
            catch { }
        }

        // 2. Scan plugin assembly for license headers (if source available)
        // This would require source code or decompilation
        // For now, we'll rely on manifest and dependency analysis

        // 3. Check dependencies for their licenses
        var dependencies = await _dbContext.PluginDependencies
            .Where(d => d.PluginVersionId == version.Id)
            .ToListAsync(ct);

        foreach (var dep in dependencies)
        {
            var depVersion = await _dbContext.PluginVersions
                .Include(v => v.PluginPackage)
                .FirstOrDefaultAsync(v => v.PluginPackage!.PluginId == dep.DependencyPluginId, ct);

            if (depVersion?.PluginPackage != null)
            {
                detected.Add(new DetectedLicense
                {
                    SpdxId = depVersion.PluginPackage.LicenseId,
                    Name = _knowledgeBase.GetLicense(depVersion.PluginPackage.LicenseId)?.Name ?? depVersion.PluginPackage.LicenseId,
                    Source = "dependency",
                    FilePath = $"dependency:{dep.DependencyPluginId}",
                    Confidence = 0.9,
                    MatchedText = $"Dependency on {dep.DependencyPluginId}"
                });
            }
        }

        return detected.DistinctBy(d => d.SpdxId).ToList();
    }

    private void AnalyzeLicenseProperties(LicenseComplianceReport report)
    {
        var allLicenses = report.DetectedLicenses.Select(d => _knowledgeBase.GetLicense(d.SpdxId)).Where(l => l != null).Select(l => l!).ToList();

        report.HasCopyleft = allLicenses.Any(l => l.IsCopyleft);
        report.HasPatentGrant = allLicenses.Any(l => l.HasPatentGrant);
        report.RequiresSourceDistribution = allLicenses.Any(l => l.RequiresSourceDistribution);
        report.AllowsCommercialUse = allLicenses.All(l => l.AllowsCommercialUse);
        report.AllowsModification = allLicenses.All(l => l.AllowsModification);
        report.AllowsDistribution = allLicenses.All(l => l.AllowsDistribution);
    }

    private async Task<List<LicenseObligation>> GetAllObligationsAsync(List<DetectedLicense> detected, CancellationToken ct)
    {
        var obligations = new List<LicenseObligation>();

        foreach (var det in detected)
        {
            var license = _knowledgeBase.GetLicense(det.SpdxId);
            if (license?.Obligations != null)
            {
                foreach (var obl in license.Obligations)
                {
                    obligations.Add(new LicenseObligation
                    {
                        Id = $"{det.SpdxId}-{obl.Id}",
                        Description = obl.Description,
                        Type = obl.Type,
                        IsMandatory = obl.IsMandatory,
                        HowToComply = obl.HowToComply
                    });
                }
            }
        }

        return obligations.DistinctBy(o => o.Id).ToList();
    }

    public async Task<List<LicenseConflict>> DetectConflictsAsync(string pluginId, string version, CancellationToken ct)
    {
        var conflicts = new List<LicenseConflict>();
        var report = await CheckComplianceAsync(pluginId, version, ct);
        var licenses = report.DetectedLicenses.Select(d => _knowledgeBase.GetLicense(d.SpdxId)).Where(l => l != null).ToList();

        // Check pairwise compatibility
        for (int i = 0; i < licenses.Count; i++)
        {
            for (int j = i + 1; j < licenses.Count; j++)
            {
                var compat = DetermineCompatibility(licenses[i]!, licenses[j]!);
                
                if (compat == CompatibilityLevel.Incompatible)
                {
                    conflicts.Add(new LicenseConflict
                    {
                        License1 = licenses[i]!.SpdxId,
                        License2 = licenses[j]!.SpdxId,
                        Type = ConflictType.Incompatible,
                        Description = $"{licenses[i]!.Name} and {licenses[j]!.Name} are incompatible",
                        Severity = Severity.Critical,
                        Resolution = "Remove one dependency or find alternative with compatible license"
                    });
                }
                else if (compat == CompatibilityLevel.ConditionallyCompatible)
                {
                    conflicts.Add(new LicenseConflict
                    {
                        License1 = licenses[i]!.SpdxId,
                        License2 = licenses[j]!.SpdxId,
                        Type = ConflictType.ConditionallyCompatible,
                        Description = $"{licenses[i]!.Name} and {licenses[j]!.Name} have conditional compatibility",
                        Severity = Severity.Medium,
                        Resolution = "Review compatibility conditions and ensure compliance"
                    });
                }
            }
        }

        // Check for copyleft violations
        var copyleftLicenses = licenses.Where(l => l?.IsCopyleft == true).ToList();
        var nonCopyleftLicenses = licenses.Where(l => l?.IsCopyleft != true).ToList();

        foreach (var copyleft in copyleftLicenses)
        {
            foreach (var other in nonCopyleftLicenses)
            {
                if (DetermineCompatibility(copyleft!, other!) == CompatibilityLevel.Incompatible)
                {
                    conflicts.Add(new LicenseConflict
                    {
                        License1 = copyleft!.SpdxId,
                        License2 = other!.SpdxId,
                        Type = ConflictType.CopyleftViolation,
                        Description = $"Copyleft license {copyleft!.Name} incompatible with {other!.Name}",
                        Severity = Severity.Critical,
                        Resolution = "Ensure all combined works comply with copyleft requirements or replace dependency"
                    });
                }
            }
        }

        return conflicts.DistinctBy(c => $"{c.License1}-{c.License2}").ToList();
    }

    private List<LicensePolicyViolation> CheckPolicyViolationsAsync(LicenseComplianceReport report, CancellationToken ct)
    {
        var violations = new List<LicensePolicyViolation>();

        // Check for missing attributions
        if (report.Obligations.Any(o => o.Type == ObligationType.Attribution && o.IsMandatory))
        {
            violations.Add(new LicensePolicyViolation
            {
                RuleId = "MISSING_ATTRIBUTION",
                RuleName = "Missing Required Attribution",
                Description = "License requires attribution but none provided in plugin",
                Severity = ViolationSeverity.High,
                Remediation = "Add attribution notices to plugin documentation and UI"
            });
        }

        // Check source distribution for copyleft
        if (report.HasCopyleft && !report.RequiresSourceDistribution)
        {
            violations.Add(new LicensePolicyViolation
            {
                RuleId = "SOURCE_DISTRIBUTION",
                RuleName = "Source Distribution Required",
                Description = "Copyleft license requires source code distribution",
                Severity = ViolationSeverity.Critical,
                Remediation = "Provide source code or use compatible license"
            });
        }

        return violations;
    }

    private double CalculateComplianceScore(LicenseComplianceReport report)
    {
        double score = 100;

        // Deduct for conflicts
        score -= report.Conflicts.Count * 15;
        score -= report.Conflicts.Count(c => c.Severity == Severity.Critical) * 10;

        // Deduct for policy violations
        score -= report.PolicyViolations.Count * 10;
        score -= report.PolicyViolations.Count(v => v.Severity == ViolationSeverity.Critical) * 15;
        score -= report.PolicyViolations.Count(v => v.Severity == ViolationSeverity.High) * 10;

        // Deduct for unknown licenses
        var unknownLicenses = report.DetectedLicenses.Count(d => _knowledgeBase.GetLicense(d.SpdxId) == null);
        score -= unknownLicenses * 10;

        // Deduct for missing obligations
        score -= report.Obligations.Count(o => o.IsMandatory) * 5;

        return Math.Max(0, Math.Min(100, score));
    }

    private ComplianceStatus DetermineStatus(LicenseComplianceReport report)
    {
        if (report.Conflicts.Any(c => c.Severity == Severity.Critical) ||
            report.PolicyViolations.Any(v => v.Severity == ViolationSeverity.Critical))
            return ComplianceStatus.NonCompliant;

        if (report.Conflicts.Any(c => c.Severity == Severity.High) ||
            report.PolicyViolations.Any(v => v.Severity == ViolationSeverity.High))
            return ComplianceStatus.Warning;

        return ComplianceStatus.Compliant;
    }

    private CompatibilityLevel DetermineCompatibility(License license1, License license2)
    {
        // Same license is always compatible
        if (license1.SpdxId == license2.SpdxId) return CompatibilityLevel.Compatible;

        // Check explicit compatibility rules
        if (license1.CompatibleLicenses.Contains(license2.SpdxId))
            return CompatibilityLevel.Compatible;

        if (license1.IncompatibleLicenses.Contains(license2.SpdxId))
            return CompatibilityLevel.Incompatible;

        // Copyleft vs non-copyleft
        if (license1.IsCopyleft && !license2.IsCopyleft)
        {
            // GPL family with non-copyleft
            if (IsGplFamily(license1) && !IsGplFamily(license2))
                return CompatibilityLevel.Incompatible;
        }

        if (license2.IsCopyleft && !license1.IsCopyleft)
        {
            if (IsGplFamily(license2) && !IsGplFamily(license1))
                return CompatibilityLevel.Incompatible;
        }

        // Apache-2.0 compatible with most
        if (license1.SpdxId == "Apache-2.0" || license2.SpdxId == "Apache-2.0")
            return CompatibilityLevel.Compatible;

        // MIT/BSD compatible with almost everything
        if (IsPermissive(license1) && IsPermissive(license2))
            return CompatibilityLevel.Compatible;

        // Default to conditional
        return CompatibilityLevel.ConditionallyCompatible;
    }

    private bool IsGplFamily(License license)
    {
        return license.SpdxId.StartsWith("GPL-") || license.SpdxId.StartsWith("AGPL-") || license.SpdxId.StartsWith("LGPL-");
    }

    private bool IsPermissive(License license)
    {
        var permissive = new[] { "MIT", "BSD-2-Clause", "BSD-3-Clause", "Apache-2.0", "ISC", "Unlicense", "Zlib" };
        return permissive.Contains(license.SpdxId) || license.SpdxId.StartsWith("MIT-") || license.SpdxId.StartsWith("BSD-");
    }

    private async Task<string> ExtractCopyrightHolderAsync(PluginVersion version, DetectedLicense detected, CancellationToken ct)
    {
        // Would extract from source files
        return version.PluginPackage?.Publisher?.Name ?? "Unknown";
    }

    private string GenerateAttributionText(License license, DetectedLicense detected)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"This software includes components licensed under the {license.Name} ({license.SpdxId}).");
        sb.AppendLine();
        sb.AppendLine("Copyright (c) " + DateTime.UtcNow.Year + " " + "Contributors");
        sb.AppendLine();
        
        if (license.RequiresAttribution)
        {
            sb.AppendLine("The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.");
        }

        return sb.ToString();
    }

    private async Task<string> GetCopyrightTextAsync(PluginVersion version, CancellationToken ct)
    {
        return $"Copyright (c) {DateTime.UtcNow.Year} {version.PluginPackage?.Publisher?.Name ?? "Contributors"}";
    }

    private class LicenseKnowledgeBase
    {
        private static readonly Lazy<LicenseKnowledgeBase> _instance = new(() => new LicenseKnowledgeBase());
        public static LicenseKnowledgeBase Instance => _instance.Value;

        public Dictionary<string, License> AllLicenses { get; } = new();
        public Dictionary<string, LicenseCompatibilityRule> CompatibilityRules { get; } = new();

        private LicenseKnowledgeBase()
        {
            InitializeLicenses();
            InitializeCompatibilityRules();
        }

        public License? GetLicense(string spdxId)
        {
            AllLicenses.TryGetValue(spdxId, out var license);
            return license;
        }

        private void InitializeLicenses()
        {
            // MIT License
            AllLicenses["MIT"] = new License
            {
                SpdxId = "MIT",
                Name = "MIT License",
                Url = "https://opensource.org/licenses/MIT",
                FullText = "MIT License text...",
                IsOsiApproved = true,
                IsCopyleft = false,
                AllowsCommercialUse = true,
                AllowsModification = true,
                AllowsDistribution = true,
                RequiresAttribution = true,
                RequiresSourceDistribution = false,
                HasPatentGrant = false,
                RequiresLicenseNotice = true,
                RequiresCopyrightNotice = true,
                RequiresStateChanges = false,
                CompatibleLicenses = new[] { "MIT", "BSD-2-Clause", "BSD-3-Clause", "Apache-2.0", "ISC", "Unlicense" }.ToList(),
                IncompatibleLicenses = new[] { "GPL-2.0", "GPL-3.0", "AGPL-3.0" }.ToList(),
                Obligations = new List<LicenseObligation>
                {
                    new LicenseObligation { Id = "MIT-ATTR", Description = "Include copyright notice and license text", Type = ObligationType.Attribution, IsMandatory = true, HowToComply = "Include license text in distribution" },
                    new LicenseObligation { Id = "MIT-COPYRIGHT", Description = "Preserve copyright notice", Type = ObligationType.CopyrightNotice, IsMandatory = true, HowToComply = "Keep copyright notice intact" }
                }
            };

            // Apache-2.0
            AllLicenses["Apache-2.0"] = new License
            {
                SpdxId = "Apache-2.0",
                Name = "Apache License 2.0",
                Url = "https://www.apache.org/licenses/LICENSE-2.0",
                FullText = "Apache License 2.0 text...",
                IsOsiApproved = true,
                IsCopyleft = false,
                AllowsCommercialUse = true,
                AllowsModification = true,
                AllowsDistribution = true,
                RequiresAttribution = true,
                RequiresSourceDistribution = false,
                HasPatentGrant = true,
                RequiresLicenseNotice = true,
                RequiresCopyrightNotice = true,
                RequiresStateChanges = true,
                CompatibleLicenses = new[] { "MIT", "BSD-2-Clause", "BSD-3-Clause", "Apache-2.0", "ISC", "Unlicense", "GPL-3.0" }.ToList(),
                IncompatibleLicenses = new[] { "GPL-2.0", "GPL-3.0", "AGPL-3.0" }.ToList(),
                Obligations = new List<LicenseObligation>
                {
                    new LicenseObligation { Id = "APACHE-ATTR", Description = "Include license and copyright notice", Type = ObligationType.Attribution, IsMandatory = true, HowToComply = "Include NOTICE file if present" },
                    new LicenseObligation { Id = "APACHE-PATENT", Description = "Patent grant applies", Type = ObligationType.PatentGrant, IsMandatory = false, HowToComply = "No action needed" },
                    new LicenseObligation { Id = "APACHE-STATE-CHANGES", Description = "Document modifications", Type = ObligationType.StateChanges, IsMandatory = true, HowToComply = "Add NOTICE of changes" }
                }
            };

            // GPL-3.0
            AllLicenses["GPL-3.0"] = new License
            {
                SpdxId = "GPL-3.0",
                Name = "GNU General Public License v3.0",
                Url = "https://www.gnu.org/licenses/gpl-3.0.html",
                FullText = "GPL-3.0 text...",
                IsOsiApproved = true,
                IsCopyleft = true,
                AllowsCommercialUse = true,
                AllowsModification = true,
                AllowsDistribution = true,
                RequiresAttribution = true,
                RequiresSourceDistribution = true,
                HasPatentGrant = true,
                RequiresLicenseNotice = true,
                RequiresCopyrightNotice = true,
                RequiresStateChanges = true,
                CompatibleLicenses = new[] { "GPL-3.0", "AGPL-3.0", "LGPL-3.0", "Apache-2.0" }.ToList(),
                IncompatibleLicenses = new[] { "MIT", "BSD-2-Clause", "BSD-3-Clause", "Apache-2.0", "Proprietary" }.ToList(),
                Obligations = new List<LicenseObligation>
                {
                    new LicenseObligation { Id = "GPL-SOURCE", Description = "Provide complete source code", Type = ObligationType.SourceDistribution, IsMandatory = true, HowToComply = "Distribute complete corresponding source code" },
                    new LicenseObligation { Id = "GPL-LICENSE", Description = "Include license copy", Type = ObligationType.LicenseNotice, IsMandatory = true, HowToComply = "Include full GPL-3.0 license text" },
                    new LicenseObligation { Id = "GPL-COPYRIGHT", Description = "Preserve copyright notices", Type = ObligationType.CopyrightNotice, IsMandatory = true, HowToComply = "Keep all copyright notices intact" },
                    new LicenseObligation { Id = "GPL-STATE-CHANGES", Description = "Document modifications", Type = ObligationType.StateChanges, IsMandatory = true, HowToComply = "Document all changes made" },
                    new LicenseObligation { Id = "GPL-PATENT", Description = "Patent grant provided", Type = ObligationType.PatentGrant, IsMandatory = false, HowToComply = "No action needed" }
                }
            };

            // BSD-3-Clause
            AllLicenses["BSD-3-Clause"] = new License
            {
                SpdxId = "BSD-3-Clause",
                Name = "BSD 3-Clause License",
                Url = "https://opensource.org/licenses/BSD-3-Clause",
                FullText = "BSD-3-Clause text...",
                IsOsiApproved = true,
                IsCopyleft = false,
                AllowsCommercialUse = true,
                AllowsModification = true,
                AllowsDistribution = true,
                RequiresAttribution = true,
                RequiresSourceDistribution = false,
                HasPatentGrant = false,
                RequiresLicenseNotice = true,
                RequiresCopyrightNotice = true,
                RequiresStateChanges = false,
                Obligations = new List<LicenseObligation>
                {
                    new LicenseObligation { Id = "BSD-ATTR", Description = "Retain copyright notice", Type = ObligationType.CopyrightNotice, IsMandatory = true, HowToComply = "Keep copyright notice in redistributions" }
                }
            };

            // Proprietary
            AllLicenses["Proprietary"] = new License
            {
                SpdxId = "Proprietary",
                Name = "Proprietary License",
                Url = "",
                FullText = "Proprietary - All rights reserved",
                IsOsiApproved = false,
                IsCopyleft = false,
                AllowsCommercialUse = false,
                AllowsModification = false,
                AllowsDistribution = false,
                RequiresAttribution = false,
                RequiresSourceDistribution = false,
                HasPatentGrant = false,
                RequiresLicenseNotice = false,
                RequiresCopyrightNotice = false,
                RequiresStateChanges = false,
                CompatibleLicenses = new List<string>(),
                IncompatibleLicenses = new List<string>(),
                Obligations = new List<LicenseObligation>()
            };

            // Add more licenses as needed...
        }

        private void InitializeCompatibilityRules()
        {
            // MIT compatible with most
            CompatibilityRules["MIT"] = new LicenseCompatibilityRule
            {
                LicenseId = "MIT",
                CompatibleWith = new[] { "MIT", "BSD-2-Clause", "BSD-3-Clause", "Apache-2.0", "ISC", "Unlicense", "GPL-3.0", "LGPL-3.0" }.ToList(),
                IncompatibleWith = new[] { "GPL-2.0", "AGPL-3.0" }.ToList(),
                ConditionallyCompatible = new Dictionary<string, string>
                {
                    ["GPL-2.0"] = "Only if combined work is licensed under GPL-2.0 or later"
                }
            };

            CompatibilityRules["Apache-2.0"] = new LicenseCompatibilityRule
            {
                LicenseId = "Apache-2.0",
                CompatibleWith = new[] { "MIT", "BSD-2-Clause", "BSD-3-Clause", "Apache-2.0", "ISC", "Unlicense", "GPL-3.0", "LGPL-3.0" }.ToList(),
                IncompatibleWith = new[] { "GPL-2.0", "AGPL-3.0" }.ToList()
            };

            CompatibilityRules["GPL-3.0"] = new LicenseCompatibilityRule
            {
                LicenseId = "GPL-3.0",
                CompatibleWith = new[] { "GPL-3.0", "AGPL-3.0", "LGPL-3.0", "Apache-2.0" }.ToList(),
                IncompatibleWith = new[] { "MIT", "BSD-2-Clause", "BSD-3-Clause", "Apache-2.0", "Proprietary" }.ToList(),
                ConditionallyCompatible = new Dictionary<string, string>
                {
                    ["LGPL-2.1"] = "Only if LGPL code is dynamically linked",
                    ["Apache-2.0"] = "Compatible - Apache-2.0 code can be included in GPL-3.0 work"
                }
            };

            CompatibilityRules["GPL-2.0"] = new LicenseCompatibilityRule
            {
                LicenseId = "GPL-2.0",
                CompatibleWith = new[] { "GPL-2.0", "GPL-3.0", "LGPL-2.1" }.ToList(),
                IncompatibleWith = new[] { "MIT", "BSD-2-Clause", "BSD-3-Clause", "Apache-2.0", "Proprietary" }.ToList()
            };

            CompatibilityRules["MIT"] = new LicenseCompatibilityRule
            {
                LicenseId = "MIT",
                CompatibleWith = new[] { "MIT", "BSD-2-Clause", "BSD-3-Clause", "Apache-2.0", "ISC", "Unlicense", "GPL-3.0", "LGPL-3.0", "Proprietary" }.ToList(),
                IncompatibleWith = new[] { "GPL-2.0", "AGPL-3.0" }.ToList()
            };

            CompatibilityRules["BSD-3-Clause"] = new LicenseCompatibilityRule
            {
                LicenseId = "BSD-3-Clause",
                CompatibleWith = new[] { "MIT", "BSD-2-Clause", "BSD-3-Clause", "Apache-2.0", "ISC", "Unlicense", "GPL-3.0", "LGPL-3.0", "Proprietary" }.ToList(),
                IncompatibleWith = new[] { "GPL-2.0" }.ToList()
            };

            CompatibilityRules["LGPL-3.0"] = new LicenseCompatibilityRule
            {
                LicenseId = "LGPL-3.0",
                CompatibleWith = new[] { "LGPL-3.0", "GPL-3.0", "AGPL-3.0", "Apache-2.0" }.ToList(),
                IncompatibleWith = new[] { "GPL-2.0" }.ToList(),
                ConditionallyCompatible = new Dictionary<string, string>
                {
                    ["MIT"] = "Only if LGPL code is dynamically linked",
                    ["Apache-2.0"] = "Compatible"
                }
            };
        }
    }

    public sealed class License
    {
        public string SpdxId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string FullText { get; set; } = string.Empty;
        public bool IsOsiApproved { get; set; }
        public bool IsCopyleft { get; set; }
        public bool AllowsCommercialUse { get; set; }
        public bool AllowsModification { get; set; }
        public bool AllowsDistribution { get; set; }
        public bool RequiresAttribution { get; set; }
        public bool RequiresSourceDistribution { get; set; }
        public bool HasPatentGrant { get; set; }
        public bool RequiresLicenseNotice { get; set; }
        public bool RequiresCopyrightNotice { get; set; }
        public bool RequiresStateChanges { get; set; }
        public bool IsDeprecated { get; set; }
        public List<string> CompatibleLicenses { get; set; } = new();
        public List<string> IncompatibleLicenses { get; set; } = new();
        public List<LicenseObligation> Obligations { get; set; } = new();
    }

    public sealed class LicenseCompatibilityRule
    {
        public string LicenseId { get; set; } = string.Empty;
        public List<string> CompatibleWith { get; set; } = new();
        public List<string> IncompatibleWith { get; set; } = new();
        public Dictionary<string, string> ConditionallyCompatible { get; set; } = new();
    }
}