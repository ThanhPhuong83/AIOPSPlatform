using HrmAiOps.Domain.Core;

namespace HrmAiOps.Application.Abstractions;

public interface IAuditWriter
{
    AuditLog Write(Guid customerId, Guid? projectId, string action, string entityType, Guid entityId, object? after = null);
}

public interface IAiRunRecorder
{
    AiRun Start(Guid customerId, Guid? projectId, string runType, string? inputSummary);
    AiRun Start(Guid customerId, Guid? projectId, string runType, string? inputSummary, string? inputRef, AiPromptTemplateVersion? promptVersion);
    void Complete(AiRun run, string outputSummary, string? outputRef = null);
    void Complete(AiRun run, string outputSummary, string? outputRef, string rawOutputJson, IReadOnlyList<string> validationErrors);
    void Fail(AiRun run, string errorMessage);
}

public interface IAiProvider
{
    Task<string> GenerateAsync(string runType, string input, CancellationToken cancellationToken);
}

public sealed record AiProviderRequest(
    string TaskType,
    string SystemPrompt,
    string UserPrompt,
    string OutputJsonSchema,
    string ContextJson);

public sealed record AiProviderResponse(
    string Provider,
    string Model,
    string OutputJson);

public interface IStructuredAiProvider
{
    Task<AiProviderResponse> ExecuteAsync(AiProviderRequest request, CancellationToken cancellationToken);
}

public sealed record AiContext(
    Guid CustomerId,
    Guid ProjectId,
    string SourceEntityType,
    Guid SourceEntityId,
    string ContextJson,
    string InputSummary);

public interface IAiContextBuilder
{
    Task<AiContext> BuildAsync(Guid customerId, Guid projectId, string sourceEntityType, Guid sourceEntityId, CancellationToken cancellationToken);
}

public interface IDataMaskingService
{
    string Mask(string input);
}

public sealed record NotificationDeliveryRequest(
    NotificationChannel Channel,
    NotificationRecipientType RecipientType,
    string RecipientRef,
    string MaskedSubject,
    string MaskedBody);

public sealed record NotificationDeliveryResult(
    string Provider,
    NotificationDeliveryStatus Status,
    string? ErrorMessage,
    DateTimeOffset? DeliveredAt);

public interface INotificationDeliveryProvider
{
    NotificationDeliveryResult Deliver(NotificationDeliveryRequest request);
}

public sealed record StructuredOutputValidationResult(bool IsValid, string Title, string Content, RiskLevel? RiskLevel, IReadOnlyList<string> Errors);

public interface IStructuredOutputValidator
{
    StructuredOutputValidationResult Validate(AiTaskType taskType, string outputJson);
}

public sealed record AiTaskExecutionRequest(Guid CustomerId, Guid ProjectId, AiTaskType TaskType, string SourceEntityType, Guid SourceEntityId);

public interface IAiTaskExecutor
{
    Task<AiProposal> ExecuteAsync(AiTaskExecutionRequest request, CancellationToken cancellationToken);
}

public interface ISecretResolver
{
    Task<string> ResolveAsync(string secretRef, CancellationToken cancellationToken);
}

public sealed record ControlledApplyRequest(
    Guid CustomerId,
    Guid ProjectId,
    Guid EnvironmentId,
    Guid? ConnectorId,
    string SourceType,
    Guid SourceId,
    string RequestedBy);

public interface IControlledApplyService
{
    Task<ApplyRun> DryRunAsync(ControlledApplyRequest request, CancellationToken cancellationToken);
    Task<ApplyRun> ApplyAsync(Guid customerId, Guid projectId, Guid applyRunId, string requestedBy, CancellationToken cancellationToken);
}

public interface IBackgroundJobDispatcher
{
    Task EnqueueAsync(string jobType, object payload, CancellationToken cancellationToken);
}

public sealed record ReportCatalogItem(
    Guid TemplateId,
    string TemplateKey,
    string Name,
    ReportDocumentType ReportType,
    ReportOutputFormat DefaultFormat,
    int ActiveVersion,
    DataClassificationLevel MaxClassification,
    bool RequiresPermission,
    string? RequiredPermission);

