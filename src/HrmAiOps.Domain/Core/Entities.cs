using HrmAiOps.Domain.Common;

namespace HrmAiOps.Domain.Core;

public enum EnvironmentKind { Dev, Test, Uat, Production }
public enum CustomerStatus { Active, Suspended, Archived }
public enum ProjectStatus { Active, OnHold, Completed, Archived }
public enum IssueStatus { Open, InProgress, Resolved, Closed, Cancelled }
public enum IssuePriority { P0, P1, P2, P3, P4 }
public enum WorkStatus { Draft, Active, InReview, Approved, Rejected, Completed, Cancelled, Archived }
public enum IssueSeverity { Low, Medium, High, Critical }
public enum AiRunStatus { Queued, Running, Completed, Failed }
public enum ApprovalStatus { Pending, Approved, Rejected, Cancelled }
public enum ReleaseStatus { Planned, Approved, Deploying, Deployed, Failed, RolledBack }
public enum DocumentKind { Requirement, Urs, Blueprint, ConfigSpec }
public enum RiskLevel { Low, Medium, High, Critical }
public enum AiProposalStatus { PendingReview, Accepted, Rejected, FailedValidation }
public enum AiTaskType { GenerateUrs, GenerateBlueprint, GenerateConfigSpec, ClassifyIssue, AnalyzeRootCause, GenerateFixProposal, GenerateChangeRequest, GenerateRegressionTestPlan, GenerateReleaseDraft, GenerateKnowledgeUpdate, GenerateLessonsLearned, DetectRepeatedIssue, CalculateGovernanceScores, EvaluateAiPerformance }
public enum IssueCategory { Functional, Configuration, Data, Integration, Security, Permission, Payroll, Performance, ProductionDatabase, Other }
public enum ConnectorRunStatus { Queued, Running, Completed, Failed, Rejected }
public enum SnapshotKind { DatabaseSchema, Config, SourceRepository, ApplicationLog, EnvironmentHealth, IntegrationHealth, ApplyComposite }
public enum SnapshotStage { Baseline, PreApply, PostApply }
public enum ApplyRunStatus { Draft, DryRunSucceeded, DryRunFailed, ApprovalRequired, Applying, Applied, Failed, RegressionFailed, ReleaseReady }
public enum ApplyStepStatus { Pending, Running, Succeeded, Failed, Skipped }
public enum RegressionRunStatus { Pending, Running, Passed, Failed, Blocked }
public enum ReleaseReadinessStatus { NotReady, Ready, Blocked }
public enum ProductionReleaseStatus { Draft, PendingApproval, Approved, Scheduled, Deploying, ValidationFailed, RollbackRequested, ReadyToClose, Closed, Rejected }
public enum ReleaseWindowStatus { Draft, Scheduled, Active, Expired, Cancelled }
public enum DeploymentRunStatus { NotStarted, Running, Succeeded, Failed, Blocked }
public enum DeploymentStepRunStatus { Pending, WaitingConfirmation, Running, Succeeded, Failed, Skipped }
public enum PostReleaseValidationStatus { Pending, Running, Passed, Warning, Failed, Completed }
public enum RollbackDecisionStatus { NotRequested, Requested, Approved, Rejected, Executed }
public enum ReleaseCommunicationAudience { Internal, Customer, Support, Training }
public enum DeploymentExecutionMethod { Automated, Manual, GuardedScript, ReadOnlyCheck }
public enum KnowledgeLifecycleStatus { Draft, PendingReview, Approved, Rejected, Superseded, Expired }
public enum KnowledgeSourceType { Issue, IssueAnalysis, FixProposal, ReleaseReadinessReport, ProductionReleasePackage, PostReleaseValidation, RollbackDecision, PostReleaseTask, ReleaseClosureReport, Manual }
public enum GovernanceScoreType { CustomerHealth, ModuleRisk, ConfigRisk, ProjectDeliveryQuality, AiPerformanceQuality }
public enum GovernanceInsightType { LessonsLearned, RepeatedIssue, RiskTrend, QualitySignal, AiQuality }
public enum AnalyticsTrend { Improving, Stable, Declining }
public enum TenantAccessStatus { Active, Suspended, Revoked }
public enum SecurityPolicyEffect { Allow, Deny }
public enum DataClassificationLevel { Public, Internal, Confidential, Restricted, Secret }
public enum SecretAccessStatus { Allowed, Denied, NotFound }
public enum ComplianceEvidenceStatus { Draft, Collected, Reviewed, Accepted, Rejected }
public enum SubscriptionStatus { Trial, Active, Suspended, Cancelled, Expired }
public enum ContractStatus { Draft, Active, Suspended, Terminated, Expired }
public enum BillingCycle { Monthly, Quarterly, Yearly }
public enum SlaStatus { OnTrack, Warning, Breached, Met, Paused }
public enum PortalTicketStatus { Open, WaitingCustomer, InProgress, Resolved, Closed, Cancelled }
public enum ServiceRequestStatus { Submitted, Approved, InProgress, Completed, Rejected, Cancelled }
public enum UsageMetricType { Project, Connector, AiRun, Ticket, StorageGb, ProductionRelease, ControlledApply }
public enum QuotaEnforcementMode { WarnOnly, Block }
public enum BillingDraftStatus { Draft, Approved, Voided }
public enum InvoiceDraftStatus { Draft, Sent, Voided, Paid }
public enum PaymentTrackingStatus { Pending, Recorded, Failed, Refunded }
public enum PortalVisibility { CustomerVisible, InternalOnly, Restricted }
public enum PortalRequestStatus { Draft, Submitted, InReview, WaitingForCustomer, InProgress, WaitingForApproval, Approved, Rejected, Completed, Closed, Cancelled }
public enum PortalApprovalType { UrsSignoff, BlueprintSignoff, ChangeRequestApproval, ReleaseApproval, UatSignoff, ServiceRequestApproval, BillingApproval, Other }
public enum PortalApprovalStatus { Pending, Approved, Rejected, Cancelled, Expired }
public enum NotificationType { TicketUpdate, ApprovalRequired, ApprovalResult, SlaWarning, SlaBreached, ReleaseScheduled, ReleaseCompleted, BillingAvailable, ServiceReportAvailable, CommentMention, SystemAnnouncement }
public enum NotificationStatus { Unread, Read, Archived }
public enum CollaborationMessageType { Comment, StatusUpdate, SystemMessage, AiMessage, Attachment, ApprovalRequest, ApprovalResult }
public enum FeedbackRating { VeryPoor, Poor, Neutral, Good, Excellent }
public enum AiSelfServiceSessionStatus { Active, Closed, EscalatedToSupport }
public enum NotificationChannel { InApp, Email }
public enum NotificationRecipientType { InternalUser, PortalUser, Role, ExternalEmail }
public enum NotificationDeliveryStatus { Pending, Delivered, Failed, Skipped }
public enum WorkflowTriggerType { IssueCreated, SlaWarning, SlaBreached, ApprovalPending, ReleaseScheduled, DeploymentCompleted, InvoiceGenerated, DocumentShared, CommentAdded, AiOutputReady, CustomerRequestSubmitted, Manual }
public enum WorkflowRuleStatus { Active, Disabled }
public enum WorkflowActionType { CreateNotification, SendEmail, CreateTask, CreateReminder, Escalate, AddTimelineEntry }
public enum WorkflowRunStatus { Queued, Running, Completed, Failed, Skipped }
public enum CollaborationTaskStatus { Open, InProgress, Waiting, Completed, Cancelled, Escalated }
public enum CollaborationTaskPriority { Low, Medium, High, Critical }
public enum ActivityTimelineItemType { Comment, StatusChange, Approval, AiAction, ReleaseAction, Notification, Task, Reminder, Escalation, System }
public enum ReminderStatus { Scheduled, Sent, Cancelled, Failed }
public enum EscalationStatus { Open, Notified, Acknowledged, Resolved }
public enum ReportDocumentType { Urs, Blueprint, ConfigSpec, UatTestCase, ReleaseReadiness, ProductionRelease, CustomerService, Sla, Billing, Audit, Security, Knowledge, ExecutiveSummary }
public enum ReportOutputFormat { Word, Pdf, Excel }
public enum ReportGenerationStatus { Queued, Running, Completed, Failed }
public enum ReportVisibility { InternalOnly, SharedWithCustomer, PublishedToPortal }
public enum DashboardSnapshotType { Executive, Project, CustomerHealth }
public enum IntegrationProviderCategory { CustomerHrmApi, GitProvider, DevOps, IssueTracking, Messaging, Email, Automation, Erp, Accounting, TicketSystem, Webhook, Other }
public enum IntegrationAuthType { None, SecretRefToken, OAuthClientSecretRef, BasicSecretRef, WebhookSignatureSecretRef }
public enum IntegrationDirection { Inbound, Outbound, Gateway }
public enum IntegrationRunStatus { Queued, Running, Succeeded, Failed, Retrying, Rejected, TimedOut }
public enum IntegrationEventType { IssueCreated, RequirementApproved, ReleaseScheduled, DeploymentCompleted, ReportPublished, BillingGenerated, SlaBreached, WebhookReceived, GatewayInvoked, Manual }
public enum WebhookSignatureMode { None, MockHmac, HmacSha256, ProviderManaged }
public enum IntegrationAutomationActionType { CreateNotification, CreateTask, InvokeOutboundWebhook, TriggerWorkflowEvent }
public enum DevOpsProviderKind { GitHub, GitLab, AzureDevOps, MockGit }
public enum DevOpsRunType { RepositorySync, BranchCreate, PullRequestCreate, CodeReview, AiCodeAnalysis, AiPatchProposal, Build, Test, CodeScan, Package, Merge, Deploy }
public enum DevOpsRunStatus { Queued, Running, Succeeded, Failed, Blocked, RequiresApproval }
public enum PullRequestStatus { Draft, Open, ReviewRequired, ChangesRequested, Approved, Merged, Rejected, Blocked }
public enum CodeReviewDecision { Pending, Approved, ChangesRequested, Rejected }
public enum PipelineRunStatus { Queued, Running, Succeeded, Failed, Blocked }
public enum PipelineRunType { Build, Test, CodeScan }
public enum DeploymentPackageStatus { Draft, BuildRequired, TestRequired, ScanBlocked, ApprovalRequired, Ready, Deployed, Blocked }
public enum CodeChangeArea { Hrm, Payroll, Permission, Security, Integration, ProductionDeployment, Other }
public enum TelemetrySourceType { PlatformApi, CustomerHrmApi, Database, Connector, Integration, Deployment, BackgroundWorker, ExternalEndpoint }
public enum TelemetrySignalType { HealthCheck, Metric, LogSummary, ErrorEvent, Uptime, ApiLatency, DatabaseHealth, ConnectorHealth, DeploymentHealth }
public enum TelemetryHealthStatus { Healthy, Degraded, Unhealthy, Unknown }
public enum MonitoringRuleOperator { GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual, Equals, Contains }
public enum AlertSeverity { Info, Warning, High, Critical }
public enum AlertStatus { Open, Acknowledged, Resolved, Suppressed }
public enum IncidentStatus { Open, Investigating, Mitigated, Resolved, Closed }
public enum IncidentPriority { P4, P3, P2, P1, P0 }
public enum IncidentActionType { Notify, Escalate, CreateIssue, AttachSla, AiDiagnose, CreatePostIncidentReview, UpdateKnowledge, ManualNote }
public enum ImportFileType { Csv, Excel }
public enum HrmDataDomain { Employee, Organization, Job, Payroll, BankAccount, LeaveBalance, Attendance, Benefit, SecurityUser, Other }
public enum ImportTemplateStatus { Draft, Active, Archived }
public enum DataMappingStatus { Draft, Active, Superseded }
public enum ValidationRuleType { Required, Regex, Range, Lookup, Unique, DuplicateDetection, DataClassification, CustomExpression }
public enum ValidationIssueSeverity { Info, Warning, Error, Critical }
public enum ImportBatchStatus { Draft, FileUploaded, MappingReady, DryRunPassed, DryRunFailed, AppliedToTestUat, Reconciled, SignedOff, Cancelled, Blocked }
public enum DataImportRunType { DryRun, Apply }
public enum DataImportRunStatus { Queued, Running, Passed, Failed, Blocked }
public enum ReconciliationStatus { Pending, Matched, Mismatch, Failed }
public enum DataSignOffStatus { Pending, SignedOff, Rejected, Revoked }
public enum AiDataAssistanceType { MappingSuggestion, ValidationExplanation, ErrorExplanation }
public enum TrainingContentStatus { Draft, Approved, Published, Archived }
public enum TrainingMaterialType { Guide, Video, Sop, Faq, ReleaseNote, Workbook }
public enum TrainingDeliveryMode { SelfPaced, InstructorLed, Webinar, Workshop }
public enum TrainingAttendanceStatus { Invited, Attended, NoShow, Excused }
public enum LearningProgressStatus { NotStarted, InProgress, Completed, Overdue }
public enum AssessmentResultStatus { Submitted, Passed, Failed }
public enum CertificationStatus { Pending, Issued, Revoked, Expired }
public enum AdoptionSignalType { Login, FeatureUse, TransactionCreated, ReportViewed, AiQuestionAsked, TrainingCompleted }
public enum TrainingGapSourceType { Issue, TrainingQuestion, AdoptionData, AssessmentFailure, Manual }
public enum TrainingGapStatus { Open, Planned, InProgress, Closed }
public enum AiTrainingAssistantStatus { Answered, Escalated, Blocked }

