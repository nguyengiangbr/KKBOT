// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace KKBOT.Enterprise.Models;

#region SSO / Identity Provider Models

/// <summary>
/// Identity Provider configuration for SSO
/// </summary>
public sealed class IdentityProvider
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public IdentityProviderType Type { get; set; } = IdentityProviderType.OIDC;
    public bool IsEnabled { get; set; } = true;
    public bool IsDefault { get; set; } = false;
    public int Priority { get; set; } = 0;
    public string TenantId { get; set; } = string.Empty;
    public string IssuerUri { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty; // Encrypted
    public string CallbackPath { get; set; } = "/signin-oidc";
    public string SignedOutCallbackPath { get; set; } = "/signout-callback-oidc";
    public string Scopes { get; set; } = "openid profile email";
    public string ResponseType { get; set; } = "code";
    public string ResponseMode { get; set; } = "query";
    public bool UsePkce { get; set; } = true;
    public string MetadataUrl { get; set; } = string.Empty;
    public string Authority { get; set; } = string.Empty;
    public string SigningAlgorithm { get; set; } = "RS256";
    public string CertificateThumbprint { get; set; } = string.Empty;
    public string PublicKeyXml { get; set; } = string.Empty;
    public Dictionary<string, string> ClaimMapping { get; set; } = new();
    public bool AutoProvisionUsers { get; set; } = true;
    public string DefaultRole { get; set; } = "Viewer";
    public Dictionary<string, string> RoleMapping { get; set; } = new();
    public bool AllowIdpInitiatedLogin { get; set; } = true;
    public bool AllowIdpInitiatedLogout { get; set; } = true;
    public string SloUrl { get; set; } = string.Empty;
    public int TokenLifetimeMinutes { get; set; } = 60;
    public int RefreshTokenLifetimeDays { get; set; } = 30;
    public bool RequireMfa { get; set; } = false;
    public List<string> AllowedDomains { get; set; } = new();
    public List<string> BlockedDomains { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public string UpdatedBy { get; set; } = string.Empty;
    public bool IsDeleted { get; set; } = false;
}

public enum IdentityProviderType
{
    OIDC,
    SAML2,
    WS_Federation,
    AzureAD,
    Google,
    Microsoft,
    GitHub,
    GitLab,
    Okta,
    Auth0,
    Keycloak,
    Custom
}

/// <summary>
/// SAML2 specific configuration
/// </summary>
public sealed class Saml2Configuration
{
    public string EntityId { get; set; } = string.Empty;
    public string AssertionConsumerServiceUrl { get; set; } = string.Empty;
    public string SingleLogoutServiceUrl { get; set; } = string.Empty;
    public string IdpMetadataUrl { get; set; } = string.Empty;
    public string IdpEntityId { get; set; } = string.Empty;
    public string IdpSsoUrl { get; set; } = string.Empty;
    public string IdpSloUrl { get; set; } = string.Empty;
    public string SigningCertificate { get; set; } = string.Empty;
    public string EncryptionCertificate { get; set; } = string.Empty;
    public bool WantAssertionsSigned { get; set; } = true;
    public bool WantAuthnRequestsSigned { get; set; } = true;
    public bool WantMessagesSigned { get; set; } = true;
    public string SignatureAlgorithm { get; set; } = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
    public string DigestAlgorithm { get; set; } = "http://www.w3.org/2001/04/xmlenc#sha256";
    public bool ForceAuthn { get; set; } = false;
    public bool IsPassive { get; set; } = false;
    public string NameIdFormat { get; set; } = "urn:oasis:names:tc:SAML:2.0:nameid-format:transient";
    public Dictionary<string, string> AttributeMapping { get; set; } = new();
    public string NameIdPolicyFormat { get; set; } = string.Empty;
    public bool AllowCreate { get; set; } = true;
    public int SessionLifetimeMinutes { get; set; } = 480;
    public bool RequireSignedAssertions { get; set; } = true;
    public bool RequireSignedAuthnRequests { get; set; } = true;
    public bool RequireSignedLogoutRequests { get; set; } = true;
    public bool RequireSignedLogoutResponses { get; set; } = true;
    public int MaxAssertionTimeDriftSeconds { get; set; } = 300;
    public string MetadataUrl { get; set; } = string.Empty;
    public string SpEntityId { get; set; } = string.Empty;
    public string SpAssertionConsumerServiceUrl { get; set; } = string.Empty;
    public string SpSingleLogoutServiceUrl { get; set; } = string.Empty;
    public string SpSigningCertificate { get; set; } = string.Empty;
    public string SpEncryptionCertificate { get; set; } = string.Empty;
}

#endregion

#region Audit & Compliance Models

/// <summary>
/// Comprehensive audit log entry
/// </summary>
public sealed class AuditLogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string UserIp { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
    public string SpanId { get; set; } = string.Empty;
    public AuditEventType EventType { get; set; }
    public string EventCategory { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public AuditOutcome Outcome { get; set; } = AuditOutcome.Success;
    public string ErrorMessage { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public int HttpStatusCode { get; set; }
    public string HttpMethod { get; set; } = string.Empty;
    public string RequestPath { get; set; } = string.Empty;
    public string RequestQueryString { get; set; } = string.Empty;
    public long RequestSizeBytes { get; set; }
    public long ResponseSizeBytes { get; set; }
    public TimeSpan Duration { get; set; }
    public string OldValuesJson { get; set; } = string.Empty;
    public string NewValuesJson { get; set; } = string.Empty;
    public string ChangedPropertiesJson { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
    public List<string> Tags { get; set; } = new();
    public string CorrelationVector { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = "KKBOT";
    public string Environment { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
}

public enum AuditEventType
{
    Authentication,
    Authorization,
    DataAccess,
    DataModification,
    ConfigurationChange,
    SecurityEvent,
    SystemEvent,
    AdministrativeAction,
    DataExport,
    DataImport,
    PermissionChange,
    RoleAssignment,
    PolicyViolation,
    AnomalyDetected,
    ComplianceCheck,
    DataRetention,
    DataDeletion,
    BackupRestore,
    SystemMaintenance,
    Custom
}

public enum AuditOutcome
{
    Success,
    Failure,
    Partial,
    Denied,
    Error,
    Cancelled
}

public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Data retention policy
/// </summary>
public sealed class DataRetentionPolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string DataCategory { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public int RetentionDays { get; set; } = 2555; // 7 years default
    public int ArchiveAfterDays { get; set; } = 365;
    public RetentionAction ActionAfterRetention { get; set; } = RetentionAction.Delete;
    public string ArchiveLocation { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool IsDefault { get; set; } = false;
    public List<string> ApplicableRegulations { get; set; } = new(); // GDPR, SOX, HIPAA, PCI-DSS, CCPA
    public List<string> ExemptedDataTypes { get; set; } = new();
    public string LegalHoldTag { get; set; } = string.Empty;
    public bool RequireLegalHoldApproval { get; set; } = true;
    public string LegalHoldContact { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public string UpdatedBy { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime? NextExecutionAt { get; set; }
    public DateTime? LastExecutionAt { get; set; }
    public RetentionExecutionStatus LastExecutionStatus { get; set; }
    public string LastExecutionMessage { get; set; } = string.Empty;
    public int ExecutionCount { get; set; } = 0;
    public int FailureCount { get; set; } = 0;
}

public enum RetentionAction
{
    Delete,
    Archive,
    Anonymize,
    MoveToColdStorage,
    LegalHold,
    NotifyOnly
}

public enum RetentionExecutionStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Partial,
    Cancelled
}

/// <summary>
/// GDPR Data Subject Request
/// </summary>
public sealed class DataSubjectRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RequestId { get; set; } = string.Empty;
    public DataSubjectRequestType Type { get; set; }
    public DataSubjectRequestStatus Status { get; set; } = DataSubjectRequestStatus.Submitted;
    public string DataSubjectId { get; set; } = string.Empty;
    public string DataSubjectEmail { get; set; } = string.Empty;
    public string DataSubjectName { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string SubmittedBy { get; set; } = string.Empty;
    public string SubmittedByEmail { get; set; } = string.Empty.
    public string LegalBasis { get; set; } = string.Empty.
    public string Description { get; set; } = string.Empty.
    public string RequestDetailsJson { get; set; } = "{}";
    public List<string> DataCategories { get; set; } = new();
    public List<string> ProcessingPurposes { get; set; } = new().
    public List<string> DataControllers { get; set; } = new().
    public List<string> DataProcessors { get; set; } = new().
    public List<string> ThirdCountries { get; set; } = new().
    public List<string> Safeguards { get; set; } = new().
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow.
    public DateTime? AcknowledgedAt { get; set; }.
    public DateTime? ProcessedAt { get; set; }.
    public DateTime? CompletedAt { get; set; }.
    public DateTime? RejectedAt { get; set; }.
    public DateTime DueDate { get; set; } = DateTime.UtcNow.AddDays(30).
    public string AssignedTo { get; set; } = string.Empty.
    public string AssignedToEmail { get; set; } = string.Empty.
    public string RejectionReason { get; set; } = string.Empty.
    public string RejectionDetails { get; set; } = string.Empty.
    public string CompletionDetails { get; set; } = string.Empty.
    public string OutputPath { get; set; } = string.Empty.
    public long OutputSizeBytes { get; set; } = 0.
    public string VerificationToken { get; set; } = string.Empty.
    public bool IsVerified { get; set; } = false.
    public DateTime? VerifiedAt { get; set; }.
    public string VerifiedBy { get; set; } = string.Empty.
    public List<DataSubjectRequestAttachment> Attachments { get; set; } = new().
    public List<DataSubjectRequestNote> Notes { get; set; } = new().
    public bool RequiresIdentityVerification { get; set; } = true.
    public bool IdentityVerified { get; set; } = false.
    public DateTime? IdentityVerifiedAt { get; set; }.
    public string IdentityVerificationMethod { get; set; } = string.Empty.
    public string LegalHoldTag { get; set; } = string.Empty.
    public string EscalationContact { get; set; } = string.Empty.
    public DateTime? EscalatedAt { get; set; }.
}

public enum DataSubjectRequestType
{
    Access,
    Rectification,
    Erasure,
    Restriction,
    Portability,
    Objection,
    AutomatedDecisionMaking,
    WithdrawConsent,
    DataBreachNotification
}

public enum DataSubjectRequestStatus
{
    Submitted,
    Acknowledged,
    InProgress,
    UnderReview,
    RequiresClarification,
    RequiresIdentityVerification,
    Approved,
    InProgress,
    Completed,
    PartiallyCompleted,
    Rejected,
    Cancelled,
    Escalated
}

public sealed class DataSubjectRequestAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid().
    public string FileName { get; set; } = string.Empty.
    public string ContentType { get; set; } = string.Empty.
    public long SizeBytes { get; set; } = 0.
    public string StoragePath { get; set; } = string.Empty.
    public string Checksum { get; set; } = string.Empty.
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow.
    public string UploadedBy { get; set; } = string.Empty.
}

public sealed class DataSubjectRequestNote
{
    public Guid Id { get; set; } = Guid.NewGuid().
    public string Note { get; set; } = string.Empty.
    public string AuthorId { get; set; } = string.Empty.
    public string AuthorName { get; set; } = string.Empty.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow.
    public bool IsInternal { get; set; } = false.
}

public sealed class DataBreachNotification
{
    public Guid Id { get; set; } = Guid.NewGuid().
    public string NotificationId { get; set; } = string.Empty.
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow.
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow.
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow.
    public string Title { get; set; } = string.Empty.
    public string Description { get; set; } = string.Empty.
    public BreachSeverity Severity { get; set; } = BreachSeverity.Medium.
    public BreachStatus Status { get; set; } = BreachStatus.Detected.
    public string TenantId { get; set; } = string.Empty.
    public string DetectedBy { get; set; } = string.Empty.
    public string DetectedByEmail { get; set; } = string.Empty.
    public string ReportedBy { get; set; } = string.Empty.
    public string ReportedByEmail { get; set; } = string.Empty.
    public List<string> AffectedDataCategories { get; set; } = new().
    public List<string> AffectedDataSubjects { get; set; } = new().
    public long EstimatedAffectedRecords { get; set; } = 0.
    public List<string> AffectedSystems { get; set; } = new().
    public List<string> AffectedDataTypes { get; set; } = new().
    public string RootCause { get; set; } = string.Empty.
    public string ImpactAssessment { get; set; } = string.Empty.
    public string RiskAssessment { get; set; } = string.Empty.
    public string ContainmentActions { get; set; } = string.Empty.
    public string RemediationActions { get; set; } = string.Empty.
    public string NotificationToAuthorities { get; set; } = string.Empty.
    public DateTime? AuthoritiesNotifiedAt { get; set; }.
    public string AuthorityReference { get; set; } = string.Empty.
    public string NotificationToDataSubjects { get; set; } = string.Empty.
    public DateTime? DataSubjectsNotifiedAt { get; set; }.
    public string MitigationMeasures { get; set; } = string.Empty.
    public string PreventionMeasures { get; set; } = string.Empty.
    public string LessonsLearned { get; set; } = string.Empty.
    public DateTime? ClosedAt { get; set; }.
    public string ClosedBy { get; set; } = string.Empty.
    public string ClosureReason { get; set; } = string.Empty.
    public List<DataBreachAttachment> Attachments { get; set; } = new().
    public List<DataBreachNote> Notes { get; set; } = new().
}

public enum BreachSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public enum BreachStatus
{
    Detected,
    Contained,
    UnderInvestigation,
    Remediated,
    Notified,
    Closed
}

public sealed class DataBreachAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid().
    public string FileName { get; set; } = string.Empty.
    public string ContentType { get; set; } = string.Empty.
    public long SizeBytes { get; set; } = 0.
    public string StoragePath { get; set; } = string.Empty.
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow.
    public string UploadedBy { get; set; } = string.Empty.
}

public sealed class DataBreachNote
{
    public Guid Id { get; set; } = Guid.NewGuid().
    public string Note { get; set; } = string.Empty.
    public string AuthorId { get; set; } = string.Empty.
    public string AuthorName { get; set; } = string.Empty.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow.
    public bool IsInternal { get; set; } = false.
}

#endregion

#region Compliance & Policy Models

/// <summary>
/// Compliance framework/policy
/// </summary>
public sealed class ComplianceFramework
{
    public Guid Id { get; set; } = Guid.NewGuid().
    public string Name { get; set; } = string.Empty.
    public string Code { get; set; } = string.Empty.
    public string Description { get; set; } = string.Empty.
    public string Version { get; set; } = string.Empty.
    public string Url { get; set; } = string.Empty.
    public FrameworkType Type { get; set; } = FrameworkType.Custom.
    public FrameworkStatus Status { get; set; } = FrameworkStatus.Active.
    public string TenantId { get; set; } = string.Empty.
    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow.
    public DateTime? ExpiryDate { get; set; }.
    public List<ComplianceRequirement> Requirements { get; set; } = new().
    public List<ComplianceControl> Controls { get; set; } = new().
    public List<CompliancePolicy> Policies { get; set; } = new().
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow.
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow.
    public string CreatedBy { get; set; } = string.Empty.
    public string UpdatedBy { get; set; } = string.Empty.
    public bool IsActive { get; set; } = true.
    public bool IsMandatory { get; set; } = false.
    public string OwnerId { get; set; } = string.Empty.
    public string OwnerEmail { get; set; } = string.Empty.
}

public enum FrameworkType
{
    GDPR,
    SOX,
    HIPAA,
    PCI_DSS,
    CCPA,
    ISO_27001,
    ISO_27002,
    NIST_CSF,
    NIST_800_53,
    ISO_27001,
    SOC2,
    FISMA,
    FedRAMP,
    PCI_DSS_v4,
    Custom
}

public enum FrameworkStatus
{
    Draft,
    Active,
    Deprecated,
    Archived,
    UnderReview
}

public sealed class ComplianceRequirement
{
    public Guid Id { get; set; } = Guid.NewGuid().
    public string RequirementId { get; set; } = string.Empty.
    public string Title { get; set; } = string.Empty.
    public string Description { get; set; } = string.Empty.
    public string Category { get; set; } = string.Empty.
    public string Section { get; set; } = string.Empty.
    public string Article { get; set; } = string.Empty.
    public string Clause { get; set; } = string.Empty.
    public RequirementPriority Priority { get; set; } = RequirementPriority.Medium.
    public RequirementType Type { get; set; } = RequirementType.Mandatory.
    public List<string> ApplicableDataTypes { get; set; } = new().
    public List<string> ApplicableSystems { get; set; } = new().
    public List<string> Keywords { get; set; } = new().
    public string EvidenceRequired { get; set; } = string.Empty.
    public string TestingProcedure { get; set; } = string.Empty.
    public string RemediationGuidance { get; set; } = string.Empty.
    public string ReferenceUrl { get; set; } = string.Empty.
    public List<string> RelatedRequirements { get; set; } = new().
    public Dictionary<string, object> Metadata { get; set; } = new().
}

public enum RequirementPriority
{
    Low,
    Medium,
    High,
    Critical
}

public enum RequirementType
{
    Mandatory,
    Conditional,
    Optional,
    Recommended
}

public sealed class ComplianceControl
{
    public Guid Id { get; set; } = Guid.NewGuid().
    public string ControlId { get; set; } = string.Empty.
    public string Name { get; set; } = string.Empty.
    public string Description { get; set; } = string.Empty.
    public ControlType Type { get; set; } = ControlType.Preventive.
    public ControlCategory Category { get; set; } = ControlCategory.Technical.
    public ControlFrequency Frequency { get; set; } = ControlFrequency.Continuous.
    public bool IsAutomated { get; set; } = false.
    public string AutomationScript { get; set; } = string.Empty.
    public string TestProcedure { get; set; } = string.Empty.
    public string EvidenceRequired { get; set; } = string.Empty.
    public string OwnerId { get; set; } = string.Empty.
    public string OwnerEmail { get; set; } = string.Empty.
    public DateTime LastTestedAt { get; set; } = DateTime.UtcNow.
    public DateTime NextTestAt { get; set; } = DateTime.UtcNow.AddDays(90).
    public ControlStatus Status { get; set; } = ControlStatus.NotTested.
    public DateTime LastAssessedAt { get; set; } = DateTime.UtcNow.
    public string LastAssessmentResult { get; set; } = string.Empty.
    public string NextAssessmentBy { get; set; } = string.Empty.
    public List<string> LinkedRequirements { get; set; } = new().
    public List<string> LinkedPolicies { get; set; } = new().
    public Dictionary<string, object> Metadata { get; set; } = new().
}

public enum ControlType
{
    Preventive,
    Detective,
    Corrective,
    Deterrent,
    Compensating,
    Directive
}

public enum ControlCategory
{
    Technical,
    Administrative,
    Physical,
    Organizational,
    Legal,
    Human
}

public enum ControlFrequency
{
    Continuous,
    RealTime,
    Hourly,
    Daily,
    Weekly,
    Monthly,
    Quarterly,
    SemiAnnually,
    Annually,
    OnDemand
}

public enum ControlStatus
{
    NotTested,
    Testing,
    Passed,
    Failed,
    Partial,
    NotApplicable,
    Exception,
    Remediated
}

public sealed class CompliancePolicy
{
    public Guid Id { get; set; } = Guid.NewGuid().
    public string Name { get; set; } = string.Empty.
    public string Description { get; set; } = string.Empty.
    public PolicyType Type { get; set; } = PolicyType.Security.
    public PolicyScope Scope { get; set; } = PolicyScope.Global.
    public string TenantId { get; set; } = string.Empty.
    public string Version { get; set; } = "1.0".
    public string ContentJson { get; set; } = "{}".
    public string ContentHash { get; set; } = string.Empty.
    public bool IsEnforced { get; set; } = true.
    public EnforcementLevel Enforcement { get; set; } = EnforcementLevel.Blocking.
    public List<string> ApplicableRoles { get; set; } = new().
    public List<string> ExemptRoles { get; set; } = new().
    public List<string> ApplicableResources { get; set; } = new().
    public List<string> ExemptResources { get; set; } = new().
    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow.
    public DateTime? ExpiryDate { get; set; }.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow.
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow.
    public string CreatedBy { get; set; } = string.Empty.
    public string UpdatedBy { get; set; } = string.Empty.
    public bool IsActive { get; set; } = true.
    public string OwnerId { get; set; } = string.Empty.
    public string OwnerEmail { get; set; } = string.Empty.
    public Dictionary<string, object> Parameters { get; set; } = new().
    public List<string> Tags { get; set; } = new().
}

public enum PolicyType
{
    Security,
    Privacy,
    DataProtection,
    AccessControl,
    Retention,
    Encryption,
    Audit,
    IncidentResponse,
    BusinessContinuity,
    DisasterRecovery,
    VendorManagement,
    ThirdPartyRisk,
    Custom
}

public enum PolicyScope
{
    Global,
    Tenant,
    Department,
    Project,
    System,
    Resource
}

public enum EnforcementLevel
{
    Advisory,
    Warning,
    Blocking,
    Quarantine,
    AutoRemediate
}

#endregion

#region Advanced RBAC Models

/// <summary>
/// Fine-grained permission
/// </summary>
public sealed class Permission
{
    public Guid Id { get; set; } = Guid.NewGuid().
    public string Name { get; set; } = string.Empty.
    public string DisplayName { get; set; } = string.Empty.
    public string Description { get; set; } = string.Empty.
    public string Category { get; set; } = string.Empty.
    public string ResourceType { get; set; } = string.Empty.
    public string Action { get; set; } = string.Empty.
    public string Scope { get; set; } = string.Empty.
    public string Condition { get; set; } = string.Empty.
    public bool IsSystemPermission { get; set; } = false.
    public bool IsDeprecated { get; set; } = false.
    public string ReplacementPermission { get; set; } = string.Empty.
    public int Priority { get; set; } = 0.
    public List<string> Tags { get; set; } = new().
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow.
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow.
}

/// <summary>
/// Role with fine-grained permissions
/// </summary>
public sealed class Role
{
    public Guid Id { get; set; } = Guid.NewGuid().
    public string Name { get; set; } = string.Empty.
    public string DisplayName { get; set; } = string.Empty.
    public string Description { get; set; } = string.Empty.
    public bool IsSystemRole { get; set; } = false.
    public bool IsDefault { get; set; } = false.
    public bool IsActive { get; set; } = true.
    public RoleType Type { get; set; } = RoleType.Custom.
    public Guid? ParentRoleId { get; set; }.
    public Role? ParentRole { get; set; }.
    public List<Role> ChildRoles { get; set; } = new().
    public List<Permission> Permissions { get; set; } = new().
    public List<string> IncludedPermissions { get; set; } = new().
    public List<string> ExcludedPermissions { get; set; } = new().
    public RoleConstraints Constraints { get; set; } = new().
    public RoleMetadata Metadata { get; set; } = new().
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow.
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow.
    public string CreatedBy { get; set; } = string.Empty.
    public string UpdatedBy { get; set; } = string.Empty.
    public bool IsSystemRole_ { get; set; } = false.
}

public enum RoleType
{
    System,
    Tenant,
    Project,
    Application,
    Resource,
    Custom
}

public sealed class RoleConstraints
{
    public int MaxUsers { get; set; } = 0.
    public int MaxSessionsPerUser { get; set; } = 0.
    public TimeSpan MaxSessionDuration { get; set; } = TimeSpan.FromHours(8).
    public TimeSpan MaxInactivityTimeout { get; set; } = TimeSpan.FromMinutes(30).
    public bool RequireMfa { get; set; } = false.
    public List<string> AllowedIpRanges { get; set; } = new().
    public List<string> BlockedIpRanges { get; set; } = new().
    public TimeSpan? AllowedTimeWindowStart { get; set; }.
    public TimeSpan? AllowedTimeWindowEnd { get; set; }.
    public List<DayOfWeek> AllowedDays { get; set; } = new().
    public int MaxConcurrentSessions { get; set; } = 1.
    public bool AllowConcurrentSessions { get; set; } = true.
    public List<string> AllowedLocations { get; set; } = new().
    public List<string> BlockedLocations { get; set; } = new().
    public bool RequireDeviceCompliance { get; set; } = false.
    public int MinDeviceSecurityLevel { get; set; } = 0.
    public Dictionary<string, object> CustomConstraints { get; set; } = new().
}

public sealed class RoleMetadata
{
    public string Color { get; set; } = string.Empty.
    public string Icon { get; set; } = string.Empty.
    public int DisplayOrder { get; set; } = 0.
    public bool ShowInUi { get; set; } = true.
    public string HelpText { get; set; } = string.Empty.
    public string DocumentationUrl { get; set; } = string.Empty.
    public Dictionary<string, string> CustomAttributes { get; set; } = new().
}

/// <summary>
/// Permission grant/assignment
/// </summary>
public sealed class PermissionGrant
{
    public Guid Id { get; set; } = Guid.NewGuid().
    public string PrincipalId { get; set; } = string.Empty. // User or Group ID
    public PrincipalType PrincipalType { get; set; } = PrincipalType.User.
    public string PrincipalName { get; set; } = string.Empty.
    public Guid PermissionId { get; set; }.
    public Permission Permission { get; set; } = null!.
    public Guid? ResourceId { get; set; }.
    public string ResourceType { get; set; } = string.Empty.
    public string ResourceName { get; set; } = string.Empty.
    public string Condition { get; set; } = string.Empty.
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow.
    public DateTime? ExpiresAt { get; set; }.
    public string GrantedBy { get; set; } = string.Empty.
    public GrantStatus Status { get; set; } = GrantStatus.Active.
    public string GrantedByName { get; set; } = string.Empty.
    public string Justification { get; set; } = string.Empty.
    public string ApprovalTicket { get; set; } = string.Empty.
    public DateTime? ApprovedAt { get; set; }.
    public string ApprovedBy { get; set; } = string.Empty.
    public List<string> Conditions { get; set; } = new().
    public Dictionary<string, object> Metadata { get; set; } = new().
}

public enum PrincipalType
{
    User,
    Group,
    Role,
    ServicePrincipal,
    Application,
    Device
}

public enum GrantStatus
{
    Active,
    Expired,
    Revoked,
    Pending,
    PendingApproval,
    Denied,
    Suspended
}

/// <summary>
/// Access control rule
/// </summary>
public sealed class AccessControlRule
{
    public Guid Id { get; set; } = Guid.NewGuid().
    public string Name { get; set; } = string.Empty.
    public string Description { get; set; } = string.Empty.
    public AccessRuleType Type { get; set; } = AccessRuleType.Allow.
    public int Priority { get; set; } = 0.
    public AccessRuleCondition Condition { get; set; } = new().
    public AccessRuleAction Action { get; set; } = new().
    public List<string> ApplicableRoles { get; set; } = new().
    public List<string> ExcludedRoles { get; set; } = new().
    public List<string> ApplicableUsers { get; set; } = new().
    public List<string> ExcludedUsers { get; set; } = new().
    public List<string> ApplicableResources { get; set; } = new().
    public List<string> ExcludedResources { get; set; } = new().
    public List<string> ApplicableActions { get; set; } = new().
    public List<string> ExcludedActions { get; set; } = new().
    public TimeSpan? TimeWindowStart { get; set; }.
    public TimeSpan? TimeWindowEnd { get; set; }.
    public List<DayOfWeek> ApplicableDays { get; set; } = new().
    public List<string> ApplicableLocations { get; set; } = new().
    public List<string> ExcludedLocations { get; set; } = new().
    public int Priority { get; set; } = 0.
    public bool IsActive { get; set; } = true.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow.
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow.
    public string CreatedBy { get; set; } = string.Empty.
    public string UpdatedBy { get; set; } = string.Empty.
}

public enum AccessRuleType
{
    Allow,
    Deny,
    Challenge,
    LogOnly
}

public sealed class AccessRuleCondition
{
    public string Expression { get; set; } = string.Empty.
    public List<string> RequiredClaims { get; set; } = new().
    public List<string> RequiredRoles { get; set; } = new().
    public List<string> RequiredPermissions { get; set; } = new().
    public List<string> RequiredGroups { get; set; } = new().
    public Dictionary<string, object> CustomAttributes { get; set; } = new().
    public string CustomExpression { get; set; } = string.Empty.
}

public sealed class AccessRuleAction
{
    public string Action { get; set; } = string.Empty.
    public Dictionary<string, object> Parameters { get; set; } = new().
    public string RedirectUrl { get; set; } = string.Empty.
    public string ChallengeType { get; set; } = string.Empty.
    public int ChallengeTimeoutSeconds { get; set; } = 300.
}

#endregion

#region Data Governance Models

/// <summary>
/// Data classification
/// </summary>
public sealed class DataClassification
{
    public Guid Id { get; set; } = Guid.NewGuid().
    public string Name { get; set; } = string.Empty.
    public string Description { get; set; } = string.Empty.
    public ClassificationLevel Level { get; set; } = ClassificationLevel.Public.
    public string Color { get; set; } = string.Empty.
    public string Icon { get; set; } = string.Empty.
    public List<string> Keywords { get; set; } = new().
    public List<string> Patterns { get; set; } = new().
    public List<string> Examples { get; set; } = new().
    public bool IsPii { get; set; } = false.
    public bool IsPhi { get; set; } = false.
    public bool IsPci { get; set; } = false.
    public bool IsSensitive { get; set; } = false.
    public List<string> ApplicableRegulations { get; set; } = new().
    public List<string> HandlingRequirements { get; set; } = new().
    public List<string> ProhibitedActions { get; set; } = new().
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow.
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow.
    public string CreatedBy { get; set; } = string.Empty.
    public string UpdatedBy { get; set; } = string.Empty.
    public bool IsActive { get; set; } = true.
}

public enum ClassificationLevel
{
    Public = 0,
    Internal = 1,
    Confidential = 2,
    Restricted = 3,
    TopSecret = 4
}

/// <summary>
/// Data lineage tracking
/// </summary>
public sealed class DataLineage
{
    public Guid Id { get; set; } = Guid.NewGuid().
    public string SourceSystem { get; set; } = string.Empty.
    public string SourceEntity { get; set; } = string.Empty.
    public string SourceField { get; set; } = string.Empty.
    public string TargetSystem { get; set; } = string.Empty.
    public string TargetEntity { get; set; } = string.Empty.
    public string TargetField { get; set; } = string.Empty.
    public TransformationType Transformation { get; set; } = TransformationType.Copy.
    public string TransformationLogic { get; set; } = string.Empty.
    public string TransformationCode { get; set; } = string.Empty.
    public string TransformationTool { get; set; } = string.Empty.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow.
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow.
    public string CreatedBy { get; set; } = string.Empty.
    public string UpdatedBy { get; set; } = string.Empty.
    public bool IsActive { get; set; } = true.
    public DataQualityMetrics QualityMetrics { get; set; } = new().
    public List<DataLineageDependency> Dependencies { get; set; } = new().
    public List<DataLineageTag> Tags { get; set; } = new().
    public Dictionary<string, object> Metadata { get; set; } = new().
}

public enum TransformationType
{
    Copy,
    Transform,
    Aggregate,
    Filter,
    Join,
    Split,
    Merge,
    Calculate,
    Lookup,
    Cleanse,
    Enrich,
    Mask,
    Encrypt,
    Decrypt,
    Compress,
    Decompress,
    Custom
}

public sealed class DataQualityMetrics
{
    public double Completeness { get; set; } = 1.0.
    public double Accuracy { get; set; } = 1.0.
    public double Consistency { get; set; } = 1.0.
    public double Timeliness { get; set; } = 1.0.
    public double Validity { get; set; } = 1.0.
    public double Uniqueness { get; set; } = 1.0.
    public double OverallScore { get; set; } = 1.0.
    public long TotalRecords { get; set; } = 0.
    public long ValidRecords { get; set; } = 0.
    public long InvalidRecords { get; set; } = 0.
    public long NullRecords { get; set; } = 0.
    public long DuplicateRecords { get; set; } = 0.
    public Dictionary<string, double> FieldQualityScores { get; set; } = new().
}

public sealed class DataLineageDependency
{
    public Guid Id { get; set; } = Guid.NewGuid().
    public string DependsOnEntity { get; set; } = string.Empty.
    public string DependsOnField { get; set; } = string.Empty.
    public DependencyType Type { get; set; } = DependencyType.Direct.
    public bool IsCritical { get; set; } = false.
}

public enum DependencyType
{
    Direct,
    Indirect,
    Transitive,
    Circular
}

public sealed class DataLineageTag
{
    public Guid Id { get; set; } = Guid.NewGuid().
    public string Name { get; set; } = string.Empty.
    public string Color { get; set; } = string.Empty.
    public string Category { get; set; } = string.Empty.
}

#endregion

#region Risk & Security Models

/// <summary>
/// Risk assessment
/// </summary>
public sealed class RiskAssessment
{
    public Guid Id { get; set; } = Guid.NewGuid().
    public string Name { get; set; } = string.Empty.
    public string Description { get; set; } = string.Empty.
    public RiskCategory Category { get; set; } = RiskCategory.Operational.
    public RiskLikelihood Likelihood { get; set; } = RiskLikelihood.Possible.
    public RiskImpact Impact { get; set; } = RiskImpact.Moderate.
    public RiskRating Rating { get; set; } = RiskRating.Medium.
    public double RiskScore { get; set; } = 0.
    public string OwnerId { get; set; } = string.Empty.
    public string OwnerEmail { get; set; } = string.Empty.
    public string TenantId { get; set; } = string.Empty.
    public List<RiskFactor> Factors { get; set; } = new().
    public List<MitigationAction> Mitigations { get; set; } = new().
    public ResidualRisk ResidualRisk { get; set; } = new().
    public RiskStatus Status { get; set; } = RiskStatus.Identified.
    public DateTime IdentifiedAt { get; set; } = DateTime.UtcNow.
    public DateTime? AssessedAt { get; set; }.
    public DateTime? ReviewedAt { get; set; }.
    public DateTime? NextReviewAt { get; set; }.
    public string AssessedBy { get; set; } = string.Empty.
    public string ReviewedBy { get; set; } = string.Empty.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow.
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow.
    public bool IsActive { get; set; } = true.
    public string OwnerId { get; set; } = string.Empty.
    public string OwnerEmail { get; set; } = string.Empty.
}

public enum RiskCategory
{
    Strategic,
    Operational,
    Financial,
    Compliance,
    Reputational,
    Technical,
    Security,
    Privacy,
    Legal,
    Environmental,
    BusinessContinuity
}

public enum RiskLikelihood
{
    Rare,
    Unlikely,
    Possible,
    Likely,
    AlmostCertain
}

public enum RiskImpact
{
    Insignificant,
    Minor,
    Moderate,
    Major,
    Catastrophic
}

public enum RiskRating
{
    VeryLow,
    Low,
    Medium,
    High,
    VeryHigh,
    Critical
}

public enum RiskStatus
{
    Identified,
    Assessed,
    Treated,
    Monitoring,
    Closed,
    Accepted
}

public sealed class RiskFactor
{
    public Guid Id { get; set; } = Guid.NewGuid().
    public string Name { get; set; } = string.Empty.
    public string Description { get; set; } = string.Empty.
    public RiskLikelihood Likelihood { get; set; } = RiskLikelihood.Possible.
    public RiskImpact Impact { get; set; } = RiskImpact.Moderate.
    public double Weight { get; set; } = 1.0.
    public string Category { get; set; } = string.Empty.
    public List<string> Indicators { get; set; } = new().
    public string Mitigation { get; set; } = string.Empty.
}

public sealed class MitigationAction
{
    public Guid Id { get; set; } = Guid.NewGuid().
    public string Name { get; set; } = string.Empty.
    public string Description { get; set; } = string.Empty.
    public MitigationType Type { get; set; } = MitigationType.Avoid.
    public string OwnerId { get; set; } = string.Empty.
    public string OwnerEmail { get; set; } = string.Empty.
    public DateTime DueDate { get; set; } = DateTime.UtcNow.AddDays(30).
    public DateTime? CompletedAt { get; set; }.
    public MitigationStatus Status { get; set; } = MitigationStatus.Planned.
    public double Effectiveness { get; set; } = 0.
    public double Cost { get; set; } = 0.
    public string Currency { get; set; } = "USD".
    public List<string> Dependencies { get; set; } = new().
    public List<string> RisksAddressed { get; set; } = new().
    public Dictionary<string, object> Metadata { get; set; } = new().
}

public enum MitigationType
{
    Avoid,
    Reduce,
    Transfer,
    Accept,
    Share
}

public enum MitigationStatus
{
    Planned,
    InProgress,
    Implemented,
    Verified,
    Effective,
    Ineffective,
    Cancelled
}

public sealed class ResidualRisk
{
    public RiskLikelihood Likelihood { get; set; } = RiskLikelihood.Possible.
    public RiskImpact Impact { get; set; } = RiskImpact.Moderate.
    public RiskRating Rating { get; set; } = RiskRating.Medium.
    public double Score { get; set; } = 0.
    public string Justification { get; set; } = string.Empty.
    public DateTime AssessedAt { get; set; } = DateTime.UtcNow.
    public string AssessedBy { get; set; } = string.Empty.
}

#endregion

#region Incident Response Models

/// <summary>
/// Security incident
/// </summary>
public sealed class SecurityIncident
{
    public Guid Id { get; set; } = Guid.NewGuid().
    public string IncidentId { get; set; } = string.Empty.
    public string Title { get; set; } = string.Empty.
    public string Description { get; set; } = string.Empty.
    public IncidentSeverity Severity { get; set; } = IncidentSeverity.Medium.
    public IncidentStatus Status { get; set; } = IncidentStatus.New.
    public IncidentType Type { get; set; } = IncidentType.Security.
    public string TenantId { get; set; } = string.Empty.
    public string ReporterId { get; set; } = string.Empty.
    public string ReporterEmail { get; set; } = string.Empty.
    public string AssignedToId { get; set; } = string.Empty.
    public string AssignedToEmail { get; set; } = string.Empty.
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow.
    public DateTime? DetectedAt { get; set; }.
    public DateTime? AcknowledgedAt { get; set; }.
    public DateTime? ContainedAt { get; set; }.
    public DateTime? ResolvedAt { get; set; }.
    public DateTime? ClosedAt { get; set; }.
    public string RootCause { get; set; } = string.Empty.
    public string ImpactAssessment { get; set; } = string.Empty.
    public List<string> AffectedSystems { get; set; } = new().
    public List<string> AffectedUsers { get; set; } = new().
    public List<string> AffectedDataTypes { get; set; } = new().
    public List<string> AttackVectors { get; set; } = new().
    public List<string> IndicatorsOfCompromise { get; set; } = new().
    public List<IncidentTimelineEvent> Timeline { get; set; } = new().
    public List<IncidentAction> Actions { get; set; } = new().
    public List<IncidentEvidence> Evidence { get; set; } = new().
    public List<IncidentCommunication> Communications { get; set; } = new().
    public List<IncidentLesson> LessonsLearned { get; set; } = new().
    public IncidentSeverity FinalSeverity { get; set; } = IncidentSeverity.Medium.
    public string RootCause { get; set; } = string.Empty.
    public string RemediationSummary { get; set; } = string.Empty.
    public string PreventionMeasures { get; set; } = string.Empty.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow.
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow.
    public string CreatedBy { get; set; } = string.Empty.
    public string UpdatedBy { get; set; } = string.Empty.
}

public enum IncidentSeverity
{
    SEV1_Critical,
    SEV2_High,
    SEV3_Medium,
    SEV4_Low,
    SEV5_Informational
}

public enum IncidentStatus
{
    New,
    Triaged,
    Investigating,
    Contained,
    Eradicated,
    Recovering,
    Resolved,
    Closed,
    PostMortem
}

public enum IncidentType
{
    Security,
    Privacy,
    Availability,
    Performance,
    DataLoss,
    DataBreach,
    UnauthorizedAccess,
    Malware,
    Phishing,
    InsiderThreat,
    DDoS,
    VulnerabilityExploit,
    ConfigurationDrift,
    ComplianceViolation,
    DataLoss,
    ServiceDegradation
}

public sealed class IncidentTimelineEvent
{
    public Guid Id { get; set; } = Guid.NewGuid().
    public DateTime Timestamp { get; set; } = DateTime.UtcNow.
    public string EventType { get; set; } = string.Empty.
    public string Description { get; set; } = string.Empty.
    public string ActorId { get; set; } = string.Empty.
    public string ActorName { get; set; } = string.Empty.
    public string ActorRole { get; set; } = string.Empty.
    public string DetailsJson { get; set; } = "{}".
    public bool IsAutomated { get; set; } = false.
    public bool IsMilestone { get; set; } = false.
}

public sealed class IncidentAction
{
    public Guid Id { get; set; } = Guid.NewGuid().
    public string ActionType { get; set; } = string.Empty.
    public string Description { get; set; } = string.Empty.
    public string ActorId { get; set; } = string.Empty.
    public string ActorName { get; set; } = string.Empty.
    public DateTime Timestamp { get; set; } = DateTime.UtcNow.
    public ActionStatus Status { get; set; } = ActionStatus.Pending.
    public string Outcome { get; set; } = string.Empty.
    public string DetailsJson { get; set; } = "{}".
    public List<string> AffectedResources { get; set; } = new().
}

public enum ActionStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Cancelled,
    Skipped
}

public sealed class IncidentEvidence
{
    public Guid Id { get; set; } = Guid.NewGuid().
    public string EvidenceType { get; set; } = string.Empty.
    public string FileName { get; set; } = string.Empty.
    public string FilePath { get; set; } = string.Empty.
    public long FileSizeBytes { get; set; } = 0.
    public string Hash { get; set; } = string.Empty.
    public string HashAlgorithm { get; set; } = "SHA256".
    public string CollectedBy { get; set; } = string.Empty.
    public DateTime CollectedAt { get; set; } = DateTime.UtcNow.
    public string ChainOfCustody { get; set; } = string.Empty.
    public bool IsAdmissible { get; set; } = true.
    public string Description { get; set; } = string.Empty.
    public Dictionary<string, object> Metadata { get; set; } = new().
}

public sealed class IncidentCommunication
{
    public Guid Id { get; set; } = Guid.NewGuid().
    public DateTime Timestamp { get; set; } = DateTime.UtcNow.
    public string Channel { get; set; } = string.Empty.
    public string Recipient { get; set; } = string.Empty.
    public string Subject { get; set; } = string.Empty.
    public string Message { get; set; } = string.Empty.
    public bool IsInternal { get; set; } = true.
    public bool IsAutomated { get; set; } = false.
    public string SentBy { get; set; } = string.Empty.
    public string SentByName { get; set; } = string.Empty.
}

public sealed class IncidentLesson
{
    public Guid Id { get; set; } = Guid.NewGuid().
    public string Title { get; set; } = string.Empty.
    public string Description { get; set; } = string.Empty.
    public string Category { get; set; } = string.Empty.
    public string Recommendation { get; set; } = string.Empty.
    public string Priority { get; set; } = string.Empty.
    public string AssignedTo { get; set; } = string.Empty.
    public DateTime? DueDate { get; set; }.
    public bool IsImplemented { get; set; } = false.
    public DateTime? ImplementedAt { get; set; }.
    public string ImplementedBy { get; set; } = string.Empty.
}

#endregion

// Supporting enums and classes

public enum DataSubjectRequestType
{
    Access,
    Rectification,
    Erasure,
    Restriction,
    Portability,
    Objection,
    AutomatedDecisionMaking,
    WithdrawConsent
}

public enum DataSubjectRequestStatus
{
    Submitted,
    Acknowledged,
    InProgress,
    Completed,
    Rejected,
    Cancelled
}

public sealed class DataSubjectRequestAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; } = 0;
    public string StoragePath { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string UploadedBy { get; set; } = string.Empty;
}

public sealed class DataSubjectRequestNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Note { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsInternal { get; set; } = false;
}

public enum BreachSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public enum BreachStatus
{
    Detected,
    Contained,
    UnderInvestigation,
    Remediated,
    Notified,
    Closed
}

public sealed class DataBreachAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; } = 0;
    public string StoragePath { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public sealed class DataBreachNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Note { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsInternal { get; set; } = false;
}