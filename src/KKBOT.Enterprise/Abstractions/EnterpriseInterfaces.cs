// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Enterprise.Models;

namespace KKBOT.Enterprise.Abstractions;

public interface IIdentityProviderService
{
    Task<IdentityProvider> CreateIdentityProviderAsync(CreateIdentityProviderRequest request, string userId, CancellationToken ct = default);
    Task<IdentityProvider?> GetIdentityProviderAsync(Guid id, string userId, CancellationToken ct = default);
    Task<IdentityProvider?> GetIdentityProviderByNameAsync(string name, CancellationToken ct = default);
    Task<List<IdentityProvider>> GetIdentityProvidersAsync(string userId, IdentityProviderFilter filter, CancellationToken ct = default);
    Task<IdentityProvider> UpdateIdentityProviderAsync(Guid id, UpdateIdentityProviderRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeleteIdentityProviderAsync(Guid id, string userId, CancellationToken ct = default);
    Task<bool> TestIdentityProviderAsync(Guid id, CancellationToken ct = default);
    Task<string> GetLoginUrlAsync(Guid providerId, string returnUrl, CancellationToken ct = default);
    Task<string> GetLogoutUrlAsync(Guid providerId, string returnUrl, CancellationToken ct = default);
}

public sealed record CreateIdentityProviderRequest(
    string Name,
    string DisplayName,
    IdentityProviderType Type,
    string IssuerUri,
    string ClientId,
    string ClientSecret,
    string CallbackPath,
    string Scopes,
    string ResponseType,
    string ResponseMode,
    bool UsePkce,
    string MetadataUrl,
    string Authority,
    string SigningAlgorithm,
    string CertificateThumbprint,
    Dictionary<string, string> ClaimMapping,
    bool AutoProvisionUsers,
    string DefaultRole,
    Dictionary<string, string> RoleMapping,
    bool AllowIdpInitiatedLogin,
    bool AllowIdpInitiatedLogout,
    string SloUrl,
    int TokenLifetimeMinutes,
    int RefreshTokenLifetimeDays,
    bool RequireMfa,
    List<string> AllowedDomains,
    List<string> BlockedDomains
);

public sealed record UpdateIdentityProviderRequest(
    string? Name = null,
    string? DisplayName = null,
    bool? IsEnabled = null,
    bool? IsDefault = null,
    int? Priority = null,
    string? IssuerUri = null,
    string? ClientId = null,
    string? ClientSecret = null,
    string? CallbackPath = null,
    string? Scopes = null,
    string? ResponseType = null,
    string? ResponseMode = null,
    bool? UsePkce = null,
    string? MetadataUrl = null,
    string? Authority = null,
    string? SigningAlgorithm = null,
    string? CertificateThumbprint = null,
    string? PublicKeyXml = null,
    Dictionary<string, string>? ClaimMapping = null,
    bool? AutoProvisionUsers = null,
    string? DefaultRole = null,
    Dictionary<string, string>? RoleMapping = null,
    bool? AllowIdpInitiatedLogin = null,
    bool? AllowIdpInitiatedLogout = null,
    string? SloUrl = null,
    int? TokenLifetimeMinutes = null,
    int? RefreshTokenLifetimeDays = null,
    bool? RequireMfa = null,
    List<string>? AllowedDomains = null,
    List<string>? BlockedDomains = null
);

public sealed record IdentityProviderFilter(
    IdentityProviderType? Type = null,
    bool? IsEnabled = null,
    bool? IsDefault = null,
    string? TenantId = null,
    int Page = 1,
    int PageSize = 20
);

public interface ISaml2Service
{
    Task<string> GetMetadataAsync(Guid identityProviderId, CancellationToken ct = default);
    Task<string> GetLoginUrlAsync(Guid identityProviderId, string returnUrl, CancellationToken ct = default);
    Task<string> GetLogoutUrlAsync(Guid identityProviderId, string returnUrl, CancellationToken ct = default);
    Task<Saml2Response> ProcessSamlResponseAsync(string samlResponse, CancellationToken ct = default);
    Task<Saml2LogoutRequest> ProcessSamlLogoutRequestAsync(string samlRequest, CancellationToken ct = default);
    Task<Saml2LogoutResponse> ProcessSamlLogoutResponseAsync(string samlResponse, CancellationToken ct = default);
    Task<bool> ValidateSamlSignatureAsync(string samlXml, CancellationToken ct = default);
}

public sealed record Saml2Response(
    string NameId,
    string SessionIndex,
    Dictionary<string, string> Attributes,
    string RelayState,
    bool IsSuccess,
    string? ErrorMessage
);

public sealed record Saml2LogoutRequest(
    string NameId,
    string SessionIndex,
    string RelayState,
    string Destination
);

public sealed record Saml2LogoutResponse(
    bool IsSuccess,
    string? ErrorMessage
);

public interface IAuditService
{
    Task LogAsync(AuditLogEntry entry, CancellationToken ct = default);
    Task<List<AuditLogEntry>> QueryAsync(AuditQuery query, CancellationToken ct = default);
    Task<long> CountAsync(AuditQuery query, CancellationToken ct = default);
    Task<AuditStats> GetStatisticsAsync(string tenantId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<byte[]> ExportAsync(AuditQuery query, ExportFormat format, CancellationToken ct = default);
    Task<bool> PurgeAsync(DateTime beforeDate, string tenantId, CancellationToken ct = default);
    Task<AuditLogEntry?> GetByIdAsync(Guid id, CancellationToken ct = default);
}

public sealed record AuditQuery(
    string? TenantId = null,
    string? UserId = null,
    AuditEventType? EventType = null,
    AuditOutcome? Outcome = null,
    RiskLevel? RiskLevel = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? ResourceType = null,
    string? ResourceId = null,
    string? Action = null,
    string? EventCategory = null,
    string? CorrelationId = null,
    string? TraceId = null,
    int Page = 1,
    int PageSize = 100,
    string SortBy = "timestamp",
    bool SortDescending = true
);

public sealed record AuditStats(
    long TotalEvents,
    long SuccessCount,
    long FailureCount,
    long CriticalCount,
    long HighCount,
    long MediumCount,
    long LowCount,
    Dictionary<string, long> EventsByType,
    Dictionary<string, long> EventsByCategory,
    Dictionary<string, long> EventsByOutcome,
    Dictionary<string, long> EventsByRiskLevel,
    Dictionary<string, long> EventsByUser,
    Dictionary<string, long> EventsByResourceType,
    TimeSpan AverageDuration,
    double ErrorRate
);

public enum ExportFormat
{
    Csv,
    Json,
    Excel,
    Pdf,
    Parquet
}

public interface IDataRetentionService
{
    Task<DataRetentionPolicy> CreatePolicyAsync(CreateRetentionPolicyRequest request, string userId, CancellationToken ct = default);
    Task<DataRetentionPolicy?> GetPolicyAsync(Guid id, string userId, CancellationToken ct = default);
    Task<List<DataRetentionPolicy>> GetPoliciesAsync(string userId, RetentionPolicyFilter filter, CancellationToken ct = default);
    Task<DataRetentionPolicy> UpdatePolicyAsync(Guid id, UpdateRetentionPolicyRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeletePolicyAsync(Guid id, string userId, CancellationToken ct = default);
    Task<RetentionExecutionResult> ExecutePolicyAsync(Guid policyId, CancellationToken ct = default);
    Task<RetentionExecutionResult> ExecuteAllPoliciesAsync(string tenantId, CancellationToken ct = default);
    Task<List<RetentionExecutionResult>> GetExecutionHistoryAsync(Guid policyId, int page = 1, int pageSize = 20, CancellationToken ct = default);
}

public sealed record CreateRetentionPolicyRequest(
    string Name,
    string Description,
    string DataCategory,
    string DataType,
    int RetentionDays,
    int ArchiveAfterDays,
    RetentionAction ActionAfterRetention,
    string ArchiveLocation,
    List<string> ApplicableRegulations,
    List<string> ExemptedDataTypes,
    string LegalHoldTag,
    bool RequireLegalHoldApproval,
    string LegalHoldContact
);

public sealed record UpdateRetentionPolicyRequest(
    string? Name = null,
    string? Description = null,
    int? RetentionDays = null,
    int? ArchiveAfterDays = null,
    RetentionAction? ActionAfterRetention = null,
    string? ArchiveLocation = null,
    List<string>? ApplicableRegulations = null,
    List<string>? ExemptedDataTypes = null,
    string? LegalHoldTag = null,
    bool? RequireLegalHoldApproval = null,
    string? LegalHoldContact = null,
    bool? IsEnabled = null
);

public sealed record RetentionPolicyFilter(
    bool? IsEnabled = null,
    bool? IsDefault = null,
    string? DataCategory = null,
    string? DataType = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record RetentionExecutionResult(
    Guid PolicyId,
    DateTime ExecutedAt,
    long RecordsProcessed,
    long RecordsDeleted,
    long RecordsArchived,
    long RecordsAnonymized,
    long RecordsMovedToColdStorage,
    long RecordsOnLegalHold,
    TimeSpan Duration,
    RetentionExecutionStatus Status,
    string? ErrorMessage,
    List<string> AffectedTables,
    long FreedSpaceBytes
);

public interface IDataSubjectRequestService
{
    Task<DataSubjectRequest> CreateRequestAsync(CreateDataSubjectRequestRequest request, CancellationToken ct = default);
    Task<DataSubjectRequest?> GetRequestAsync(Guid id, string userId, CancellationToken ct = default);
    Task<List<DataSubjectRequest>> GetRequestsAsync(string userId, DataSubjectRequestFilter filter, CancellationToken ct = default);
    Task<DataSubjectRequest> UpdateRequestAsync(Guid id, UpdateDataSubjectRequestRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeleteRequestAsync(Guid id, string userId, CancellationToken ct = default);
    Task<DataSubjectRequest> ProcessRequestAsync(Guid id, ProcessDataSubjectRequestRequest request, string userId, CancellationToken ct = default);
    Task<DataSubjectRequest> VerifyIdentityAsync(Guid id, VerifyIdentityRequest request, CancellationToken ct = default);
    Task<DataSubjectRequest> AddAttachmentAsync(Guid requestId, DataSubjectRequestAttachment attachment, CancellationToken ct = default);
    Task<bool> RemoveAttachmentAsync(Guid requestId, Guid attachmentId, string userId, CancellationToken ct = default);
    Task<DataSubjectRequest> AddNoteAsync(Guid requestId, DataSubjectRequestNote note, string userId, CancellationToken ct = default);
    Task<DataSubjectRequest> VerifyIdentityAsync(Guid id, VerifyIdentityRequest request, CancellationToken ct = default);
}

public sealed record CreateDataSubjectRequestRequest(
    DataSubjectRequestType Type,
    string DataSubjectId,
    string DataSubjectEmail,
    string DataSubjectName,
    string LegalBasis,
    string Description,
    List<string> DataCategories,
    List<string> ProcessingPurposes,
    List<string> DataControllers,
    List<string> DataProcessors,
    List<string> ThirdCountries,
    List<string> Safeguards,
    bool RequiresIdentityVerification
);

public sealed record UpdateDataSubjectRequestRequest(
    DataSubjectRequestStatus? Status = null,
    string? LegalBasis = null,
    string? Description = null,
    List<string>? DataCategories = null,
    List<string>? ProcessingPurposes = null,
    List<string>? DataControllers = null,
    List<string>? DataProcessors = null,
    List<string>? ThirdCountries = null,
    List<string>? Safeguards = null,
    string? AssignedTo = null,
    string? AssignedToEmail = null,
    string? LegalHoldTag = null,
    string? EscalationContact = null
);

public sealed record DataSubjectRequestFilter(
    DataSubjectRequestType? Type = null,
    DataSubjectRequestStatus? Status = null,
    string? DataSubjectId = null,
    string? DataSubjectEmail = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    bool? IsVerified = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record ProcessDataSubjectRequestRequest(
    DataSubjectRequestStatus Status,
    string? RejectionReason = null,
    string? RejectionDetails = null,
    string? CompletionDetails = null,
    string? OutputPath = null
);

public sealed record VerifyIdentityRequest(
    string Method,
    string VerificationToken,
    string VerifiedBy
);

public interface IDataBreachService
{
    Task<DataBreachNotification> CreateNotificationAsync(CreateDataBreachNotificationRequest request, string userId, CancellationToken ct = default);
    Task<DataBreachNotification?> GetNotificationAsync(Guid id, string userId, CancellationToken ct = default);
    Task<List<DataBreachNotification>> GetNotificationsAsync(string userId, DataBreachFilter filter, CancellationToken ct = default);
    Task<DataBreachNotification> UpdateNotificationAsync(Guid id, UpdateDataBreachNotificationRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeleteNotificationAsync(Guid id, string userId, CancellationToken ct = default);
    Task<DataBreachNotification> NotifyAuthoritiesAsync(Guid id, NotifyAuthoritiesRequest request, string userId, CancellationToken ct = default);
    Task<DataBreachNotification> NotifyDataSubjectsAsync(Guid id, NotifyDataSubjectsRequest request, string userId, CancellationToken ct = default);
}

public sealed record CreateDataBreachNotificationRequest(
    string Title,
    string Description,
    BreachSeverity Severity,
    List<string> AffectedDataCategories,
    List<string> AffectedDataSubjects,
    long EstimatedAffectedRecords,
    List<string> AffectedSystems,
    List<string> AffectedDataTypes,
    string RootCause,
    string ImpactAssessment,
    string RiskAssessment,
    string ContainmentActions,
    string RemediationActions,
    string MitigationMeasures,
    string PreventionMeasures,
    List<DataBreachAttachment> Attachments
);

public sealed record UpdateDataBreachNotificationRequest(
    BreachSeverity? Severity = null,
    BreachStatus? Status = null,
    string? Title = null,
    string? Description = null,
    string? RootCause = null,
    string? ImpactAssessment = null,
    string? RiskAssessment = null,
    string? ContainmentActions = null,
    string? RemediationActions = null,
    string? MitigationMeasures = null,
    string? PreventionMeasures = null,
    string? NotificationToAuthorities = null,
    string? AuthorityReference = null,
    string? NotificationToDataSubjects = null,
    string? MitigationMeasures = null,
    string? PreventionMeasures = null,
    List<DataBreachAttachment>? Attachments = null
);

public sealed record DataBreachFilter(
    BreachSeverity? Severity = null,
    BreachStatus? Status = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? TenantId = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record NotifyAuthoritiesRequest(
    string AuthorityName,
    string AuthorityContact,
    string NotificationMethod,
    string NotificationDetails
);

public sealed record NotifyDataSubjectsRequest(
    string NotificationMethod,
    string NotificationTemplate,
    List<string> DataSubjectEmails,
    string CustomMessage
);

public interface IComplianceService
{
    Task<ComplianceFramework> CreateFrameworkAsync(CreateFrameworkRequest request, string userId, CancellationToken ct = default);
    Task<ComplianceFramework?> GetFrameworkAsync(Guid id, string userId, CancellationToken ct = default);
    Task<List<ComplianceFramework>> GetFrameworksAsync(string userId, FrameworkFilter filter, CancellationToken ct = default);
    Task<ComplianceFramework> UpdateFrameworkAsync(Guid id, UpdateFrameworkRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeleteFrameworkAsync(Guid id, string userId, CancellationToken ct = default);
    Task<ComplianceAssessment> AssessAsync(Guid frameworkId, string userId, CancellationToken ct = default);
    Task<ComplianceReport> GenerateReportAsync(Guid frameworkId, CancellationToken ct = default);
    Task<CompliancePolicy> CreatePolicyAsync(CreatePolicyRequest request, string userId, CancellationToken ct = default);
    Task<CompliancePolicy?> GetPolicyAsync(Guid id, string userId, CancellationToken ct = default);
    Task<List<CompliancePolicy>> GetPoliciesAsync(string userId, PolicyFilter filter, CancellationToken ct = default);
    Task<CompliancePolicy> UpdatePolicyAsync(Guid id, UpdatePolicyRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeletePolicyAsync(Guid id, string userId, CancellationToken ct = default);
    Task<PolicyEvaluationResult> EvaluatePolicyAsync(Guid policyId, string userId, CancellationToken ct = default);
    Task<List<PolicyViolation>> GetViolationsAsync(string userId, ViolationFilter filter, CancellationToken ct = default);
}

public sealed record CreateFrameworkRequest(
    string Name,
    string Code,
    string Description,
    string Version,
    string Url,
    FrameworkType Type,
    DateTime EffectiveDate,
    DateTime? ExpiryDate,
    List<ComplianceRequirement> Requirements,
    List<ComplianceControl> Controls,
    List<CompliancePolicy> Policies
);

public sealed record UpdateFrameworkRequest(
    string? Name = null,
    string? Description = null,
    string? Version = null,
    string? Url = null,
    FrameworkStatus? Status = null,
    DateTime? ExpiryDate = null,
    List<ComplianceRequirement>? Requirements = null,
    List<ComplianceControl>? Controls = null,
    List<CompliancePolicy>? Policies = null,
    string? OwnerId = null,
    string? OwnerEmail = null
);

public sealed record FrameworkFilter(
    FrameworkType? Type = null,
    FrameworkStatus? Status = null,
    bool? IsActive = null,
    bool? IsMandatory = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record ComplianceAssessment(
    Guid FrameworkId,
    string FrameworkName,
    DateTime AssessedAt,
    double OverallScore,
    double CompliancePercentage,
    int TotalRequirements,
    int CompliantRequirements,
    int NonCompliantRequirements,
    int PartiallyCompliantRequirements,
    int NotApplicableRequirements,
    List<RequirementAssessment> RequirementAssessments,
    List<ControlAssessment> ControlAssessments,
    List<PolicyViolation> Violations,
    List<string> Recommendations,
    TimeSpan AssessmentDuration
);

public sealed record RequirementAssessment(
    Guid RequirementId,
    string RequirementTitle,
    AssessmentStatus Status,
    double Score,
    string Evidence,
    string Notes,
    List<string> Gaps,
    List<string> Recommendations
);

public sealed class ControlAssessment
{
    public Guid ControlId { get; set; }
    public string ControlName { get; set; } = string.Empty;
    public AssessmentStatus Status { get; set; }
    public double Score { get; set; }
    public string Evidence { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime AssessedAt { get; set; }
    public string AssessedBy { get; set; } = string.Empty;
}

public sealed record ComplianceReport(
    Guid FrameworkId,
    string FrameworkName,
    DateTime GeneratedAt,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    double OverallScore,
    double CompliancePercentage,
    int TotalRequirements,
    int CompliantRequirements,
    int NonCompliantRequirements,
    int PartiallyCompliantRequirements,
    int NotApplicableRequirements,
    List<RequirementAssessment> RequirementAssessments,
    List<ControlAssessment> ControlAssessments,
    List<PolicyViolation> Violations,
    List<string> Recommendations,
    TimeSpan GenerationTime
);

public sealed record CreatePolicyRequest(
    string Name,
    string Description,
    PolicyType Type,
    PolicyScope Scope,
    string ContentJson,
    bool IsEnforced,
    EnforcementLevel Enforcement,
    List<string> ApplicableRoles,
    List<string> ExemptRoles,
    List<string> ApplicableResources,
    List<string> ExemptResources,
    DateTime EffectiveDate,
    DateTime? ExpiryDate,
    List<PolicyRule> Rules
);

public sealed record UpdatePolicyRequest(
    string? Name = null,
    string? Description = null,
    PolicyType? Type = null,
    PolicyScope? Scope = null,
    string? ContentJson = null,
    bool? IsEnforced = null,
    EnforcementLevel? Enforcement = null,
    List<string>? ApplicableRoles = null,
    List<string>? ExemptRoles = null,
    List<string>? ApplicableResources = null,
    List<string>? ExemptResources = null,
    DateTime? EffectiveDate = null,
    DateTime? ExpiryDate = null,
    List<PolicyRule>? Rules = null,
    bool? IsActive = null
);

public sealed record PolicyFilter(
    PolicyType? Type = null,
    PolicyScope? Scope = null,
    bool? IsEnforced = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record PolicyRule
{
    public string RuleId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ViolationSeverity Severity { get; set; } = ViolationSeverity.Medium;
    public string Condition { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
}

public sealed record PolicyEvaluationResult(
    bool IsCompliant,
    List<PolicyViolation> Violations,
    double ComplianceScore,
    List<string> Recommendations
);

public sealed record PolicyViolation
{
    public string PolicyId { get; set; } = string.Empty;
    public string PolicyName { get; set; } = string.Empty;
    public string RuleId { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public ViolationSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty.
    public string Remediation { get; set; } = string.Empty.
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow.
    public string DetectedBy { get; set; } = string.Empty.
    public string Status { get; set; } = "Open".
    public string RemediationStatus { get; set; } = "Open".
    public DateTime? ResolvedAt { get; set; }.
    public string ResolvedBy { get; set; } = string.Empty.
}

public enum ViolationSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public sealed record ViolationFilter(
    ViolationSeverity? Severity = null,
    string? PolicyId = null,
    string? ResourceId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20
);

public interface IAdvancedRbacService
{
    Task<Permission> CreatePermissionAsync(CreatePermissionRequest request, string userId, CancellationToken ct = default);
    Task<Permission?> GetPermissionAsync(Guid id, CancellationToken ct = default);
    Task<List<Permission>> GetPermissionsAsync(PermissionFilter filter, CancellationToken ct = default);
    Task<Permission> UpdatePermissionAsync(Guid id, UpdatePermissionRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeletePermissionAsync(Guid id, CancellationToken ct = default);
    
    Task<Role> CreateRoleAsync(CreateRoleRequest request, string userId, CancellationToken ct = default);
    Task<Role?> GetRoleAsync(Guid id, CancellationToken ct = default);
    Task<List<Role>> GetRolesAsync(RoleFilter filter, CancellationToken ct = default);
    Task<Role> UpdateRoleAsync(Guid id, UpdateRoleRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeleteRoleAsync(Guid id, string userId, CancellationToken ct = default);
    Task<Role> AddPermissionToRoleAsync(Guid roleId, Guid permissionId, string userId, CancellationToken ct = default);
    Task<Role> RemovePermissionFromRoleAsync(Guid roleId, Guid permissionId, string userId, CancellationToken ct = default);
    
    Task<PermissionGrant> GrantPermissionAsync(GrantPermissionRequest request, string userId, CancellationToken ct = default);
    Task<PermissionGrant?> GetGrantAsync(Guid id, CancellationToken ct = default);
    Task<List<PermissionGrant>> GetGrantsAsync(GrantFilter filter, CancellationToken ct = default);
    Task<PermissionGrant> UpdateGrantAsync(Guid id, UpdateGrantRequest request, string userId, CancellationToken ct = default);
    Task<bool> RevokePermissionAsync(Guid id, string userId, CancellationToken ct = default);
    Task<bool> RevokeAllPermissionsForPrincipalAsync(string principalId, PrincipalType principalType, string userId, CancellationToken ct = default);
    
    Task<AccessControlRule> CreateAccessRuleAsync(CreateAccessRuleRequest request, string userId, CancellationToken ct = default);
    Task<AccessControlRule?> GetAccessRuleAsync(Guid id, CancellationToken ct = default);
    Task<List<AccessControlRule>> GetAccessRulesAsync(AccessRuleFilter filter, CancellationToken ct = default);
    Task<AccessControlRule> UpdateAccessRuleAsync(Guid id, UpdateAccessRuleRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeleteAccessRuleAsync(Guid id, string userId, CancellationToken ct = default);
    Task<AccessDecision> EvaluateAccessAsync(AccessEvaluationRequest request, CancellationToken ct = default);
}

public sealed record CreatePermissionRequest(
    string Name,
    string DisplayName,
    string Description,
    string Category,
    string ResourceType,
    string Action,
    string Scope,
    string Condition
);

public sealed record UpdatePermissionRequest(
    string? DisplayName = null,
    string? Description = null,
    string? Category = null,
    string? ResourceType = null,
    string? Action = null,
    string? Scope = null,
    string? Condition = null
);

public sealed record PermissionFilter(
    string? Category = null,
    string? ResourceType = null,
    string? Action = null,
    bool? IsSystemPermission = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record CreateRoleRequest(
    string Name,
    string DisplayName,
    string Description,
    RoleType Type,
    Guid? ParentRoleId,
    List<Guid> PermissionIds,
    RoleConstraints Constraints,
    RoleMetadata Metadata
);

public sealed record UpdateRoleRequest(
    string? DisplayName = null,
    string? Description = null,
    bool? IsActive = null,
    RoleType? Type = null,
    Guid? ParentRoleId = null,
    List<Guid>? PermissionIds = null,
    RoleConstraints? Constraints = null,
    RoleMetadata? Metadata = null
);

public sealed record RoleFilter(
    RoleType? Type = null,
    bool? IsActive = null,
    bool? IsSystemRole = null,
    Guid? ParentRoleId = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record GrantPermissionRequest(
    string PrincipalId,
    PrincipalType PrincipalType,
    Guid PermissionId,
    Guid? ResourceId = null,
    string? ResourceType = null,
    string? ResourceName = null,
    string? Condition = null,
    DateTime? ExpiresAt = null,
    string Justification = "",
    string ApprovalTicket = ""
);

public sealed record UpdateGrantRequest(
    DateTime? ExpiresAt = null,
    GrantStatus? Status = null,
    string? Condition = null
);

public sealed record GrantFilter(
    PrincipalType? PrincipalType = null,
    string? PrincipalId = null,
    Guid? PermissionId = null,
    Guid? ResourceId = null,
    string? ResourceType = null,
    GrantStatus? Status = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record UpdateGrantRequest(
    DateTime? ExpiresAt = null,
    GrantStatus? Status = null,
    string? Condition = null
);

public sealed record CreateAccessRuleRequest(
    string Name,
    string Description,
    AccessRuleType Type,
    int Priority,
    AccessRuleCondition Condition,
    AccessRuleAction Action,
    List<string> ApplicableRoles,
    List<string> ExcludedRoles,
    List<string> ApplicableUsers,
    List<string> ExcludedUsers,
    List<string> ApplicableResources,
    List<string> ExcludedResources,
    List<string> ApplicableActions,
    List<string> ExcludedActions,
    TimeSpan? TimeWindowStart,
    TimeSpan? TimeWindowEnd,
    List<DayOfWeek> ApplicableDays,
    List<string> ApplicableLocations,
    List<string> ExcludedLocations
);

public sealed record UpdateAccessRuleRequest(
    string? Name = null,
    string? Description = null,
    AccessRuleType? Type = null,
    int? Priority = null,
    AccessRuleCondition? Condition = null,
    AccessRuleAction? Action = null,
    List<string>? ApplicableRoles = null,
    List<string>? ExcludedRoles = null,
    List<string>? ApplicableUsers = null,
    List<string>? ExcludedUsers = null,
    List<string>? ApplicableResources = null,
    List<string>? ExcludedResources = null,
    List<string>? ApplicableActions = null,
    List<string>? ExcludedActions = null,
    TimeSpan? TimeWindowStart = null,
    TimeSpan? TimeWindowEnd = null,
    List<DayOfWeek>? ApplicableDays = null,
    List<string>? ApplicableLocations = null,
    List<string>? ExcludedLocations = null,
    int? Priority = null,
    bool? IsActive = null
);

public sealed record AccessRuleFilter(
    AccessRuleType? Type = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record UpdateAccessRuleRequest(
    string? Name = null,
    string? Description = null,
    AccessRuleType? Type = null,
    int? Priority = null,
    AccessRuleCondition? Condition = null,
    AccessRuleAction? Action = null,
    List<string>? ApplicableRoles = null,
    List<string>? ExcludedRoles = null,
    List<string>? ApplicableUsers = null,
    List<string>? ExcludedUsers = null,
    List<string>? ApplicableResources = null,
    List<string>? ExcludedResources = null,
    List<string>? ApplicableActions = null,
    List<string>? ExcludedActions = null,
    TimeSpan? TimeWindowStart = null,
    TimeSpan? TimeWindowEnd = null,
    List<DayOfWeek>? ApplicableDays = null,
    List<string>? ApplicableLocations = null,
    List<string>? ExcludedLocations = null,
    int? Priority = null,
    bool? IsActive = null
);

public sealed record AccessEvaluationRequest(
    string PrincipalId,
    PrincipalType PrincipalType,
    string ResourceId,
    string ResourceType,
    string Action,
    Dictionary<string, object> Context
);

public sealed record AccessDecision(
    bool Allowed,
    string DecisionReason,
    List<AccessControlRule> MatchedRules,
    List<string> MissingPermissions,
    List<string> ViolatedPolicies
);

public interface IDataGovernanceService
{
    Task<DataClassification> CreateClassificationAsync(CreateClassificationRequest request, string userId, CancellationToken ct = default);
    Task<DataClassification?> GetClassificationAsync(Guid id, CancellationToken ct = default);
    Task<List<DataClassification>> GetClassificationsAsync(ClassificationFilter filter, CancellationToken ct = default);
    Task<DataClassification> UpdateClassificationAsync(Guid id, UpdateClassificationRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeleteClassificationAsync(Guid id, CancellationToken ct = default);
    
    Task<DataLineage> CreateLineageAsync(CreateLineageRequest request, string userId, CancellationToken ct = default);
    Task<DataLineage?> GetLineageAsync(Guid id, CancellationToken ct = default);
    Task<List<DataLineage>> GetLineagesAsync(LineageFilter filter, CancellationToken ct = default);
    Task<DataLineage> UpdateLineageAsync(Guid id, UpdateLineageRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeleteLineageAsync(Guid id, CancellationToken ct = default);
    Task<DataLineageGraph> GetLineageGraphAsync(string entityId, int depth = 3, CancellationToken ct = default);
    Task<DataQualityReport> AssessQualityAsync(string entityId, CancellationToken ct = default);
    Task<DataQualityReport> ProfileDataAsync(string dataSourceId, string tableName, CancellationToken ct = default);
}

public sealed record CreateClassificationRequest(
    string Name,
    string Description,
    ClassificationLevel Level,
    string Color,
    string Icon,
    List<string> Keywords,
    List<string> Patterns,
    List<string> Examples,
    bool IsPii,
    bool IsPhi,
    bool IsPci,
    bool IsSensitive,
    List<string> ApplicableRegulations,
    List<string> HandlingRequirements,
    List<string> ProhibitedActions
);

public sealed record UpdateClassificationRequest(
    string? Name = null,
    string? Description = null,
    ClassificationLevel? Level = null,
    string? Color = null,
    string? Icon = null,
    List<string>? Keywords = null,
    List<string>? Patterns = null,
    List<string>? Examples = null,
    bool? IsPii = null,
    bool? IsPhi = null,
    bool? IsPci = null,
    bool? IsSensitive = null,
    List<string>? ApplicableRegulations = null,
    List<string>? HandlingRequirements = null,
    List<string>? ProhibitedActions = null,
    bool? IsActive = null
);

public sealed record ClassificationFilter(
    ClassificationLevel? Level = null,
    bool? IsPii = null,
    bool? IsPhi = null,
    bool? IsPci = null,
    bool? IsSensitive = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record UpdateClassificationRequest(
    string? Name = null,
    string? Description = null,
    ClassificationLevel? Level = null,
    string? Color = null,
    string? Icon = null,
    List<string>? Keywords = null,
    List<string>? Patterns = null,
    List<string>? Examples = null,
    bool? IsPii = null,
    bool? IsPhi = null,
    bool? IsPci = null,
    bool? IsSensitive = null,
    List<string>? ApplicableRegulations = null,
    List<string>? HandlingRequirements = null,
    List<string>? ProhibitedActions = null,
    bool? IsActive = null
);

public sealed record CreateLineageRequest(
    string SourceSystem,
    string SourceEntity,
    string SourceField,
    string TargetSystem,
    string TargetEntity,
    string TargetField,
    TransformationType Transformation,
    string TransformationLogic,
    string TransformationTool
);

public sealed record UpdateLineageRequest(
    TransformationType? Transformation = null,
    string? TransformationLogic = null,
    string? TransformationTool = null,
    bool? IsActive = null
);

public sealed record LineageFilter(
    string? SourceSystem = null,
    string? TargetSystem = null,
    TransformationType? Transformation = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 20
);

public sealed class DataLineageGraph
{
    public string RootEntityId { get; set; } = string.Empty;
    public List<DataLineageNode> Nodes { get; set; } = new();
    public List<DataLineageEdge> Edges { get; set; } = new();
    public int Depth { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public sealed class DataLineageNode
{
    public string EntityId { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string System { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DataQualityMetrics? Quality { get; set; }
    public ClassificationLevel Classification { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class DataLineageEdge
{
    public string SourceId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public TransformationType Transformation { get; set; }
    public string Logic { get; set; } = string.Empty;
    public double Weight { get; set; } = 1.0;
}

public sealed class DataQualityReport
{
    public string EntityId { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty.
    public DateTime AssessedAt { get; set; } = DateTime.UtcNow.
    public DataQualityMetrics OverallMetrics { get; set; } = new().
    public List<FieldQualityIssue> Issues { get; set; } = new().
    public List<string> Recommendations { get; set; } = new().
    public Dictionary<string, double> FieldScores { get; set; } = new().
}

public sealed class FieldQualityIssue
{
    public string FieldName { get; set; } = string.Empty.
    public string IssueType { get; set; } = string.Empty.
    public string Description { get; set; } = string.Empty.
    public double Severity { get; set; } = 0.
    public long AffectedRecords { get; set; } = 0.
    public string Recommendation { get; set; } = string.Empty.
}

public sealed class LineageFilter
{
    string? SourceSystem = null;
    string? TargetSystem = null;
    TransformationType? Transformation = null;
    bool? IsActive = null;
    int Page = 1;
    int PageSize = 20;
}

public interface IRiskManagementService
{
    Task<RiskAssessment> CreateRiskAssessmentAsync(CreateRiskAssessmentRequest request, string userId, CancellationToken ct = default);
    Task<RiskAssessment?> GetRiskAssessmentAsync(Guid id, string userId, CancellationToken ct = default);
    Task<List<RiskAssessment>> GetRiskAssessmentsAsync(string userId, RiskFilter filter, CancellationToken ct = default);
    Task<RiskAssessment> UpdateRiskAssessmentAsync(Guid id, UpdateRiskAssessmentRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeleteRiskAssessmentAsync(Guid id, string userId, CancellationToken ct = default);
    Task<RiskAssessment> AssessRiskAsync(Guid id, AssessRiskRequest request, string userId, CancellationToken ct = default);
    Task<List<MitigationAction>> GetMitigationsAsync(Guid riskId, CancellationToken ct = default);
    Task<MitigationAction> AddMitigationAsync(Guid riskId, CreateMitigationRequest request, string userId, CancellationToken ct = default);
    Task<MitigationAction> UpdateMitigationAsync(Guid mitigationId, UpdateMitigationRequest request, string userId, CancellationToken ct = default);
    Task<bool> CompleteMitigationAsync(Guid mitigationId, string userId, CancellationToken ct = default);
}

public sealed record CreateRiskAssessmentRequest(
    string Name,
    string Description,
    RiskCategory Category,
    RiskLikelihood Likelihood,
    RiskImpact Impact,
    string OwnerId,
    string OwnerEmail,
    string TenantId,
    List<RiskFactor> Factors,
    List<MitigationAction> Mitigations
);

public sealed record UpdateRiskAssessmentRequest(
    string? Name = null,
    string? Description = null,
    RiskCategory? Category = null,
    RiskLikelihood? Likelihood = null,
    RiskImpact? Impact = null,
    RiskRating? Rating = null,
    double? RiskScore = null,
    string? OwnerId = null,
    string? OwnerEmail = null,
    List<RiskFactor>? Factors = null,
    List<MitigationAction>? Mitigations = null,
    RiskStatus? Status = null,
    DateTime? NextReviewAt = null
);

public sealed record RiskFilter(
    RiskCategory? Category = null,
    RiskLikelihood? Likelihood = null,
    RiskImpact? Impact = null,
    RiskRating? Rating = null,
    RiskStatus? Status = null,
    string? OwnerId = null,
    string? TenantId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record AssessRiskRequest(
    RiskLikelihood Likelihood,
    RiskImpact Impact,
    List<RiskFactor> Factors
);

public sealed record CreateMitigationRequest(
    string Name,
    string Description,
    MitigationType Type,
    string OwnerId,
    string OwnerEmail,
    DateTime DueDate,
    List<string> RisksAddressed,
    Dictionary<string, object> Metadata
);

public sealed record UpdateMitigationRequest(
    string? Name = null,
    string? Description = null,
    MitigationType? Type = null,
    string? OwnerId = null,
    string? OwnerEmail = null,
    DateTime? DueDate = null,
    MitigationStatus? Status = null,
    double? Effectiveness = null,
    double? Cost = null,
    string? Currency = null,
    List<string>? Dependencies = null,
    List<string>? RisksAddressed = null
);

public interface IIncidentResponseService
{
    Task<SecurityIncident> CreateIncidentAsync(CreateIncidentRequest request, string userId, CancellationToken ct = default);
    Task<SecurityIncident?> GetIncidentAsync(Guid id, string userId, CancellationToken ct = default);
    Task<List<SecurityIncident>> GetIncidentsAsync(string userId, IncidentFilter filter, CancellationToken ct = default);
    Task<SecurityIncident> UpdateIncidentAsync(Guid id, UpdateIncidentRequest request, string userId, CancellationToken ct = default);
    Task<SecurityIncident> UpdateIncidentStatusAsync(Guid id, IncidentStatus status, string userId, CancellationToken ct = default);
    Task<SecurityIncident> AddTimelineEventAsync(Guid id, IncidentTimelineEvent request, string userId, CancellationToken ct = default);
    Task<SecurityIncident> AddActionAsync(Guid id, IncidentAction request, string userId, CancellationToken ct = default);
    Task<SecurityIncident> AddEvidenceAsync(Guid id, IncidentEvidence request, string userId, CancellationToken ct = default);
    Task<SecurityIncident> AddCommunicationAsync(Guid id, IncidentCommunication request, string userId, CancellationToken ct = default);
    Task<SecurityIncident> AddLessonAsync(Guid id, IncidentLesson request, string userId, CancellationToken ct = default);
    Task<SecurityIncident> ResolveIncidentAsync(Guid id, ResolveIncidentRequest request, string userId, CancellationToken ct = default);
    Task<SecurityIncident> CloseIncidentAsync(Guid id, CloseIncidentRequest request, string userId, CancellationToken ct = default);
}

public sealed record CreateIncidentRequest(
    string Title,
    string Description,
    IncidentSeverity Severity,
    IncidentType Type,
    string TenantId,
    string ReporterId,
    string ReporterEmail,
    string AssignedToId,
    string AssignedToEmail
);

public sealed record UpdateIncidentRequest(
    string? Title = null,
    string? Description = null,
    IncidentSeverity? Severity = null,
    IncidentStatus? Status = null,
    IncidentType? Type = null,
    string? AssignedToId = null,
    string? AssignedToEmail = null,
    string? RootCause = null,
    string? ImpactAssessment = null,
    List<string>? AffectedSystems = null,
    List<string>? AffectedUsers = null,
    List<string>? AffectedDataTypes = null,
    List<string>? AttackVectors = null,
    List<string>? IndicatorsOfCompromise = null
);

public sealed record IncidentFilter(
    IncidentSeverity? Severity = null,
    IncidentStatus? Status = null,
    IncidentType? Type = null,
    string? AssignedToId = null,
    string? TenantId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record ResolveIncidentRequest(
    string RootCause,
    string RemediationSummary,
    string PreventionMeasures,
    IncidentSeverity FinalSeverity
);

public sealed record CloseIncidentRequest(
    string ClosureReason,
    string LessonsLearned
);

public sealed record UpdateMitigationRequest(
    string? Name = null,
    string? Description = null,
    MitigationType? Type = null,
    string? OwnerId = null,
    string? OwnerEmail = null,
    DateTime? DueDate = null,
    MitigationStatus? Status = null,
    double? Effectiveness = null,
    double? Cost = null,
    string? Currency = null,
    List<string>? Dependencies = null,
    List<string>? RisksAddressed = null
);

public interface IDataSubjectRequestService
{
    Task<DataSubjectRequest> CreateRequestAsync(CreateDataSubjectRequestRequest request, CancellationToken ct = default);
    Task<DataSubjectRequest?> GetRequestAsync(Guid id, string userId, CancellationToken ct = default);
    Task<List<DataSubjectRequest>> GetRequestsAsync(string userId, DataSubjectRequestFilter filter, CancellationToken ct = default);
    Task<DataSubjectRequest> UpdateRequestAsync(Guid id, UpdateDataSubjectRequestRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeleteRequestAsync(Guid id, string userId, CancellationToken ct = default);
    Task<DataSubjectRequest> ProcessRequestAsync(Guid id, ProcessDataSubjectRequestRequest request, string userId, CancellationToken ct = default);
    Task<DataSubjectRequest> VerifyIdentityAsync(Guid id, VerifyIdentityRequest request, CancellationToken ct = default);
}

public sealed record CreateDataSubjectRequestRequest(
    DataSubjectRequestType Type,
    string DataSubjectId,
    string DataSubjectEmail,
    string DataSubjectName,
    string LegalBasis,
    string Description,
    List<string> DataCategories,
    List<string> ProcessingPurposes,
    List<string> DataControllers,
    List<string> DataProcessors,
    List<string> ThirdCountries,
    List<string> Safeguards
);

public sealed record UpdateDataSubjectRequestRequest(
    DataSubjectRequestStatus? Status = null,
    string? LegalBasis = null,
    string? Description = null,
    List<string>? DataCategories = null,
    List<string>? ProcessingPurposes = null,
    List<string>? DataControllers = null,
    List<string>? DataProcessors = null,
    List<string>? ThirdCountries = null,
    List<string>? Safeguards = null,
    string? AssignedTo = null,
    string? AssignedToEmail = null,
    string? LegalHoldTag = null,
    string? EscalationContact = null
);

public sealed record DataSubjectRequestFilter(
    DataSubjectRequestType? Type = null,
    DataSubjectRequestStatus? Status = null,
    string? DataSubjectId = null,
    string? DataSubjectEmail = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    bool? IsVerified = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record ProcessDataSubjectRequestRequest(
    DataSubjectRequestStatus Status,
    string? RejectionReason = null,
    string? RejectionDetails = null,
    string? CompletionDetails = null,
    string? OutputPath = null
);

public sealed record VerifyIdentityRequest(
    string Method,
    string VerificationToken,
    string VerifiedBy
);

public interface IDataBreachService
{
    Task<DataBreachNotification> CreateNotificationAsync(CreateDataBreachNotificationRequest request, string userId, CancellationToken ct = default);
    Task<DataBreachNotification?> GetNotificationAsync(Guid id, string userId, CancellationToken ct = default);
    Task<List<DataBreachNotification>> GetNotificationsAsync(string userId, DataBreachFilter filter, CancellationToken ct = default);
    Task<DataBreachNotification> UpdateNotificationAsync(Guid id, UpdateDataBreachNotificationRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeleteNotificationAsync(Guid id, string userId, CancellationToken ct = default);
    Task<DataBreachNotification> NotifyAuthoritiesAsync(Guid id, NotifyAuthoritiesRequest request, string userId, CancellationToken ct = default);
    Task<DataBreachNotification> NotifyDataSubjectsAsync(Guid id, NotifyDataSubjectsRequest request, string userId, CancellationToken ct = default);
}

public sealed record CreateDataBreachNotificationRequest(
    string Title,
    string Description,
    BreachSeverity Severity,
    List<string> AffectedDataCategories,
    List<string> AffectedDataSubjects,
    long EstimatedAffectedRecords,
    List<string> AffectedSystems,
    List<string> AffectedDataTypes,
    string RootCause,
    string ImpactAssessment,
    string RiskAssessment,
    string ContainmentActions,
    string RemediationActions,
    string MitigationMeasures,
    string PreventionMeasures,
    List<DataBreachAttachment> Attachments
);

public sealed record UpdateDataBreachNotificationRequest(
    BreachSeverity? Severity = null,
    BreachStatus? Status = null,
    string? Title = null,
    string? Description = null,
    string? RootCause = null,
    string? ImpactAssessment = null,
    string? RiskAssessment = null,
    string? ContainmentActions = null,
    string? RemediationActions = null,
    string? MitigationMeasures = null,
    string? PreventionMeasures = null,
    List<DataBreachAttachment>? Attachments = null
);

public sealed record DataBreachFilter(
    BreachSeverity? Severity = null,
    BreachStatus? Status = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? TenantId = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record NotifyAuthoritiesRequest(
    string AuthorityName,
    string AuthorityContact,
    string NotificationMethod,
    string NotificationDetails
);

public sealed record NotifyDataSubjectsRequest(
    string NotificationMethod,
    string NotificationTemplate,
    List<string> DataSubjectEmails,
    string CustomMessage
);