public sealed class Customer : Entity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public CustomerStatus Status { get; set; } = CustomerStatus.Active;
    public string? Industry { get; set; }
    public string Timezone { get; set; } = "Asia/Bangkok";
}

public sealed class Project : CustomerScopedEntity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    public string? HrmProductName { get; set; }
}

public sealed class ProjectEnvironment : ProjectScopedEntity
{
    public string Name { get; set; } = "";
    public EnvironmentKind Kind { get; set; }
    public string? BaseUrl { get; set; }
    public string Status { get; set; } = "Active";
    public bool RequiresApproval { get; set; }
}

public sealed class SourceRepository : ProjectScopedEntity
{
    public Guid? EnvironmentId { get; set; }
    public string Provider { get; set; } = "Git";
    public string RepoUrl { get; set; } = "";
    public string DefaultBranch { get; set; } = "main";
    public string SecretRef { get; set; } = "";
    public string Status { get; set; } = "Active";
}

public sealed class DatabaseProfile : ProjectScopedEntity
{
    public Guid EnvironmentId { get; set; }
    public string Engine { get; set; } = "PostgreSQL";
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public string DatabaseName { get; set; } = "";
    public string? UsernameRef { get; set; }
    public string SecretRef { get; set; } = "";
    public bool ReadOnly { get; set; }
    public string Status { get; set; } = "Active";
}