public sealed record ReportGenerationRequest(
    Guid CustomerId,
    Guid? ProjectId,
    Guid? TemplateId,
    ReportDocumentType ReportType,
    ReportOutputFormat OutputFormat,
    ReportVisibility Visibility,
    DateTimeOffset DateFrom,
    DateTimeOffset DateTo,
    string RequestedBy,
    bool ExternalExport);

public sealed record ReportGenerationResult(ReportGenerationJob Job, ReportExportFile File);

public sealed record ReportingDashboardFilter(Guid CustomerId, Guid? ProjectId, DateTimeOffset DateFrom, DateTimeOffset DateTo);

public interface IReportingService
{
    IReadOnlyList<ReportCatalogItem> GetCatalog(Guid customerId, Guid? projectId);
    object GetExecutiveDashboard(ReportingDashboardFilter filter);
    object GetProjectDashboard(ReportingDashboardFilter filter);
    object GetCustomerHealthDashboard(ReportingDashboardFilter filter);
    Task<ReportGenerationResult> GenerateAsync(ReportGenerationRequest request, CancellationToken cancellationToken);
    Task<DashboardSnapshot> GenerateAiSummaryAsync(ReportingDashboardFilter filter, string requestedBy, CancellationToken cancellationToken);
}

public sealed record IntegrationDashboardFilter(Guid CustomerId, Guid? ProjectId);

public sealed record IntegrationActionRequest(
    Guid CustomerId,
    Guid? ProjectId,
    Guid EndpointId,
    IntegrationEventType EventType,
    string PayloadJson,
    string RequestedBy,
    string? CorrelationId);

public sealed record InboundWebhookRequest(
    Guid CustomerId,
    Guid? ProjectId,
    Guid ProviderId,
    Guid? EndpointId,
    IntegrationEventType EventType,
    string PayloadJson,
    string? Signature,
    string RequestedBy,
    string? CorrelationId);

public sealed record GatewayInvocationRequest(
    Guid CustomerId,
    Guid? ProjectId,
    Guid RouteId,
    string ExternalSystem,
    string TokenSecretRef,
    string PayloadJson,
    string RequestedBy,
    string? CorrelationId);