public sealed class Requirement : ProjectScopedEntity
{
    public Guid VersionGroupId { get; set; }
    public Guid? SupersedesDocumentId { get; set; }
    public bool IsLatest { get; set; } = true;
    public string RequirementNo { get; set; } = "";
    public string Title { get; set; } = "";
    public string SourceType { get; set; } = "Manual";
    public string? SourceFileRef { get; set; }
    public string ContentText { get; set; } = "";
    public WorkStatus Status { get; set; } = WorkStatus.Draft;
    public int Version { get; set; } = 1;
    public string? CreatedBy { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
}

public sealed class UrsDocument : ProjectScopedEntity
{
    public Guid VersionGroupId { get; set; }
    public Guid? SupersedesDocumentId { get; set; }
    public bool IsLatest { get; set; } = true;
    public Guid RequirementId { get; set; }
    public string UrsNo { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public WorkStatus Status { get; set; } = WorkStatus.Draft;
    public int Version { get; set; } = 1;
    public Guid? AiRunId { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
}

public sealed class Blueprint : ProjectScopedEntity
{
    public Guid VersionGroupId { get; set; }
    public Guid? SupersedesDocumentId { get; set; }
    public bool IsLatest { get; set; } = true;
    public Guid UrsId { get; set; }
    public string BlueprintNo { get; set; } = "";
    public string Type { get; set; } = "Functional";
    public string Content { get; set; } = "";
    public WorkStatus Status { get; set; } = WorkStatus.Draft;
    public int Version { get; set; } = 1;
    public Guid? AiRunId { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
}

public sealed class ConfigSpec : ProjectScopedEntity
{
    public Guid VersionGroupId { get; set; }
    public Guid? SupersedesDocumentId { get; set; }
    public bool IsLatest { get; set; } = true;
    public Guid? EnvironmentId { get; set; }
    public Guid BlueprintId { get; set; }
    public string ConfigNo { get; set; } = "";
    public string ModuleName { get; set; } = "HRM";
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Medium;
    public string Content { get; set; } = "";
    public WorkStatus Status { get; set; } = WorkStatus.Draft;
    public int Version { get; set; } = 1;
    public Guid? AiRunId { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
}

public sealed class DocumentSignOff : ProjectScopedEntity
{
    public DocumentKind DocumentKind { get; set; }
    public Guid DocumentId { get; set; }
    public int Version { get; set; }
    public string SignedOffBy { get; set; } = "";
    public string? Role { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset SignedOffAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class HrmModuleDefinition : Entity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public RiskLevel DefaultRiskLevel { get; set; } = RiskLevel.Medium;
    public string Description { get; set; } = "";
}

public sealed class Issue : ProjectScopedEntity
{
    public Guid? EnvironmentId { get; set; }
    public string? LinkedEntityType { get; set; }
    public Guid? LinkedEntityId { get; set; }
    public string IssueNo { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public IssueCategory Category { get; set; } = IssueCategory.Other;
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Medium;
    public IssueSeverity Severity { get; set; } = IssueSeverity.Medium;
    public IssuePriority Priority { get; set; } = IssuePriority.P2;
    public IssueStatus Status { get; set; } = IssueStatus.Open;
    public string? ReportedBy { get; set; }
    public string? AssignedTo { get; set; }
    public string? RootCauseSummary { get; set; }
}

public sealed class IssueAnalysis : ProjectScopedEntity
{
    public Guid IssueId { get; set; }
    public Guid? AiRunId { get; set; }
    public string AnalysisType { get; set; } = "Diagnosis";
    public string Content { get; set; } = "";
    public decimal ConfidenceScore { get; set; }
}

public sealed class FixProposal : ProjectScopedEntity
{
    public Guid IssueId { get; set; }
    public Guid? AiRunId { get; set; }
    public string Title { get; set; } = "";
    public string ProposedSolution { get; set; } = "";
    public string? CodeChangeSummary { get; set; }
    public string? DbChangeSummary { get; set; }
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Medium;
    public WorkStatus Status { get; set; } = WorkStatus.Draft;
}

public sealed class ChangeRequest : ProjectScopedEntity
{
    public Guid? IssueId { get; set; }
    public Guid? FixProposalId { get; set; }
    public string CrNo { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public Guid TargetEnvironmentId { get; set; }
    public WorkStatus Status { get; set; } = WorkStatus.Draft;
    public bool RequiresApproval { get; set; }
}

public sealed class ApprovalRequest : ProjectScopedEntity
{
    public string EntityType { get; set; } = "";
    public Guid EntityId { get; set; }
    public Guid? TargetEnvironmentId { get; set; }
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public string? RequestedBy { get; set; }
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class ApprovalStep : CustomerScopedEntity
{
    public Guid ApprovalRequestId { get; set; }
    public int StepOrder { get; set; }
    public string ApproverUserId { get; set; } = "";
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public string? Comment { get; set; }
    public DateTimeOffset? ActedAt { get; set; }
}

public sealed class Release : ProjectScopedEntity
{
    public string ReleaseNo { get; set; } = "";
    public Guid? ChangeRequestId { get; set; }
    public Guid TargetEnvironmentId { get; set; }
    public string Version { get; set; } = "";
    public ReleaseStatus Status { get; set; } = ReleaseStatus.Planned;
    public string? ReleaseNotes { get; set; }
    public string? DeploymentPlan { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? DeployedAt { get; set; }
}

public sealed class RollbackPoint : ProjectScopedEntity
{
    public Guid ReleaseId { get; set; }
    public Guid EnvironmentId { get; set; }
    public string? SourceCommit { get; set; }
    public string? ArtifactRef { get; set; }
    public string? DatabaseBackupRef { get; set; }
    public string? ConfigSnapshotRef { get; set; }
}

public sealed class RegressionTestPlan : ProjectScopedEntity
{
    public Guid IssueId { get; set; }
    public Guid? ChangeRequestId { get; set; }
    public Guid? AiRunId { get; set; }
    public string TestPlanNo { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Medium;
    public WorkStatus Status { get; set; } = WorkStatus.Draft;
}

public sealed class ReleaseDraft : ProjectScopedEntity
{
    public Guid IssueId { get; set; }
    public Guid? ChangeRequestId { get; set; }
    public Guid? AiRunId { get; set; }
    public string ReleaseDraftNo { get; set; } = "";
    public string Title { get; set; } = "";
    public string ReleaseNotes { get; set; } = "";
    public string DeploymentPlan { get; set; } = "";
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Medium;
    public WorkStatus Status { get; set; } = WorkStatus.Draft;
}

public sealed class KnowledgeArticle : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid? IssueId { get; set; }
    public string Title { get; set; } = "";
    public string Category { get; set; } = "General";
    public string Content { get; set; } = "";
    public string Visibility { get; set; } = "Customer";
    public WorkStatus Status { get; set; } = WorkStatus.Draft;
}

public sealed class AiRun : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public string RunType { get; set; } = "";
    public string Provider { get; set; } = "LocalStub";
    public string Model { get; set; } = "rule-based";
    public string? PromptTemplateId { get; set; }
    public string? PromptTemplateKey { get; set; }
    public int? PromptVersion { get; set; }
    public string? InputRef { get; set; }
    public string? InputSummary { get; set; }
    public string? MaskedInputPreview { get; set; }
    public string? OutputRef { get; set; }
    public string? OutputSummary { get; set; }
    public string? RawOutputJson { get; set; }
    public string? ValidationErrorsJson { get; set; }
    public AiRunStatus Status { get; set; } = AiRunStatus.Queued;
    public int TokenInput { get; set; }
    public int TokenOutput { get; set; }
    public decimal CostEstimate { get; set; }
    public string? ErrorMessage { get; set; }
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class AiPromptTemplate : Entity
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public AiTaskType TaskType { get; set; }
    public string Description { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public sealed class AiPromptTemplateVersion : Entity
{
    public Guid TemplateId { get; set; }
    public string TemplateKey { get; set; } = "";
    public int Version { get; set; }
    public string SystemPrompt { get; set; } = "";
    public string UserPromptTemplate { get; set; } = "";
    public string OutputJsonSchema { get; set; } = "";
    public string CreatedBy { get; set; } = "system";
    public bool IsActive { get; set; } = true;
}

public sealed class AiProposal : ProjectScopedEntity
{
    public Guid AiRunId { get; set; }
    public AiTaskType TaskType { get; set; }
    public string SourceEntityType { get; set; } = "";
    public Guid SourceEntityId { get; set; }
    public string TargetEntityType { get; set; } = "";
    public Guid? TargetEntityId { get; set; }
    public AiProposalStatus Status { get; set; } = AiProposalStatus.PendingReview;
    public string Title { get; set; } = "";
    public string ProposedContent { get; set; } = "";
    public string StructuredOutputJson { get; set; } = "";
    public string ValidationErrorsJson { get; set; } = "[]";
    public string? ReviewedBy { get; set; }
    public string? ReviewComment { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
}

public sealed class CustomerConnector : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid? EnvironmentId { get; set; }
    public Guid? PermissionPolicyId { get; set; }
    public string ConnectorType { get; set; } = "";
    public string Name { get; set; } = "";
    public string? BaseUrl { get; set; }
    public string SecretRef { get; set; } = "";
    public string ConfigJson { get; set; } = "{}";
    public string Status { get; set; } = "Active";
    public string? LastHealthStatus { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
}

public sealed class ConnectorPermissionPolicy : ProjectScopedEntity
{
    public Guid? EnvironmentId { get; set; }
    public string Name { get; set; } = "";
    public bool AllowSchemaRead { get; set; } = true;
    public bool AllowConfigRead { get; set; } = true;
    public bool AllowLogRead { get; set; } = true;
    public bool AllowSourceMetadataRead { get; set; } = true;
    public bool AllowHealthCheck { get; set; } = true;
    public bool AllowTestApply { get; set; }
    public bool AllowProductionApplyWithApproval { get; set; }
    public string MaskingProfile { get; set; } = "Default";
    public string Status { get; set; } = "Active";
}

public sealed class ConnectorRun : ProjectScopedEntity
{
    public Guid EnvironmentId { get; set; }
    public Guid ConnectorId { get; set; }
    public string RunType { get; set; } = "";
    public ConnectorRunStatus Status { get; set; } = ConnectorRunStatus.Queued;
    public string? InputSummary { get; set; }
    public string? OutputSummary { get; set; }
    public string? MaskedPreview { get; set; }
    public string? ErrorMessage { get; set; }
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public int? DurationMs { get; set; }
}

public sealed class EnvironmentSnapshot : ProjectScopedEntity
{
    public Guid EnvironmentId { get; set; }
    public Guid ConnectorId { get; set; }
    public Guid? ConnectorRunId { get; set; }
    public Guid? ApplyRunId { get; set; }
    public string SnapshotNo { get; set; } = "";
    public SnapshotKind Kind { get; set; } = SnapshotKind.ApplyComposite;
    public SnapshotStage Stage { get; set; } = SnapshotStage.Baseline;
    public string Summary { get; set; } = "";
    public string SnapshotJson { get; set; } = "{}";
    public string MaskedSummary { get; set; } = "";
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SnapshotDiff : ProjectScopedEntity
{
    public Guid EnvironmentId { get; set; }
    public Guid FromSnapshotId { get; set; }
    public Guid ToSnapshotId { get; set; }
    public SnapshotKind SnapshotKind { get; set; } = SnapshotKind.ApplyComposite;
    public string DiffSummary { get; set; } = "";
    public string DiffJson { get; set; } = "{}";
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Medium;
}

public sealed class ApplyRun : ProjectScopedEntity
{
    public Guid EnvironmentId { get; set; }
    public Guid ConnectorId { get; set; }
    public Guid? FixProposalId { get; set; }
    public Guid? ChangeRequestId { get; set; }
    public Guid? ApprovalRequestId { get; set; }
    public Guid? PreSnapshotId { get; set; }
    public Guid? PostSnapshotId { get; set; }
    public Guid? SnapshotDiffId { get; set; }
    public Guid? RollbackPlanId { get; set; }
    public Guid? RegressionTestRunId { get; set; }
    public Guid? ReleaseReadinessReportId { get; set; }
    public string ApplyRunNo { get; set; } = "";
    public string SourceType { get; set; } = "";
    public Guid SourceId { get; set; }
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Medium;
    public ApplyRunStatus Status { get; set; } = ApplyRunStatus.Draft;
    public bool RequiresApproval { get; set; }
    public string RequestedBy { get; set; } = "";
    public string RollbackRecommendation { get; set; } = "";
    public string Summary { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class ApplyStep : ProjectScopedEntity
{
    public Guid EnvironmentId { get; set; }
    public Guid ApplyRunId { get; set; }
    public int StepOrder { get; set; }
    public string Name { get; set; } = "";
    public ApplyStepStatus Status { get; set; } = ApplyStepStatus.Pending;
    public string Details { get; set; } = "";
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class ApplyLog : ProjectScopedEntity
{
    public Guid EnvironmentId { get; set; }
    public Guid ApplyRunId { get; set; }
    public string Level { get; set; } = "Info";
    public string Message { get; set; } = "";
    public string? MaskedPayload { get; set; }
}

public sealed class RollbackPlan : ProjectScopedEntity
{
    public Guid EnvironmentId { get; set; }
    public Guid ApplyRunId { get; set; }
    public string PlanNo { get; set; } = "";
    public string Strategy { get; set; } = "";
    public string Steps { get; set; } = "";
    public string ValidationChecklist { get; set; } = "";
    public WorkStatus Status { get; set; } = WorkStatus.Draft;
}

public sealed class RegressionTestRun : ProjectScopedEntity
{
    public Guid EnvironmentId { get; set; }
    public Guid ApplyRunId { get; set; }
    public Guid? RegressionTestPlanId { get; set; }
    public string RunNo { get; set; } = "";
    public RegressionRunStatus Status { get; set; } = RegressionRunStatus.Pending;
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public string Summary { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class ReleaseReadinessReport : ProjectScopedEntity
{
    public Guid EnvironmentId { get; set; }
    public Guid ApplyRunId { get; set; }
    public Guid? SnapshotDiffId { get; set; }
    public Guid? RegressionTestRunId { get; set; }
    public string ReportNo { get; set; } = "";
    public ReleaseReadinessStatus Status { get; set; } = ReleaseReadinessStatus.NotReady;
    public string Summary { get; set; } = "";
    public string Blockers { get; set; } = "";
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ProductionReleasePackage : ProjectScopedEntity
{
    public Guid ProductionEnvironmentId { get; set; }
    public Guid ReleaseReadinessReportId { get; set; }
    public Guid? ApprovalRequestId { get; set; }
    public Guid? ReleaseWindowId { get; set; }
    public Guid? DeploymentPlanId { get; set; }
    public Guid? PreSnapshotId { get; set; }
    public Guid? PostSnapshotId { get; set; }
    public Guid? SnapshotDiffId { get; set; }
    public Guid? LatestDeploymentRunId { get; set; }
    public Guid? ClosureReportId { get; set; }
    public string PackageNo { get; set; } = "";
    public string Version { get; set; } = "";
    public string Title { get; set; } = "";
    public ProductionReleaseStatus Status { get; set; } = ProductionReleaseStatus.Draft;
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Medium;
    public string Summary { get; set; } = "";
    public bool RollbackPlanValidated { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
}

public sealed class ReleaseChecklistItem : ProjectScopedEntity
{
    public Guid ProductionReleasePackageId { get; set; }
    public string Title { get; set; } = "";
    public bool Required { get; set; }
    public bool Completed { get; set; }
    public string? EvidenceRef { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class ReleaseWindow : ProjectScopedEntity
{
    public Guid ProductionReleasePackageId { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string Timezone { get; set; } = "Asia/Bangkok";
    public ReleaseWindowStatus Status { get; set; } = ReleaseWindowStatus.Draft;
}

public sealed class ProductionDeploymentPlan : ProjectScopedEntity
{
    public Guid ProductionReleasePackageId { get; set; }
    public string PlanNo { get; set; } = "";
    public bool Validated { get; set; }
    public string ValidationErrors { get; set; } = "";
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ProductionDeploymentStep : ProjectScopedEntity
{
    public Guid ProductionReleasePackageId { get; set; }
    public Guid DeploymentPlanId { get; set; }
    public int StepOrder { get; set; }
    public string Title { get; set; } = "";
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Medium;
    public DeploymentExecutionMethod ExecutionMethod { get; set; } = DeploymentExecutionMethod.ReadOnlyCheck;
    public bool ManualConfirmationRequired { get; set; }
    public bool Confirmed { get; set; }
    public string GuardResult { get; set; } = "";
}

public sealed class ProductionDeploymentRun : ProjectScopedEntity
{
    public Guid ProductionReleasePackageId { get; set; }
    public Guid ProductionEnvironmentId { get; set; }
    public Guid DeploymentPlanId { get; set; }
    public Guid? PreSnapshotId { get; set; }
    public Guid? PostSnapshotId { get; set; }
    public Guid? SnapshotDiffId { get; set; }
    public string RunNo { get; set; } = "";
    public DeploymentRunStatus Status { get; set; } = DeploymentRunStatus.NotStarted;
    public string? ErrorMessage { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class DeploymentStepRun : ProjectScopedEntity
{
    public Guid ProductionReleasePackageId { get; set; }
    public Guid DeploymentRunId { get; set; }
    public Guid DeploymentStepId { get; set; }
    public int StepOrder { get; set; }
    public string Title { get; set; } = "";
    public DeploymentStepRunStatus Status { get; set; } = DeploymentStepRunStatus.Pending;
    public bool ManualConfirmationRequired { get; set; }
    public string? ConfirmedBy { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class ProductionDeploymentLog : ProjectScopedEntity
{
    public Guid ProductionReleasePackageId { get; set; }
    public Guid DeploymentRunId { get; set; }
    public string Level { get; set; } = "Info";
    public string Message { get; set; } = "";
    public string? MaskedPayload { get; set; }
}

public sealed class PostReleaseValidationCheck : ProjectScopedEntity
{
    public Guid ProductionReleasePackageId { get; set; }
    public string Title { get; set; } = "";
    public PostReleaseValidationStatus Status { get; set; } = PostReleaseValidationStatus.Pending;
    public string? Evidence { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class RollbackDecisionRequest : ProjectScopedEntity
{
    public Guid ProductionReleasePackageId { get; set; }
    public Guid? DeploymentRunId { get; set; }
    public string Reason { get; set; } = "";
    public string Impact { get; set; } = "";
    public RollbackDecisionStatus Status { get; set; } = RollbackDecisionStatus.Requested;
    public string? ApprovedBy { get; set; }
    public string? RollbackRunRef { get; set; }
}

public sealed class ReleaseCommunication : ProjectScopedEntity
{
    public Guid ProductionReleasePackageId { get; set; }
    public ReleaseCommunicationAudience Audience { get; set; }
    public string Subject { get; set; } = "";
    public string Content { get; set; } = "";
    public bool Sent { get; set; }
    public DateTimeOffset? SentAt { get; set; }
}

public sealed class PostReleaseTask : ProjectScopedEntity
{
    public Guid ProductionReleasePackageId { get; set; }
    public string Target { get; set; } = "";
    public string Title { get; set; } = "";
    public bool Completed { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class ReleaseClosureReport : ProjectScopedEntity
{
    public Guid ProductionReleasePackageId { get; set; }
    public string ReportNo { get; set; } = "";
    public string DeploymentSummary { get; set; } = "";
    public string ValidationSummary { get; set; } = "";
    public string RollbackSummary { get; set; } = "";
    public string DocumentUpdateSummary { get; set; } = "";
    public string FinalRecommendation { get; set; } = "";
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class KnowledgeLearningItem : ProjectScopedEntity
{
    public Guid? AiRunId { get; set; }
    public KnowledgeSourceType SourceType { get; set; } = KnowledgeSourceType.Manual;
    public string SourceEntityType { get; set; } = "";
    public Guid SourceEntityId { get; set; }
    public string SourceSummary { get; set; } = "";
    public string KnowledgeNo { get; set; } = "";
    public string Title { get; set; } = "";
    public string Category { get; set; } = "General";
    public string ModuleName { get; set; } = "";
    public string Content { get; set; } = "";
    public string LessonsLearned { get; set; } = "";
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Medium;
    public KnowledgeLifecycleStatus Status { get; set; } = KnowledgeLifecycleStatus.PendingReview;
    public int Version { get; set; } = 1;
    public Guid VersionGroupId { get; set; }
    public Guid? SupersedesKnowledgeItemId { get; set; }
    public bool LowRiskAutoApproved { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string ExplainabilityJson { get; set; } = "{}";
}

public sealed class RepeatedIssuePattern : ProjectScopedEntity
{
    public Guid? AiRunId { get; set; }
    public string PatternKey { get; set; } = "";
    public string ModuleName { get; set; } = "";
    public IssueCategory Category { get; set; } = IssueCategory.Other;
    public int IssueCount { get; set; }
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Medium;
    public string Summary { get; set; } = "";
    public string Recommendation { get; set; } = "";
    public string SourceIssueIdsJson { get; set; } = "[]";
    public DateTimeOffset FirstSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class GovernanceScoreSnapshot : ProjectScopedEntity
{
    public Guid? AiRunId { get; set; }
    public GovernanceScoreType ScoreType { get; set; }
    public string? ModuleName { get; set; }
    public Guid? ConfigSpecId { get; set; }
    public decimal Score { get; set; }
    public AnalyticsTrend Trend { get; set; } = AnalyticsTrend.Stable;
    public string Formula { get; set; } = "";
    public string Explanation { get; set; } = "";
    public string InputsJson { get; set; } = "{}";
    public DateTimeOffset CalculatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class GovernanceInsight : ProjectScopedEntity
{
    public Guid? AiRunId { get; set; }
    public GovernanceInsightType InsightType { get; set; }
    public string ModuleName { get; set; } = "";
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Medium;
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Recommendation { get; set; } = "";
    public string SourceRefsJson { get; set; } = "[]";
    public WorkStatus Status { get; set; } = WorkStatus.Draft;
}

public sealed class AiPerformanceMetric : ProjectScopedEntity
{
    public string? PromptTemplateKey { get; set; }
    public string? TaskType { get; set; }
    public int TotalRuns { get; set; }
    public int CompletedRuns { get; set; }
    public int FailedRuns { get; set; }
    public int AcceptedOutputs { get; set; }
    public int RejectedOutputs { get; set; }
    public int FailedValidationOutputs { get; set; }
    public decimal QualityScore { get; set; }
    public string Formula { get; set; } = "";
    public string Explanation { get; set; } = "";
    public DateTimeOffset CalculatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TenantAccessGrant : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public string UserId { get; set; } = "";
    public string RoleKey { get; set; } = "";
    public TenantAccessStatus Status { get; set; } = TenantAccessStatus.Active;
    public string GrantedBy { get; set; } = "";
    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class SecurityRole : CustomerScopedEntity
{
    public string RoleKey { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsSystemRole { get; set; }
}

public sealed class SecurityPermission : Entity
{
    public string PermissionKey { get; set; } = "";
    public string Name { get; set; } = "";
    public string Resource { get; set; } = "";
    public string Action { get; set; } = "";
    public bool Sensitive { get; set; }
}

public sealed class SecurityRolePermission : CustomerScopedEntity
{
    public string RoleKey { get; set; } = "";
    public string PermissionKey { get; set; } = "";
    public SecurityPolicyEffect Effect { get; set; } = SecurityPolicyEffect.Allow;
}

public sealed class UserRoleAssignment : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public string UserId { get; set; } = "";
    public string RoleKey { get; set; } = "";
    public TenantAccessStatus Status { get; set; } = TenantAccessStatus.Active;
}

public sealed class SecurityPolicyRule : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public string PolicyKey { get; set; } = "";
    public string Resource { get; set; } = "";
    public string Action { get; set; } = "";
    public string RequiredPermission { get; set; } = "";
    public SecurityPolicyEffect Effect { get; set; } = SecurityPolicyEffect.Allow;
    public bool Enabled { get; set; } = true;
}

public sealed class SecretVaultReference : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public string Name { get; set; } = "";
    public string SecretRef { get; set; } = "";
    public string VaultProvider { get; set; } = "LocalStubVault";
    public DataClassificationLevel Classification { get; set; } = DataClassificationLevel.Secret;
    public DateTimeOffset? RotationDueAt { get; set; }
    public string Status { get; set; } = "Active";
}

public sealed class SecretAccessAudit : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public string UserId { get; set; } = "";
    public string SecretRef { get; set; } = "";
    public string Purpose { get; set; } = "";
    public SecretAccessStatus Status { get; set; } = SecretAccessStatus.Denied;
    public string Reason { get; set; } = "";
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");
}

public sealed class DataClassificationRule : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public string ResourceType { get; set; } = "";
    public string FieldName { get; set; } = "";
    public DataClassificationLevel Classification { get; set; } = DataClassificationLevel.Internal;
    public string MaskingStrategy { get; set; } = "Redact";
    public bool ApplyToAiPrompt { get; set; } = true;
}

public sealed class AiAccessPolicy : ProjectScopedEntity
{
    public AiTaskType TaskType { get; set; }
    public string AllowedRolesCsv { get; set; } = "";
    public DataClassificationLevel MaxInputClassification { get; set; } = DataClassificationLevel.Confidential;
    public bool MaskingRequired { get; set; } = true;
    public bool RequiresApprovalForHighRisk { get; set; } = true;
    public string Status { get; set; } = "Active";
}

public sealed class ConnectorSecurityPolicy : ProjectScopedEntity
{
    public Guid? EnvironmentId { get; set; }
    public string ConnectorType { get; set; } = "";
    public string AllowedActionsCsv { get; set; } = "";
    public string RequiredPermission { get; set; } = "connector.read";
    public DataClassificationLevel MaxDataClassification { get; set; } = DataClassificationLevel.Confidential;
    public bool ReadOnlyRequired { get; set; } = true;
    public bool AllowTestApply { get; set; }
    public bool AllowProductionApplyWithApproval { get; set; }
    public string Status { get; set; } = "Active";
}

public sealed class ApprovalGovernanceRule : ProjectScopedEntity
{
    public string RuleKey { get; set; } = "";
    public string ModuleName { get; set; } = "";
    public RiskLevel? MinimumRiskLevel { get; set; }
    public bool AppliesToProduction { get; set; }
    public int RequiredApprovalSteps { get; set; } = 1;
    public string ApproverRolesCsv { get; set; } = "";
    public bool RequiresSecurityApproval { get; set; }
    public string Reason { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public sealed class ComplianceEvidence : ProjectScopedEntity
{
    public Guid? AuditLogId { get; set; }
    public Guid? ApprovalRequestId { get; set; }
    public string EvidenceNo { get; set; } = "";
    public string ControlId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string EntityType { get; set; } = "";
    public Guid? EntityId { get; set; }
    public string EvidenceRef { get; set; } = "";
    public ComplianceEvidenceStatus Status { get; set; } = ComplianceEvidenceStatus.Collected;
    public string TraceJson { get; set; } = "{}";
}

public sealed class ServicePlan : Entity
{
    public string PlanCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal BaseMonthlyPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public int MaxProjects { get; set; }
    public int MaxConnectors { get; set; }
    public int MaxAiRunsPerMonth { get; set; }
    public int MaxTicketsPerMonth { get; set; }
    public int IncludedSupportHours { get; set; }
    public int SlaResponseHours { get; set; }
    public int SlaResolutionHours { get; set; }
    public string EnabledModulesCsv { get; set; } = "";
    public QuotaEnforcementMode QuotaEnforcementMode { get; set; } = QuotaEnforcementMode.WarnOnly;
    public bool Active { get; set; } = true;
}

public sealed class CustomerContract : CustomerScopedEntity
{
    public string ContractNo { get; set; } = "";
    public string Title { get; set; } = "";
    public ContractStatus Status { get; set; } = ContractStatus.Draft;
    public DateTimeOffset StartsAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset EndsAt { get; set; } = DateTimeOffset.UtcNow.AddYears(1);
    public string Currency { get; set; } = "USD";
    public decimal ContractValue { get; set; }
    public string TermsSummary { get; set; } = "";
    public string BillingContactRef { get; set; } = "";
}

public sealed class Subscription : CustomerScopedEntity
{
    public Guid ServicePlanId { get; set; }
    public Guid? ContractId { get; set; }
    public string SubscriptionNo { get; set; } = "";
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trial;
    public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;
    public DateTimeOffset StartsAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndsAt { get; set; }
    public DateTimeOffset CurrentPeriodStart { get; set; } = DateTimeOffset.UtcNow.Date;
    public DateTimeOffset CurrentPeriodEnd { get; set; } = DateTimeOffset.UtcNow.Date.AddMonths(1);
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = "USD";
}

public sealed class SupportEntitlement : CustomerScopedEntity
{
    public Guid SubscriptionId { get; set; }
    public string EntitlementCode { get; set; } = "";
    public string Name { get; set; } = "";
    public int MaxTicketsPerMonth { get; set; }
    public int MaxAiRunsPerMonth { get; set; }
    public int MaxConnectors { get; set; }
    public int MaxProjects { get; set; }
    public int IncludedSupportHours { get; set; }
    public string EnabledModulesCsv { get; set; } = "";
    public QuotaEnforcementMode QuotaEnforcementMode { get; set; } = QuotaEnforcementMode.WarnOnly;
}

public sealed class SlaPolicy : CustomerScopedEntity
{
    public Guid? SubscriptionId { get; set; }
    public string PolicyNo { get; set; } = "";
    public string Name { get; set; } = "";
    public IssueSeverity Severity { get; set; } = IssueSeverity.Medium;
    public int ResponseHours { get; set; }
    public int ResolutionHours { get; set; }
    public int WarningBeforeHours { get; set; } = 2;
    public bool BusinessHoursOnly { get; set; }
    public string Timezone { get; set; } = "Asia/Bangkok";
}

public sealed class CustomerPortalTicket : ProjectScopedEntity
{
    public Guid? IssueId { get; set; }
    public Guid? SlaPolicyId { get; set; }
    public string TicketNo { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public IssueSeverity Severity { get; set; } = IssueSeverity.Medium;
    public PortalTicketStatus Status { get; set; } = PortalTicketStatus.Open;
    public string RequestedBy { get; set; } = "";
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FirstResponseAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public SlaStatus SlaStatus { get; set; } = SlaStatus.OnTrack;
    public DateTimeOffset? ResponseDueAt { get; set; }
    public DateTimeOffset? ResolutionDueAt { get; set; }
}

public sealed class ServiceRequest : ProjectScopedEntity
{
    public Guid? PortalTicketId { get; set; }
    public string RequestNo { get; set; } = "";
    public string RequestType { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public ServiceRequestStatus Status { get; set; } = ServiceRequestStatus.Submitted;
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Medium;
    public decimal EstimatedHours { get; set; }
    public string RequestedBy { get; set; } = "";
}

public sealed class UsageRecord : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid? SubscriptionId { get; set; }
    public UsageMetricType MetricType { get; set; }
    public string SourceEntityType { get; set; } = "";
    public Guid? SourceEntityId { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "count";
    public DateTimeOffset UsageDate { get; set; } = DateTimeOffset.UtcNow;
    public string Notes { get; set; } = "";
}

public sealed class UsageQuotaSnapshot : CustomerScopedEntity
{
    public Guid SubscriptionId { get; set; }
    public UsageMetricType MetricType { get; set; }
    public decimal UsedQuantity { get; set; }
    public decimal IncludedQuantity { get; set; }
    public decimal OverageQuantity { get; set; }
    public QuotaEnforcementMode EnforcementMode { get; set; }
    public bool Blocked { get; set; }
    public string WarningMessage { get; set; } = "";
    public DateTimeOffset PeriodStart { get; set; }
    public DateTimeOffset PeriodEnd { get; set; }
}

public sealed class BillingDraft : CustomerScopedEntity
{
    public Guid SubscriptionId { get; set; }
    public Guid? ContractId { get; set; }
    public string BillingDraftNo { get; set; } = "";
    public BillingDraftStatus Status { get; set; } = BillingDraftStatus.Draft;
    public DateTimeOffset PeriodStart { get; set; }
    public DateTimeOffset PeriodEnd { get; set; }
    public decimal Subtotal { get; set; }
    public decimal OverageAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string TraceJson { get; set; } = "{}";
}

public sealed class BillingLineItem : CustomerScopedEntity
{
    public Guid BillingDraftId { get; set; }
    public string ItemType { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public string SourceEntityType { get; set; } = "";
    public Guid? SourceEntityId { get; set; }
}

public sealed class InvoiceDraft : CustomerScopedEntity
{
    public Guid BillingDraftId { get; set; }
    public Guid SubscriptionId { get; set; }
    public string InvoiceNo { get; set; } = "";
    public InvoiceDraftStatus Status { get; set; } = InvoiceDraftStatus.Draft;
    public DateTimeOffset IssueDate { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset DueDate { get; set; } = DateTimeOffset.UtcNow.AddDays(15);
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string TraceJson { get; set; } = "{}";
}

public sealed class PaymentTrackingRecord : CustomerScopedEntity
{
    public Guid InvoiceDraftId { get; set; }
    public string PaymentRef { get; set; } = "";
    public PaymentTrackingStatus Status { get; set; } = PaymentTrackingStatus.Pending;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTimeOffset? RecordedAt { get; set; }
    public string Notes { get; set; } = "";
}

public sealed class CustomerServiceReport : CustomerScopedEntity
{
    public Guid? SubscriptionId { get; set; }
    public string ReportNo { get; set; } = "";
    public DateTimeOffset PeriodStart { get; set; }
    public DateTimeOffset PeriodEnd { get; set; }
    public int IssueCount { get; set; }
    public int SlaMetCount { get; set; }
    public int SlaBreachedCount { get; set; }
    public int ReleaseCount { get; set; }
    public int AiRunCount { get; set; }
    public int ConnectorRunCount { get; set; }
    public decimal HealthScore { get; set; }
    public string Summary { get; set; } = "";
    public string TraceJson { get; set; } = "{}";
}

public sealed class PortalUser : CustomerScopedEntity
{
    public string UserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string RoleKey { get; set; } = "portal.user";
    public string Status { get; set; } = "Active";
    public bool CanViewBilling { get; set; }
    public bool CanViewReports { get; set; }
    public bool CanApprove { get; set; }
}

public sealed class PortalProjectAccess : ProjectScopedEntity
{
    public Guid PortalUserId { get; set; }
    public string AccessLevel { get; set; } = "Viewer";
}

public sealed class PortalRequest : ProjectScopedEntity
{
    public string RequestNo { get; set; } = "";
    public string RequestType { get; set; } = "Support";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public PortalRequestStatus Status { get; set; } = PortalRequestStatus.Draft;
    public string Priority { get; set; } = "P2";
    public PortalVisibility Visibility { get; set; } = PortalVisibility.CustomerVisible;
    public string SubmittedByUserId { get; set; } = "";
    public Guid? ConvertedIssueId { get; set; }
    public Guid? ConvertedServiceRequestId { get; set; }
    public Guid? ConvertedChangeRequestId { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
}

public sealed class PortalRequirementIntake : ProjectScopedEntity
{
    public Guid? PortalRequestId { get; set; }
    public string Title { get; set; } = "";
    public string BusinessContext { get; set; } = "";
    public string RequirementText { get; set; } = "";
    public PortalRequestStatus Status { get; set; } = PortalRequestStatus.Draft;
    public string CreatedByUserId { get; set; } = "";
    public Guid? ConvertedRequirementId { get; set; }
}

public sealed class PortalDocumentShare : ProjectScopedEntity
{
    public string DocumentType { get; set; } = "";
    public Guid DocumentId { get; set; }
    public int DocumentVersion { get; set; } = 1;
    public PortalVisibility Visibility { get; set; } = PortalVisibility.CustomerVisible;
    public string SharedBy { get; set; } = "";
    public DateTimeOffset SharedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; set; }
    public string Status { get; set; } = "Published";
}

public sealed class PortalDocumentReview : ProjectScopedEntity
{
    public Guid DocumentShareId { get; set; }
    public string ReviewerUserId { get; set; } = "";
    public PortalApprovalStatus Status { get; set; } = PortalApprovalStatus.Pending;
    public string Comment { get; set; } = "";
    public DateTimeOffset? ReviewedAt { get; set; }
}

public sealed class PortalApproval : ProjectScopedEntity
{
    public PortalApprovalType ApprovalType { get; set; } = PortalApprovalType.Other;
    public string SourceEntityType { get; set; } = "";
    public Guid SourceEntityId { get; set; }
    public string RequestedBy { get; set; } = "";
    public Guid? ApproverPortalUserId { get; set; }
    public PortalApprovalStatus Status { get; set; } = PortalApprovalStatus.Pending;
    public string Comment { get; set; } = "";
    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
}

public sealed class PortalKnowledgeArticle : ProjectScopedEntity
{
    public Guid? SourceKnowledgeArticleId { get; set; }
    public string Title { get; set; } = "";
    public string Category { get; set; } = "General";
    public string Content { get; set; } = "";
    public PortalVisibility Visibility { get; set; } = PortalVisibility.CustomerVisible;
    public string Status { get; set; } = "Published";
    public int Version { get; set; } = 1;
    public DateTimeOffset? PublishedAt { get; set; }
}

public sealed class PortalTrainingSection : ProjectScopedEntity
{
    public string Title { get; set; } = "";
    public string ModuleName { get; set; } = "HRM";
    public string Content { get; set; } = "";
    public PortalVisibility Visibility { get; set; } = PortalVisibility.CustomerVisible;
    public string Status { get; set; } = "Published";
    public int Version { get; set; } = 1;
}

public sealed class PortalAiChatSession : ProjectScopedEntity
{
    public Guid PortalUserId { get; set; }
    public AiSelfServiceSessionStatus Status { get; set; } = AiSelfServiceSessionStatus.Active;
    public string Title { get; set; } = "";
    public string ContextPolicy { get; set; } = "ApprovedPublishedCustomerVisibleOnly";
    public DateTimeOffset? ClosedAt { get; set; }
}

public sealed class PortalAiChatMessage : ProjectScopedEntity
{
    public Guid SessionId { get; set; }
    public string SenderType { get; set; } = "User";
    public string Message { get; set; } = "";
    public string MaskedMessage { get; set; } = "";
    public Guid? AiRunId { get; set; }
}

public sealed class PortalNotification : ProjectScopedEntity
{
    public Guid? PortalUserId { get; set; }
    public NotificationType NotificationType { get; set; } = NotificationType.SystemAnnouncement;
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public NotificationStatus Status { get; set; } = NotificationStatus.Unread;
    public string SourceEntityType { get; set; } = "";
    public Guid? SourceEntityId { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}

public sealed class PortalComment : ProjectScopedEntity
{
    public string SourceEntityType { get; set; } = "";
    public Guid SourceEntityId { get; set; }
    public Guid? PortalUserId { get; set; }
    public string Message { get; set; } = "";
    public PortalVisibility Visibility { get; set; } = PortalVisibility.CustomerVisible;
}

public sealed class PortalAttachment : ProjectScopedEntity
{
    public string SourceEntityType { get; set; } = "";
    public Guid SourceEntityId { get; set; }
    public Guid? UploadedByPortalUserId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public string StorageRef { get; set; } = "";
    public PortalVisibility Visibility { get; set; } = PortalVisibility.CustomerVisible;
}

public sealed class PortalServiceReportShare : CustomerScopedEntity
{
    public Guid ServiceReportId { get; set; }
    public Guid? SharedWithPortalUserId { get; set; }
    public string Status { get; set; } = "Shared";
    public DateTimeOffset SharedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PortalBillingSummaryView : CustomerScopedEntity
{
    public Guid PortalUserId { get; set; }
    public Guid InvoiceDraftId { get; set; }
    public DateTimeOffset ViewedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class NotificationTemplate : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public string TemplateKey { get; set; } = "";
    public string Name { get; set; } = "";
    public NotificationType NotificationType { get; set; } = NotificationType.SystemAnnouncement;
    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;
    public NotificationRecipientType RecipientType { get; set; } = NotificationRecipientType.InternalUser;
    public bool Active { get; set; } = true;
}

public sealed class NotificationTemplateVersion : CustomerScopedEntity
{
    public Guid TemplateId { get; set; }
    public int Version { get; set; } = 1;
    public string SubjectTemplate { get; set; } = "";
    public string BodyTemplate { get; set; } = "";
    public DataClassificationLevel MaxClassification { get; set; } = DataClassificationLevel.Internal;
    public string CreatedBy { get; set; } = "system";
    public bool Active { get; set; } = true;
}

public sealed class NotificationDeliveryLog : ProjectScopedEntity
{
    public Guid NotificationId { get; set; }
    public Guid? TemplateId { get; set; }
    public int? TemplateVersion { get; set; }
    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;
    public NotificationRecipientType RecipientType { get; set; } = NotificationRecipientType.InternalUser;
    public string RecipientRef { get; set; } = "";
    public string Provider { get; set; } = "MockEmailProvider";
    public NotificationDeliveryStatus Status { get; set; } = NotificationDeliveryStatus.Pending;
    public string MaskedPayload { get; set; } = "";
    public string? ErrorMessage { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
}

public sealed class WorkflowRule : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public string RuleKey { get; set; } = "";
    public string Name { get; set; } = "";
    public WorkflowTriggerType TriggerEvent { get; set; } = WorkflowTriggerType.Manual;
    public string ConditionJson { get; set; } = "{}";
    public string ActionJson { get; set; } = "{}";
    public WorkflowRuleStatus Status { get; set; } = WorkflowRuleStatus.Active;
    public int Priority { get; set; } = 100;
}

public sealed class WorkflowRun : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid WorkflowRuleId { get; set; }
    public WorkflowTriggerType TriggerEvent { get; set; } = WorkflowTriggerType.Manual;
    public string SourceEntityType { get; set; } = "";
    public Guid? SourceEntityId { get; set; }
    public WorkflowRunStatus Status { get; set; } = WorkflowRunStatus.Queued;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class WorkflowActionLog : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid WorkflowRunId { get; set; }
    public WorkflowActionType ActionType { get; set; } = WorkflowActionType.AddTimelineEntry;
    public string TargetEntityType { get; set; } = "";
    public Guid? TargetEntityId { get; set; }
    public WorkflowRunStatus Status { get; set; } = WorkflowRunStatus.Completed;
    public string InputJson { get; set; } = "{}";
    public string OutputJson { get; set; } = "{}";
    public string? ErrorMessage { get; set; }
}

public sealed class CollaborationTask : ProjectScopedEntity
{
    public string TaskNo { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string AssigneeUserId { get; set; } = "";
    public NotificationRecipientType AssigneeType { get; set; } = NotificationRecipientType.InternalUser;
    public CollaborationTaskStatus Status { get; set; } = CollaborationTaskStatus.Open;
    public CollaborationTaskPriority Priority { get; set; } = CollaborationTaskPriority.Medium;
    public DateTimeOffset? DueAt { get; set; }
    public string SourceEntityType { get; set; } = "";
    public Guid? SourceEntityId { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public bool Escalated { get; set; }
}

public sealed class ReminderSchedule : ProjectScopedEntity
{
    public Guid? TaskId { get; set; }
    public Guid? ApprovalId { get; set; }
    public string SourceEntityType { get; set; } = "";
    public Guid? SourceEntityId { get; set; }
    public string ReminderType { get; set; } = "Generic";
    public DateTimeOffset RemindAt { get; set; } = DateTimeOffset.UtcNow;
    public ReminderStatus Status { get; set; } = ReminderStatus.Scheduled;
    public DateTimeOffset? SentAt { get; set; }
}

public sealed class EscalationEvent : ProjectScopedEntity
{
    public string SourceEntityType { get; set; } = "";
    public Guid SourceEntityId { get; set; }
    public string Reason { get; set; } = "";
    public string EscalatedToUserId { get; set; } = "";
    public EscalationStatus Status { get; set; } = EscalationStatus.Open;
    public DateTimeOffset EscalatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
}

public sealed class ActivityTimelineEntry : ProjectScopedEntity
{
    public ActivityTimelineItemType ItemType { get; set; } = ActivityTimelineItemType.System;
    public string SourceEntityType { get; set; } = "";
    public Guid? SourceEntityId { get; set; }
    public string ActorUserId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public PortalVisibility Visibility { get; set; } = PortalVisibility.InternalOnly;
    public string MetadataJson { get; set; } = "{}";
}

public sealed class ReportTemplate : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public string TemplateKey { get; set; } = "";
    public string Name { get; set; } = "";
    public ReportDocumentType ReportType { get; set; } = ReportDocumentType.ExecutiveSummary;
    public ReportOutputFormat DefaultFormat { get; set; } = ReportOutputFormat.Pdf;
    public DataClassificationLevel MaxClassification { get; set; } = DataClassificationLevel.Internal;
    public bool RequiresPermission { get; set; }
    public string? RequiredPermission { get; set; }
    public bool ApplyMaskingForExternalExport { get; set; } = true;
    public bool Active { get; set; } = true;
}

public sealed class ReportTemplateVersion : CustomerScopedEntity
{
    public Guid TemplateId { get; set; }
    public int Version { get; set; } = 1;
    public string LayoutDefinitionJson { get; set; } = "{}";
    public string ContentSchemaJson { get; set; } = "{}";
    public string CreatedBy { get; set; } = "system";
    public bool Active { get; set; } = true;
}

public sealed class ReportGenerationJob : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid TemplateId { get; set; }
    public int TemplateVersion { get; set; }
    public ReportDocumentType ReportType { get; set; } = ReportDocumentType.ExecutiveSummary;
    public ReportOutputFormat OutputFormat { get; set; } = ReportOutputFormat.Pdf;
    public ReportGenerationStatus Status { get; set; } = ReportGenerationStatus.Queued;
    public ReportVisibility Visibility { get; set; } = ReportVisibility.InternalOnly;
    public string RequestedBy { get; set; } = "";
    public DateTimeOffset DateFrom { get; set; }
    public DateTimeOffset DateTo { get; set; }
    public string FilterJson { get; set; } = "{}";
    public bool MaskingApplied { get; set; }
    public string QueueProvider { get; set; } = "Inline";
    public Guid? AiRunId { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class ReportExportFile : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid ReportJobId { get; set; }
    public Guid TemplateId { get; set; }
    public int TemplateVersion { get; set; }
    public ReportDocumentType ReportType { get; set; } = ReportDocumentType.ExecutiveSummary;
    public ReportOutputFormat OutputFormat { get; set; } = ReportOutputFormat.Pdf;
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string StorageRef { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Checksum { get; set; } = "";
    public ReportVisibility Visibility { get; set; } = ReportVisibility.InternalOnly;
    public bool MaskingApplied { get; set; }
    public bool ContainsSensitiveData { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? SharedAt { get; set; }
}

public sealed class PortalReportShare : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid ReportExportFileId { get; set; }
    public Guid? SharedWithPortalUserId { get; set; }
    public ReportVisibility Visibility { get; set; } = ReportVisibility.SharedWithCustomer;
    public string Status { get; set; } = "Shared";
    public DateTimeOffset SharedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class DashboardSnapshot : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public DashboardSnapshotType SnapshotType { get; set; } = DashboardSnapshotType.Executive;
    public DateTimeOffset DateFrom { get; set; }
    public DateTimeOffset DateTo { get; set; }
    public decimal HealthScore { get; set; }
    public decimal DeliveryScore { get; set; }
    public decimal SlaScore { get; set; }
    public decimal RiskScore { get; set; }
    public Guid? AiRunId { get; set; }
    public string AiSummary { get; set; } = "";
    public string SnapshotJson { get; set; } = "{}";
}

public sealed class IntegrationProvider : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public string ProviderKey { get; set; } = "";
    public string Name { get; set; } = "";
    public IntegrationProviderCategory Category { get; set; } = IntegrationProviderCategory.Other;
    public string BaseUrl { get; set; } = "";
    public string DocumentationUrl { get; set; } = "";
    public bool SupportsInboundWebhook { get; set; }
    public bool SupportsOutboundWebhook { get; set; }
    public bool SupportsSignatureVerification { get; set; }
    public bool SupportsRetry { get; set; } = true;
    public int DefaultTimeoutSeconds { get; set; } = 30;
    public string ConfigJson { get; set; } = "{}";
    public bool Active { get; set; } = true;
}

public sealed class IntegrationEndpoint : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid ProviderId { get; set; }
    public string EndpointKey { get; set; } = "";
    public string Name { get; set; } = "";
    public IntegrationDirection Direction { get; set; } = IntegrationDirection.Outbound;
    public string HttpMethod { get; set; } = "POST";
    public string PathOrUrl { get; set; } = "";
    public IntegrationAuthType AuthType { get; set; } = IntegrationAuthType.SecretRefToken;
    public string? SecretRef { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public DataClassificationLevel MaxDataClassification { get; set; } = DataClassificationLevel.Internal;
    public bool MaskOutboundPayloads { get; set; } = true;
    public bool Active { get; set; } = true;
}

public sealed class IntegrationPayloadMapping : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid ProviderId { get; set; }
    public Guid? EndpointId { get; set; }
    public string MappingKey { get; set; } = "";
    public string SourceSystem { get; set; } = "";
    public string TargetSystem { get; set; } = "";
    public IntegrationEventType EventType { get; set; } = IntegrationEventType.Manual;
    public string MappingJson { get; set; } = "{}";
    public bool Active { get; set; } = true;
}

public sealed class IntegrationEventSubscription : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid ProviderId { get; set; }
    public Guid? EndpointId { get; set; }
    public IntegrationEventType EventType { get; set; } = IntegrationEventType.Manual;
    public string SubscriptionKey { get; set; } = "";
    public string FilterJson { get; set; } = "{}";
    public bool Active { get; set; } = true;
}

public sealed class WebhookOutboundSubscription : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid ProviderId { get; set; }
    public Guid? EndpointId { get; set; }
    public IntegrationEventType EventType { get; set; } = IntegrationEventType.Manual;
    public string TargetUrl { get; set; } = "";
    public string? SecretRef { get; set; }
    public WebhookSignatureMode SignatureMode { get; set; } = WebhookSignatureMode.MockHmac;
    public int MaxRetryAttempts { get; set; } = 3;
    public int RetryBackoffSeconds { get; set; } = 30;
    public int TimeoutSeconds { get; set; } = 30;
    public bool Active { get; set; } = true;
}

public sealed class ApiGatewayRoute : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public string RouteKey { get; set; } = "";
    public string PublicPath { get; set; } = "";
    public string InternalTarget { get; set; } = "";
    public string HttpMethod { get; set; } = "POST";
    public string AllowedExternalSystem { get; set; } = "";
    public string RequiredPermission { get; set; } = "integration.gateway.invoke";
    public string TokenSecretRef { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 30;
    public DataClassificationLevel MaxDataClassification { get; set; } = DataClassificationLevel.Confidential;
    public string AccessPolicyJson { get; set; } = "{}";
    public bool Active { get; set; } = true;
}

public sealed class IntegrationAutomationTrigger : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid? ProviderId { get; set; }
    public string TriggerKey { get; set; } = "";
    public IntegrationEventType EventType { get; set; } = IntegrationEventType.Manual;
    public IntegrationAutomationActionType ActionType { get; set; } = IntegrationAutomationActionType.CreateTask;
    public string ConditionJson { get; set; } = "{}";
    public string ActionJson { get; set; } = "{}";
    public bool CreateOnFailureOnly { get; set; } = true;
    public bool Active { get; set; } = true;
}

public sealed class IntegrationRun : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid? ProviderId { get; set; }
    public Guid? EndpointId { get; set; }
    public Guid? EventSubscriptionId { get; set; }
    public Guid? WebhookSubscriptionId { get; set; }
    public Guid? ApiGatewayRouteId { get; set; }
    public IntegrationDirection Direction { get; set; } = IntegrationDirection.Outbound;
    public IntegrationEventType EventType { get; set; } = IntegrationEventType.Manual;
    public IntegrationRunStatus Status { get; set; } = IntegrationRunStatus.Queued;
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");
    public string TraceId { get; set; } = Guid.NewGuid().ToString("N");
    public int Attempt { get; set; } = 1;
    public int MaxAttempts { get; set; } = 1;
    public int TimeoutSeconds { get; set; } = 30;
    public string RequestSummary { get; set; } = "";
    public string MaskedPayload { get; set; } = "";
    public string ResponseSummary { get; set; } = "";
    public string? ErrorMessage { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? NextRetryAt { get; set; }
}

public sealed class IntegrationRunLog : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid IntegrationRunId { get; set; }
    public string Level { get; set; } = "Info";
    public string Message { get; set; } = "";
    public string MaskedPayload { get; set; } = "";
}

public sealed class DevOpsRepository : ProjectScopedEntity
{
    public DevOpsProviderKind Provider { get; set; } = DevOpsProviderKind.MockGit;
    public string ProviderRepositoryId { get; set; } = "";
    public string Name { get; set; } = "";
    public string RepoUrl { get; set; } = "";
    public string DefaultBranch { get; set; } = "main";
    public string SecretRef { get; set; } = "";
    public bool ProtectMainBranch { get; set; } = true;
    public bool RequirePullRequestReview { get; set; } = true;
    public bool RequireCiBeforeMerge { get; set; } = true;
    public string Status { get; set; } = "Active";
}

public sealed class DevOpsBranch : ProjectScopedEntity
{
    public Guid RepositoryId { get; set; }
    public string BranchName { get; set; } = "";
    public string SourceBranch { get; set; } = "main";
    public string CreatedBy { get; set; } = "system";
    public bool CreatedByAi { get; set; }
    public string Status { get; set; } = "Active";
}

public sealed class DevOpsPullRequest : ProjectScopedEntity
{
    public Guid RepositoryId { get; set; }
    public string ExternalPrRef { get; set; } = "";
    public string SourceBranch { get; set; } = "";
    public string TargetBranch { get; set; } = "main";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public PullRequestStatus Status { get; set; } = PullRequestStatus.Open;
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Medium;
    public string ChangeAreasCsv { get; set; } = "Other";
    public Guid? AiRunId { get; set; }
    public Guid? ApprovalRequestId { get; set; }
    public Guid? BuildRunId { get; set; }
    public Guid? TestRunId { get; set; }
    public Guid? CodeScanRunId { get; set; }
    public string? MergeCommitRef { get; set; }
    public string CreatedBy { get; set; } = "system";
    public bool CreatedByAi { get; set; }
    public DateTimeOffset? MergedAt { get; set; }
}

public sealed class CodeReviewRecord : ProjectScopedEntity
{
    public Guid PullRequestId { get; set; }
    public string ReviewerUserId { get; set; } = "";
    public CodeReviewDecision Decision { get; set; } = CodeReviewDecision.Pending;
    public string Comments { get; set; } = "";
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Medium;
    public bool RequiresSpecialApproval { get; set; }
    public bool CreatedByAi { get; set; }
    public Guid? AiRunId { get; set; }
}

public sealed class AiCodeAnalysis : ProjectScopedEntity
{
    public Guid RepositoryId { get; set; }
    public Guid? PullRequestId { get; set; }
    public Guid AiRunId { get; set; }
    public string BranchName { get; set; } = "";
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Medium;
    public string ChangeAreasCsv { get; set; } = "Other";
    public string Summary { get; set; } = "";
    public string FindingsJson { get; set; } = "[]";
    public Guid? PatchProposalId { get; set; }
}

public sealed class AiPatchProposal : ProjectScopedEntity
{
    public Guid RepositoryId { get; set; }
    public Guid? PullRequestId { get; set; }
    public Guid AiRunId { get; set; }
    public string BranchName { get; set; } = "";
    public string Title { get; set; } = "";
    public string DiffText { get; set; } = "";
    public int DiffSizeBytes { get; set; }
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Medium;
    public string ChangeAreasCsv { get; set; } = "Other";
    public string Status { get; set; } = "Proposed";
}

public sealed class CiCdPipeline : ProjectScopedEntity
{
    public Guid RepositoryId { get; set; }
    public string PipelineKey { get; set; } = "";
    public string Name { get; set; } = "";
    public DevOpsProviderKind Provider { get; set; } = DevOpsProviderKind.MockGit;
    public string ConfigPath { get; set; } = ".hrm-aiops/pipeline.yml";
    public int TimeoutSeconds { get; set; } = 600;
    public bool Active { get; set; } = true;
}

public sealed class PipelineRun : ProjectScopedEntity
{
    public Guid RepositoryId { get; set; }
    public Guid PipelineId { get; set; }
    public Guid? PullRequestId { get; set; }
    public PipelineRunType RunType { get; set; } = PipelineRunType.Build;
    public PipelineRunStatus Status { get; set; } = PipelineRunStatus.Queued;
    public string Summary { get; set; } = "";
    public string? LogsRef { get; set; }
    public string? ArtifactRef { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class DeploymentPackage : ProjectScopedEntity
{
    public Guid RepositoryId { get; set; }
    public Guid PullRequestId { get; set; }
    public Guid? BuildRunId { get; set; }
    public Guid? TestRunId { get; set; }
    public Guid? CodeScanRunId { get; set; }
    public string PackageNo { get; set; } = "";
    public string Version { get; set; } = "";
    public DeploymentPackageStatus Status { get; set; } = DeploymentPackageStatus.Draft;
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Medium;
    public string ArtifactRef { get; set; } = "";
    public string DiffSummary { get; set; } = "";
    public Guid? ApprovalRequestId { get; set; }
    public DateTimeOffset? ReadyAt { get; set; }
}

public sealed class SourceCodeSnapshot : ProjectScopedEntity
{
    public Guid RepositoryId { get; set; }
    public string BranchName { get; set; } = "";
    public string CommitSha { get; set; } = "";
    public string SnapshotNo { get; set; } = "";
    public string MetadataJson { get; set; } = "{}";
    public string DiffSummary { get; set; } = "";
    public string DiffTextPreview { get; set; } = "";
    public int DiffSizeBytes { get; set; }
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class DevOpsRun : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid? RepositoryId { get; set; }
    public Guid? PullRequestId { get; set; }
    public Guid? PipelineRunId { get; set; }
    public DevOpsRunType RunType { get; set; } = DevOpsRunType.RepositorySync;
    public DevOpsRunStatus Status { get; set; } = DevOpsRunStatus.Queued;
    public string ActorUserId { get; set; } = "system";
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");
    public string TraceId { get; set; } = Guid.NewGuid().ToString("N");
    public string MaskedInput { get; set; } = "";
    public string Summary { get; set; } = "";
    public string? ErrorMessage { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class DevOpsRunLog : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid DevOpsRunId { get; set; }
    public string Level { get; set; } = "Info";
    public string Message { get; set; } = "";
    public string MaskedPayload { get; set; } = "";
}

public sealed class AiCodeGovernancePolicy : ProjectScopedEntity
{
    public Guid? RepositoryId { get; set; }
    public string PolicyKey { get; set; } = "";
    public string ProtectedBranchesCsv { get; set; } = "main,master";
    public bool RequireHumanReview { get; set; } = true;
    public bool BlockDirectMainMerge { get; set; } = true;
    public bool BlockAiProductionDeploy { get; set; } = true;
    public bool HighRiskRequiresApproval { get; set; } = true;
    public string SpecialApprovalAreasCsv { get; set; } = "Payroll,Permission,Security,Integration,ProductionDeployment";
    public int MaxDiffBytes { get; set; } = 12000;
    public bool Active { get; set; } = true;
}

public sealed class TelemetrySource : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid? EnvironmentId { get; set; }
    public Guid? ConnectorId { get; set; }
    public Guid? ProductionReleasePackageId { get; set; }
    public Guid? ProductionDeploymentRunId { get; set; }
    public string SourceKey { get; set; } = "";
    public string Name { get; set; } = "";
    public TelemetrySourceType SourceType { get; set; } = TelemetrySourceType.PlatformApi;
    public string EndpointRef { get; set; } = "";
    public string Provider { get; set; } = "MockTelemetryProvider";
    public int PollIntervalSeconds { get; set; } = 60;
    public int TimeoutSeconds { get; set; } = 10;
    public bool MaskLogs { get; set; } = true;
    public bool Active { get; set; } = true;
}

public sealed class RuntimeTelemetrySample : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid TelemetrySourceId { get; set; }
    public Guid? EnvironmentId { get; set; }
    public Guid? ConnectorId { get; set; }
    public Guid? ProductionReleasePackageId { get; set; }
    public Guid? ProductionDeploymentRunId { get; set; }
    public TelemetrySignalType SignalType { get; set; } = TelemetrySignalType.HealthCheck;
    public TelemetryHealthStatus HealthStatus { get; set; } = TelemetryHealthStatus.Unknown;
    public string MetricName { get; set; } = "";
    public decimal? MetricValue { get; set; }
    public string Unit { get; set; } = "";
    public int? ApiLatencyMs { get; set; }
    public decimal? UptimePercent { get; set; }
    public string Summary { get; set; } = "";
    public string MaskedPayloadJson { get; set; } = "{}";
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");
    public string TraceId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset ObservedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TelemetryLogSummary : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid TelemetrySourceId { get; set; }
    public string LogWindow { get; set; } = "5m";
    public int TotalLines { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public string MaskedSummary { get; set; } = "";
    public string TopErrorsJson { get; set; } = "[]";
    public DateTimeOffset ObservedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class MonitoringRule : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid? TelemetrySourceId { get; set; }
    public string RuleKey { get; set; } = "";
    public string Name { get; set; } = "";
    public TelemetrySignalType SignalType { get; set; } = TelemetrySignalType.HealthCheck;
    public string MetricName { get; set; } = "";
    public MonitoringRuleOperator Operator { get; set; } = MonitoringRuleOperator.GreaterThan;
    public decimal? ThresholdValue { get; set; }
    public string MatchText { get; set; } = "";
    public AlertSeverity Severity { get; set; } = AlertSeverity.Warning;
    public bool AutoCreateIncident { get; set; } = true;
    public bool AutoCreateIssue { get; set; }
    public bool Active { get; set; } = true;
}

public sealed class AlertRule : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid? MonitoringRuleId { get; set; }
    public string AlertKey { get; set; } = "";
    public AlertSeverity MinimumSeverity { get; set; } = AlertSeverity.Warning;
    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;
    public string RecipientRef { get; set; } = "sre.oncall";
    public bool CreateNotification { get; set; } = true;
    public bool CreateEscalationForCritical { get; set; } = true;
    public bool Active { get; set; } = true;
}

public sealed class AlertEvent : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid? MonitoringRuleId { get; set; }
    public Guid? AlertRuleId { get; set; }
    public Guid? TelemetrySampleId { get; set; }
    public Guid? IncidentId { get; set; }
    public AlertSeverity Severity { get; set; } = AlertSeverity.Warning;
    public AlertStatus Status { get; set; } = AlertStatus.Open;
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");
    public string TraceId { get; set; } = Guid.NewGuid().ToString("N");
    public string MaskedPayloadJson { get; set; } = "{}";
    public DateTimeOffset TriggeredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}

public sealed class IncidentRecord : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid? EnvironmentId { get; set; }
    public Guid? ConnectorId { get; set; }
    public Guid? ProductionReleasePackageId { get; set; }
    public Guid? ProductionDeploymentRunId { get; set; }
    public Guid? IssueId { get; set; }
    public Guid? SlaPolicyId { get; set; }
    public Guid? AlertEventId { get; set; }
    public string IncidentNo { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public IncidentStatus Status { get; set; } = IncidentStatus.Open;
    public IncidentPriority Priority { get; set; } = IncidentPriority.P3;
    public AlertSeverity Severity { get; set; } = AlertSeverity.Warning;
    public string ImpactSummary { get; set; } = "";
    public string CurrentMitigation { get; set; } = "";
    public Guid? AiRunId { get; set; }
    public DateTimeOffset DetectedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? MitigatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}

public sealed class IncidentAction : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid IncidentId { get; set; }
    public IncidentActionType ActionType { get; set; } = IncidentActionType.ManualNote;
    public string ActorUserId { get; set; } = "system";
    public string Summary { get; set; } = "";
    public string ResultJson { get; set; } = "{}";
}

public sealed class IncidentSlaBinding : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid IncidentId { get; set; }
    public Guid SlaPolicyId { get; set; }
    public SlaStatus Status { get; set; } = SlaStatus.OnTrack;
    public DateTimeOffset ResponseDueAt { get; set; }
    public DateTimeOffset ResolutionDueAt { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}

public sealed class AiIncidentDiagnosis : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid IncidentId { get; set; }
    public Guid AiRunId { get; set; }
    public string RootCauseHypothesis { get; set; } = "";
    public string RecommendedActions { get; set; } = "";
    public string EvidenceSummary { get; set; } = "";
    public decimal ConfidenceScore { get; set; }
    public bool ProductionFixExecuted { get; set; }
}

public sealed class PostIncidentReview : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid IncidentId { get; set; }
    public Guid? AiRunId { get; set; }
    public Guid? KnowledgeArticleId { get; set; }
    public string ReviewNo { get; set; } = "";
    public string Summary { get; set; } = "";
    public string TimelineJson { get; set; } = "[]";
    public string PreventiveActions { get; set; } = "";
    public WorkStatus Status { get; set; } = WorkStatus.Draft;
    public string CreatedBy { get; set; } = "system";
}

public sealed class DataImportTemplate : ProjectScopedEntity
{
    public string TemplateKey { get; set; } = "";
    public string Name { get; set; } = "";
    public HrmDataDomain Domain { get; set; } = HrmDataDomain.Employee;
    public ImportFileType DefaultFileType { get; set; } = ImportFileType.Csv;
    public DataClassificationLevel MaxClassification { get; set; } = DataClassificationLevel.Confidential;
    public ImportTemplateStatus Status { get; set; } = ImportTemplateStatus.Draft;
    public int CurrentVersion { get; set; } = 1;
    public bool Active { get; set; } = true;
}

public sealed class DataImportTemplateVersion : ProjectScopedEntity
{
    public Guid TemplateId { get; set; }
    public int Version { get; set; } = 1;
    public string SchemaJson { get; set; } = "{}";
    public string SampleFileRef { get; set; } = "";
    public string CreatedBy { get; set; } = "system";
    public bool Active { get; set; } = true;
}

public sealed class DataImportFile : ProjectScopedEntity
{
    public Guid? EnvironmentId { get; set; }
    public Guid? TemplateId { get; set; }
    public string FileRef { get; set; } = "";
    public string FileName { get; set; } = "";
    public ImportFileType FileType { get; set; } = ImportFileType.Csv;
    public long SizeBytes { get; set; }
    public int RowCount { get; set; }
    public DataClassificationLevel Classification { get; set; } = DataClassificationLevel.Confidential;
    public string UploadedBy { get; set; } = "system";
    public string MaskedPreviewJson { get; set; } = "[]";
    public string Status { get; set; } = "Uploaded";
}

public sealed class DataColumnMapping : ProjectScopedEntity
{
    public Guid TemplateId { get; set; }
    public int TemplateVersion { get; set; } = 1;
    public int MappingVersion { get; set; } = 1;
    public string MappingKey { get; set; } = "";
    public string SourceColumn { get; set; } = "";
    public string TargetEntity { get; set; } = "";
    public string TargetField { get; set; } = "";
    public string TransformExpression { get; set; } = "";
    public DataClassificationLevel DataClassification { get; set; } = DataClassificationLevel.Internal;
    public DataMappingStatus Status { get; set; } = DataMappingStatus.Active;
}

public sealed class DataValidationRule : ProjectScopedEntity
{
    public Guid? TemplateId { get; set; }
    public string RuleKey { get; set; } = "";
    public string Name { get; set; } = "";
    public HrmDataDomain Domain { get; set; } = HrmDataDomain.Employee;
    public string TargetField { get; set; } = "";
    public ValidationRuleType RuleType { get; set; } = ValidationRuleType.Required;
    public string ExpressionJson { get; set; } = "{}";
    public ValidationIssueSeverity Severity { get; set; } = ValidationIssueSeverity.Error;
    public bool Active { get; set; } = true;
}

public sealed class DataImportBatch : ProjectScopedEntity
{
    public Guid EnvironmentId { get; set; }
    public Guid? ConnectorId { get; set; }
    public Guid TemplateId { get; set; }
    public int TemplateVersion { get; set; } = 1;
    public Guid ImportFileId { get; set; }
    public string BatchNo { get; set; } = "";
    public HrmDataDomain Domain { get; set; } = HrmDataDomain.Employee;
    public ImportBatchStatus Status { get; set; } = ImportBatchStatus.Draft;
    public string RequestedBy { get; set; } = "system";
    public bool DryRunRequired { get; set; } = true;
    public Guid? PreImportSnapshotId { get; set; }
    public Guid? DryRunId { get; set; }
    public Guid? ApplyRunId { get; set; }
    public Guid? ReconciliationReportId { get; set; }
    public Guid? SignOffId { get; set; }
}

public sealed class DataImportRun : ProjectScopedEntity
{
    public Guid BatchId { get; set; }
    public Guid EnvironmentId { get; set; }
    public Guid? ConnectorId { get; set; }
    public string RunNo { get; set; } = "";
    public DataImportRunType RunType { get; set; } = DataImportRunType.DryRun;
    public DataImportRunStatus Status { get; set; } = DataImportRunStatus.Queued;
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int ErrorRows { get; set; }
    public int DuplicateRows { get; set; }
    public string Summary { get; set; } = "";
    public Guid? AiRunId { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class DataValidationIssue : ProjectScopedEntity
{
    public Guid ImportRunId { get; set; }
    public int RowNumber { get; set; }
    public string FieldName { get; set; } = "";
    public ValidationIssueSeverity Severity { get; set; } = ValidationIssueSeverity.Error;
    public string ErrorCode { get; set; } = "";
    public string Message { get; set; } = "";
    public string MaskedValuePreview { get; set; } = "";
    public string? DuplicateKey { get; set; }
}

public sealed class DataReconciliationReport : ProjectScopedEntity
{
    public Guid BatchId { get; set; }
    public Guid ImportRunId { get; set; }
    public string ReportNo { get; set; } = "";
    public ReconciliationStatus Status { get; set; } = ReconciliationStatus.Pending;
    public int SourceRows { get; set; }
    public int ImportedRows { get; set; }
    public int MatchedRows { get; set; }
    public int MismatchedRows { get; set; }
    public int MissingRows { get; set; }
    public string Summary { get; set; } = "";
    public string ReportFileRef { get; set; } = "";
}

public sealed class DataMigrationReport : ProjectScopedEntity
{
    public Guid BatchId { get; set; }
    public Guid? ReconciliationReportId { get; set; }
    public string ReportNo { get; set; } = "";
    public HrmDataDomain Domain { get; set; } = HrmDataDomain.Employee;
    public string Summary { get; set; } = "";
    public string FileRef { get; set; } = "";
}

public sealed class DataSignOff : ProjectScopedEntity
{
    public Guid BatchId { get; set; }
    public Guid? ReconciliationReportId { get; set; }
    public string SignOffNo { get; set; } = "";
    public DataSignOffStatus Status { get; set; } = DataSignOffStatus.Pending;
    public string SignedBy { get; set; } = "";
    public string Role { get; set; } = "";
    public string Comment { get; set; } = "";
    public DateTimeOffset? SignedAt { get; set; }
}

public sealed class AiDataMigrationAssistance : ProjectScopedEntity
{
    public Guid? BatchId { get; set; }
    public Guid? TemplateId { get; set; }
    public Guid AiRunId { get; set; }
    public AiDataAssistanceType AssistanceType { get; set; } = AiDataAssistanceType.MappingSuggestion;
    public string Summary { get; set; } = "";
    public string SuggestionJson { get; set; } = "{}";
    public bool AppliedByUser { get; set; }
}

public sealed class TrainingPlan : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public string PlanNo { get; set; } = "";
    public string Name { get; set; } = "";
    public string Module { get; set; } = "";
    public string TargetRole { get; set; } = "";
    public WorkStatus Status { get; set; } = WorkStatus.Draft;
    public string OwnerUserId { get; set; } = "system";
    public DateTimeOffset? StartAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }
}

public sealed class LearningPath : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid? TrainingPlanId { get; set; }
    public string PathKey { get; set; } = "";
    public string Name { get; set; } = "";
    public string Module { get; set; } = "";
    public string RoleKey { get; set; } = "";
    public decimal RequiredPassScore { get; set; } = 80;
    public TrainingContentStatus Status { get; set; } = TrainingContentStatus.Draft;
    public bool Published { get; set; }
}

public sealed class TrainingMaterial : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public string MaterialKey { get; set; } = "";
    public string Title { get; set; } = "";
    public string Module { get; set; } = "";
    public string RoleKey { get; set; } = "";
    public TrainingMaterialType MaterialType { get; set; } = TrainingMaterialType.Guide;
    public string ContentRef { get; set; } = "";
    public string Summary { get; set; } = "";
    public DataClassificationLevel Classification { get; set; } = DataClassificationLevel.Internal;
    public TrainingContentStatus Status { get; set; } = TrainingContentStatus.Draft;
    public bool Published { get; set; }
    public string ApprovedBy { get; set; } = "";
    public DateTimeOffset? PublishedAt { get; set; }
}

public sealed class TrainingLesson : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid? LearningPathId { get; set; }
    public Guid? MaterialId { get; set; }
    public string LessonKey { get; set; } = "";
    public string Title { get; set; } = "";
    public string Module { get; set; } = "";
    public string RoleKey { get; set; } = "";
    public int Sequence { get; set; }
    public int EstimatedMinutes { get; set; }
    public TrainingContentStatus Status { get; set; } = TrainingContentStatus.Draft;
    public bool Published { get; set; }
}

public sealed class TrainingQuiz : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid LessonId { get; set; }
    public string QuizKey { get; set; } = "";
    public string Title { get; set; } = "";
    public decimal PassScore { get; set; } = 80;
    public string QuestionsJson { get; set; } = "[]";
    public TrainingContentStatus Status { get; set; } = TrainingContentStatus.Draft;
    public bool Published { get; set; }
}

public sealed class AssessmentResult : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid QuizId { get; set; }
    public Guid? LearningPathId { get; set; }
    public string UserId { get; set; } = "";
    public string RoleKey { get; set; } = "";
    public int AttemptNo { get; set; } = 1;
    public decimal Score { get; set; }
    public bool Passed { get; set; }
    public AssessmentResultStatus Status { get; set; } = AssessmentResultStatus.Submitted;
    public string AnswersJson { get; set; } = "{}";
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TrainingSession : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid? TrainingPlanId { get; set; }
    public Guid? LearningPathId { get; set; }
    public string SessionNo { get; set; } = "";
    public string Title { get; set; } = "";
    public TrainingDeliveryMode DeliveryMode { get; set; } = TrainingDeliveryMode.InstructorLed;
    public string TrainerUserId { get; set; } = "";
    public DateTimeOffset ScheduledStartAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ScheduledEndAt { get; set; } = DateTimeOffset.UtcNow.AddHours(1);
    public WorkStatus Status { get; set; } = WorkStatus.Active;
}

public sealed class TrainingAttendance : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid SessionId { get; set; }
    public string UserId { get; set; } = "";
    public string RoleKey { get; set; } = "";
    public TrainingAttendanceStatus Status { get; set; } = TrainingAttendanceStatus.Invited;
    public DateTimeOffset? CheckedInAt { get; set; }
}

public sealed class UserLearningProgress : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid LearningPathId { get; set; }
    public Guid? LessonId { get; set; }
    public string UserId { get; set; } = "";
    public string RoleKey { get; set; } = "";
    public LearningProgressStatus Status { get; set; } = LearningProgressStatus.NotStarted;
    public decimal PercentComplete { get; set; }
    public DateTimeOffset LastActivityAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class UserCertification : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid LearningPathId { get; set; }
    public string CertificateNo { get; set; } = "";
    public string UserId { get; set; } = "";
    public string RoleKey { get; set; } = "";
    public decimal Score { get; set; }
    public CertificationStatus Status { get; set; } = CertificationStatus.Pending;
    public DateTimeOffset? IssuedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class AdoptionMetric : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public string Module { get; set; } = "";
    public string RoleKey { get; set; } = "";
    public string? UserId { get; set; }
    public AdoptionSignalType SignalType { get; set; } = AdoptionSignalType.FeatureUse;
    public decimal AdoptionScore { get; set; }
    public int ActiveUsers { get; set; }
    public int TotalUsers { get; set; }
    public int UsageCount { get; set; }
    public DateTimeOffset MeasuredAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TrainingGap : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public string GapNo { get; set; } = "";
    public TrainingGapSourceType SourceType { get; set; } = TrainingGapSourceType.Manual;
    public Guid? SourceEntityId { get; set; }
    public string Module { get; set; } = "";
    public string RoleKey { get; set; } = "";
    public string? UserId { get; set; }
    public ValidationIssueSeverity Severity { get; set; } = ValidationIssueSeverity.Warning;
    public string Summary { get; set; } = "";
    public string RecommendedAction { get; set; } = "";
    public TrainingGapStatus Status { get; set; } = TrainingGapStatus.Open;
}

public sealed class AiTrainingAssistantExchange : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public Guid AiRunId { get; set; }
    public string UserId { get; set; } = "";
    public string RoleKey { get; set; } = "";
    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";
    public string SourceRefsJson { get; set; } = "[]";
    public AiTrainingAssistantStatus Status { get; set; } = AiTrainingAssistantStatus.Answered;
}

public sealed class TraceLink : ProjectScopedEntity
{
    public string FromEntityType { get; set; } = "";
    public Guid FromEntityId { get; set; }
    public string ToEntityType { get; set; } = "";
    public Guid ToEntityId { get; set; }
    public string RelationType { get; set; } = "Generated";
}

public sealed class AuditLog : CustomerScopedEntity
{
    public Guid? ProjectId { get; set; }
    public string? ActorUserId { get; set; }
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public Guid EntityId { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");
}