public interface IIntegrationHubService
{
    object GetDashboard(IntegrationDashboardFilter filter);
    Task<IntegrationRun> ExecuteOutboundAsync(IntegrationActionRequest request, CancellationToken cancellationToken);
    Task<IntegrationRun> ReceiveWebhookAsync(InboundWebhookRequest request, CancellationToken cancellationToken);
    Task<IntegrationRun> InvokeGatewayAsync(GatewayInvocationRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<IntegrationRun>> ProcessRetriesAsync(Guid customerId, Guid? projectId, CancellationToken cancellationToken);
}

public sealed record DevOpsDashboardFilter(Guid CustomerId, Guid ProjectId, DateTimeOffset? DateFrom, DateTimeOffset? DateTo);

public sealed record DevOpsRepositoryRegistrationRequest(
    Guid CustomerId,
    Guid ProjectId,
    DevOpsProviderKind Provider,
    string Name,
    string ProviderRepositoryId,
    string RepoUrl,
    string DefaultBranch,
    string SecretRef,
    string RequestedBy);

public sealed record DevOpsBranchRequest(
    Guid CustomerId,
    Guid ProjectId,
    Guid RepositoryId,
    string BranchName,
    string SourceBranch,
    bool CreatedByAi,
    string RequestedBy);

public sealed record DevOpsPullRequestCreateRequest(
    Guid CustomerId,
    Guid ProjectId,
    Guid RepositoryId,
    string SourceBranch,
    string TargetBranch,
    string Title,
    string Description,
    RiskLevel RiskLevel,
    string ChangeAreasCsv,
    bool CreatedByAi,
    string RequestedBy);

public sealed record AiCodeAnalysisRequest(
    Guid CustomerId,
    Guid ProjectId,
    Guid RepositoryId,
    Guid? PullRequestId,
    string BranchName,
    string DiffText,
    string RequestedBy);

public sealed record AiPatchProposalRequest(
    Guid CustomerId,
    Guid ProjectId,
    Guid RepositoryId,
    Guid? PullRequestId,
    string BranchName,
    string Intent,
    string RequestedBy);

public sealed record PipelineRunRequest(
    Guid CustomerId,
    Guid ProjectId,
    Guid RepositoryId,
    Guid PipelineId,
    Guid? PullRequestId,
    PipelineRunType RunType,
    string InputJson,
    string RequestedBy);

public sealed record DeploymentPackageRequest(
    Guid CustomerId,
    Guid ProjectId,
    Guid PullRequestId,
    string Version,
    string RequestedBy);

public sealed record MergePullRequestRequest(
    Guid CustomerId,
    Guid ProjectId,
    Guid PullRequestId,
    bool RequestedByAi,
    string RequestedBy);

public interface IDevOpsAutomationService
{
    object GetDashboard(DevOpsDashboardFilter filter);
    DevOpsRepository RegisterRepository(DevOpsRepositoryRegistrationRequest request);
    DevOpsBranch CreateBranch(DevOpsBranchRequest request);
    DevOpsPullRequest CreatePullRequest(DevOpsPullRequestCreateRequest request);
    Task<AiCodeAnalysis> AnalyzeCodeAsync(AiCodeAnalysisRequest request, CancellationToken cancellationToken);
    Task<AiPatchProposal> ProposePatchAsync(AiPatchProposalRequest request, CancellationToken cancellationToken);
    CodeReviewRecord AddReview(Guid customerId, Guid projectId, Guid pullRequestId, string reviewerUserId, CodeReviewDecision decision, string comments);
    ApprovalRequest SubmitApproval(Guid customerId, Guid projectId, Guid pullRequestId, string requestedBy, string approverUserId);
    Task<PipelineRun> RunPipelineAsync(PipelineRunRequest request, CancellationToken cancellationToken);
    DeploymentPackage CreateOrUpdatePackage(DeploymentPackageRequest request);
    DevOpsPullRequest MergePullRequest(MergePullRequestRequest request);
}

public sealed record ObservabilityDashboardFilter(Guid CustomerId, Guid? ProjectId, Guid? EnvironmentId, DateTimeOffset? DateFrom, DateTimeOffset? DateTo);

public sealed record TelemetrySourceRegistrationRequest(
    Guid CustomerId,
    Guid? ProjectId,
    Guid? EnvironmentId,
    Guid? ConnectorId,
    Guid? ProductionReleasePackageId,
    Guid? ProductionDeploymentRunId,
    string SourceKey,
    string Name,
    TelemetrySourceType SourceType,
    string EndpointRef,
    int PollIntervalSeconds,
    int TimeoutSeconds,
    bool MaskLogs,
    string RequestedBy);

public sealed record TelemetryIngestRequest(
    Guid CustomerId,
    Guid? ProjectId,
    Guid TelemetrySourceId,
    Guid? EnvironmentId,
    Guid? ConnectorId,
    Guid? ProductionReleasePackageId,
    Guid? ProductionDeploymentRunId,
    TelemetrySignalType SignalType,
    TelemetryHealthStatus HealthStatus,
    string MetricName,
    decimal? MetricValue,
    string Unit,
    int? ApiLatencyMs,
    decimal? UptimePercent,
    string Summary,
    string PayloadJson,
    string? CorrelationId);

public sealed record MonitoringEvaluationRequest(Guid CustomerId, Guid? ProjectId, Guid? TelemetrySourceId, string RequestedBy);

public sealed record IncidentCreateRequest(
    Guid CustomerId,
    Guid? ProjectId,
    Guid? EnvironmentId,
    Guid? ConnectorId,
    Guid? ProductionReleasePackageId,
    Guid? ProductionDeploymentRunId,
    Guid? IssueId,
    Guid? SlaPolicyId,
    Guid? AlertEventId,
    string Title,
    string Description,
    AlertSeverity Severity,
    IncidentPriority Priority,
    string ImpactSummary,
    string RequestedBy);

public interface IObservabilityService
{
    object GetDashboard(ObservabilityDashboardFilter filter);
    TelemetrySource RegisterSource(TelemetrySourceRegistrationRequest request);
    Task<RuntimeTelemetrySample> CollectMockTelemetryAsync(Guid customerId, Guid? projectId, Guid telemetrySourceId, string requestedBy, CancellationToken cancellationToken);
    RuntimeTelemetrySample IngestTelemetry(TelemetryIngestRequest request, string requestedBy);
    IReadOnlyList<AlertEvent> EvaluateMonitoring(MonitoringEvaluationRequest request);
    IncidentRecord CreateIncident(IncidentCreateRequest request);
    Issue ConvertIncidentToIssue(Guid customerId, Guid projectId, Guid incidentId, string requestedBy);
    Task<AiIncidentDiagnosis> DiagnoseIncidentAsync(Guid customerId, Guid? projectId, Guid incidentId, string requestedBy, CancellationToken cancellationToken);
    PostIncidentReview CreatePostIncidentReview(Guid customerId, Guid? projectId, Guid incidentId, string createdBy);
    AlertEvent AcknowledgeAlert(Guid customerId, Guid? projectId, Guid alertId, string actor);
    IncidentRecord ResolveIncident(Guid customerId, Guid? projectId, Guid incidentId, string actor, string resolution);
}

public sealed record DataMigrationDashboardFilter(Guid CustomerId, Guid ProjectId, Guid? EnvironmentId);

public sealed record DataImportTemplateRequest(
    Guid CustomerId,
    Guid ProjectId,
    string TemplateKey,
    string Name,
    HrmDataDomain Domain,
    ImportFileType DefaultFileType,
    DataClassificationLevel MaxClassification,
    string SchemaJson,
    string SampleFileRef,
    string RequestedBy);

public sealed record DataImportFileRequest(
    Guid CustomerId,
    Guid ProjectId,
    Guid? EnvironmentId,
    Guid? TemplateId,
    string FileRef,
    string FileName,
    ImportFileType FileType,
    long SizeBytes,
    int RowCount,
    DataClassificationLevel Classification,
    string PreviewJson,
    string UploadedBy);

public sealed record DataColumnMappingRequest(
    Guid CustomerId,
    Guid ProjectId,
    Guid TemplateId,
    int TemplateVersion,
    string MappingKey,
    string SourceColumn,
    string TargetEntity,
    string TargetField,
    string TransformExpression,
    DataClassificationLevel DataClassification);

public sealed record DataValidationRuleRequest(
    Guid CustomerId,
    Guid ProjectId,
    Guid? TemplateId,
    string RuleKey,
    string Name,
    HrmDataDomain Domain,
    string TargetField,
    ValidationRuleType RuleType,
    string ExpressionJson,
    ValidationIssueSeverity Severity);

public sealed record DataImportBatchRequest(
    Guid CustomerId,
    Guid ProjectId,
    Guid EnvironmentId,
    Guid? ConnectorId,
    Guid TemplateId,
    int TemplateVersion,
    Guid ImportFileId,
    HrmDataDomain Domain,
    string RequestedBy);

public interface IDataMigrationService
{
    object GetDashboard(DataMigrationDashboardFilter filter);
    DataImportTemplate CreateTemplate(DataImportTemplateRequest request);
    DataImportFile RegisterFile(DataImportFileRequest request, bool canViewSensitivePreview);
    DataColumnMapping CreateMapping(DataColumnMappingRequest request);
    DataValidationRule CreateValidationRule(DataValidationRuleRequest request);
    DataImportBatch CreateBatch(DataImportBatchRequest request);
    Task<DataImportRun> DryRunAsync(Guid customerId, Guid projectId, Guid batchId, string requestedBy, CancellationToken cancellationToken);
    Task<DataImportRun> ApplyToTestUatAsync(Guid customerId, Guid projectId, Guid batchId, string requestedBy, CancellationToken cancellationToken);
    DataReconciliationReport Reconcile(Guid customerId, Guid projectId, Guid batchId, string requestedBy);
    Task<AiDataMigrationAssistance> GenerateAiAssistanceAsync(Guid customerId, Guid projectId, Guid? batchId, Guid? templateId, AiDataAssistanceType assistanceType, string context, string requestedBy, CancellationToken cancellationToken);
    DataSignOff SignOff(Guid customerId, Guid projectId, Guid batchId, string signedBy, string role, string comment);
}
