using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HrmAiOps.Application.Abstractions;
using HrmAiOps.Domain.Core;

namespace HrmAiOps.Infrastructure.Reporting;

public sealed class ReportingService(
    IAppStore store,
    IAuditWriter audit,
    IDataMaskingService masking,
    IAiRunRecorder aiRuns,
    IBackgroundJobDispatcher jobs) : IReportingService
{
    private readonly JsonSerializerOptions _jsonOptions = CreateJsonOptions();

    public IReadOnlyList<ReportCatalogItem> GetCatalog(Guid customerId, Guid? projectId) =>
        store.ReportTemplates
            .Where(x => x.CustomerId == customerId && x.Active && (x.ProjectId == null || x.ProjectId == projectId))
            .OrderBy(x => x.ReportType)
            .ThenBy(x => x.Name)
            .Select(template =>
            {
                var version = ActiveVersion(customerId, template.Id);
                return new ReportCatalogItem(
                    template.Id,
                    template.TemplateKey,
                    template.Name,
                    template.ReportType,
                    template.DefaultFormat,
                    version?.Version ?? 1,
                    template.MaxClassification,
                    template.RequiresPermission,
                    template.RequiredPermission);
            })
            .ToList();

    public object GetExecutiveDashboard(ReportingDashboardFilter filter)
    {
        var projectIds = ProjectIds(filter);
        var issues = store.Issues.Where(x => x.CustomerId == filter.CustomerId && projectIds.Contains(x.ProjectId) && InRange(x.CreatedAt, filter)).ToList();
        var tickets = store.CustomerPortalTickets.Where(x => x.CustomerId == filter.CustomerId && projectIds.Contains(x.ProjectId) && InRange(x.SubmittedAt, filter)).ToList();
        var releases = store.ProductionReleasePackages.Where(x => x.CustomerId == filter.CustomerId && projectIds.Contains(x.ProjectId) && InRange(x.CreatedAt, filter)).ToList();
        var ai = store.AiRuns.Where(x => x.CustomerId == filter.CustomerId && (!x.ProjectId.HasValue || projectIds.Contains(x.ProjectId.Value)) && InRange(x.StartedAt, filter)).ToList();
        var exports = store.ReportExportFiles.Where(x => x.CustomerId == filter.CustomerId && (!x.ProjectId.HasValue || projectIds.Contains(x.ProjectId.Value)) && InRange(x.CreatedAt, filter)).ToList();
        var slaBreached = tickets.Count(x => x.SlaStatus == SlaStatus.Breached);
        var highRisk = issues.Count(x => x.RiskLevel is RiskLevel.High or RiskLevel.Critical);
        var closedReleases = releases.Count(x => x.Status is ProductionReleaseStatus.Closed or ProductionReleaseStatus.ReadyToClose);
        var health = Clamp(100 - (slaBreached * 12) - (highRisk * 6) + (closedReleases * 4));

        return new
        {
            filter,
            healthScore = health,
            activeProjects = projectIds.Count,
            openIssues = issues.Count(x => x.Status != IssueStatus.Closed),
            highRiskIssues = highRisk,
            slaBreached,
            releases = releases.Count,
            successfulReleases = closedReleases,
            aiRuns = ai.Count,
            reportExports = exports.Count,
            latestAiSummary = store.DashboardSnapshots
                .Where(x => x.CustomerId == filter.CustomerId && x.ProjectId == filter.ProjectId && x.SnapshotType == DashboardSnapshotType.Executive)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault()?.AiSummary,
            riskTrend = highRisk == 0 ? "Stable" : highRisk > 2 ? "NeedsAttention" : "Watch",
            topRisks = issues.OrderByDescending(x => x.RiskLevel).Take(5).Select(x => new { x.IssueNo, x.Title, x.RiskLevel, x.Severity, x.Status }),
            recentExports = exports.OrderByDescending(x => x.CreatedAt).Take(5)
        };
    }

    public object GetProjectDashboard(ReportingDashboardFilter filter)
    {
        var projectIds = ProjectIds(filter);
        var requirements = store.Requirements.Where(x => x.CustomerId == filter.CustomerId && projectIds.Contains(x.ProjectId)).ToList();
        var urs = store.UrsDocuments.Where(x => x.CustomerId == filter.CustomerId && projectIds.Contains(x.ProjectId)).ToList();
        var blueprints = store.Blueprints.Where(x => x.CustomerId == filter.CustomerId && projectIds.Contains(x.ProjectId)).ToList();
        var specs = store.ConfigSpecs.Where(x => x.CustomerId == filter.CustomerId && projectIds.Contains(x.ProjectId)).ToList();
        var applyRuns = store.ApplyRuns.Where(x => x.CustomerId == filter.CustomerId && projectIds.Contains(x.ProjectId) && InRange(x.CreatedAt, filter)).ToList();
        var readiness = store.ReleaseReadinessReports.Where(x => x.CustomerId == filter.CustomerId && projectIds.Contains(x.ProjectId) && InRange(x.GeneratedAt, filter)).ToList();

        return new
        {
            filter,
            documents = new
            {
                requirements = requirements.Count,
                urs = urs.Count,
                blueprints = blueprints.Count,
                configSpecs = specs.Count,
                signedOff = store.DocumentSignOffs.Count(x => x.CustomerId == filter.CustomerId && projectIds.Contains(x.ProjectId))
            },
            delivery = new
            {
                applyRuns = applyRuns.Count,
                applied = applyRuns.Count(x => x.Status is ApplyRunStatus.Applied or ApplyRunStatus.ReleaseReady),
                releaseReady = readiness.Count(x => x.Status == ReleaseReadinessStatus.Ready),
                blocked = readiness.Count(x => x.Status == ReleaseReadinessStatus.Blocked)
            },
            traceability = store.TraceLinks.Count(x => x.CustomerId == filter.CustomerId && projectIds.Contains(x.ProjectId)),
            latestReadiness = readiness.OrderByDescending(x => x.GeneratedAt).FirstOrDefault(),
            latestDocuments = requirements.OrderByDescending(x => x.UpdatedAt).Take(5).Select(x => new { x.RequirementNo, x.Title, x.Status, x.Version })
        };
    }

    public object GetCustomerHealthDashboard(ReportingDashboardFilter filter)
    {
        var projectIds = ProjectIds(filter);
        var reports = store.CustomerServiceReports.Where(x => x.CustomerId == filter.CustomerId && InRange(x.PeriodEnd, filter)).ToList();
        var tickets = store.CustomerPortalTickets.Where(x => x.CustomerId == filter.CustomerId && projectIds.Contains(x.ProjectId) && InRange(x.SubmittedAt, filter)).ToList();
        var usage = store.UsageQuotaSnapshots.Where(x => x.CustomerId == filter.CustomerId).ToList();
        var invoices = store.InvoiceDrafts.Where(x => x.CustomerId == filter.CustomerId && InRange(x.IssueDate, filter)).ToList();
        var health = reports.OrderByDescending(x => x.PeriodEnd).FirstOrDefault()?.HealthScore ?? Clamp(100 - tickets.Count(x => x.SlaStatus == SlaStatus.Breached) * 15);

        return new
        {
            filter,
            healthScore = health,
            openTickets = tickets.Count(x => x.Status is PortalTicketStatus.Open or PortalTicketStatus.InProgress or PortalTicketStatus.WaitingCustomer),
            slaMet = tickets.Count(x => x.SlaStatus is SlaStatus.Met or SlaStatus.OnTrack),
            slaBreached = tickets.Count(x => x.SlaStatus == SlaStatus.Breached),
            quotaWarnings = usage.Count(x => x.OverageQuantity > 0 || x.Blocked),
            unpaidInvoices = invoices.Count(x => x.Status is InvoiceDraftStatus.Sent or InvoiceDraftStatus.Draft),
            latestServiceReport = reports.OrderByDescending(x => x.PeriodEnd).FirstOrDefault(),
            customerVisibleReports = store.ReportExportFiles.Count(x => x.CustomerId == filter.CustomerId && (!x.ProjectId.HasValue || projectIds.Contains(x.ProjectId.Value)) && x.Visibility != ReportVisibility.InternalOnly)
        };
    }

    public async Task<ReportGenerationResult> GenerateAsync(ReportGenerationRequest request, CancellationToken cancellationToken)
    {
        if (request.ProjectId is null)
        {
            throw new InvalidOperationException("ProjectId is required for report export.");
        }

        var template = ResolveTemplate(request);
        var version = ActiveVersion(request.CustomerId, template.Id)
            ?? throw new InvalidOperationException("Active report template version not found.");
        var external = request.ExternalExport || request.Visibility != ReportVisibility.InternalOnly;
        var maskingApplied = external && template.ApplyMaskingForExternalExport && template.MaxClassification >= DataClassificationLevel.Confidential;
        var job = new ReportGenerationJob
        {
            CustomerId = request.CustomerId,
            ProjectId = request.ProjectId,
            TemplateId = template.Id,
            TemplateVersion = version.Version,
            ReportType = template.ReportType,
            OutputFormat = request.OutputFormat,
            Visibility = request.Visibility,
            RequestedBy = request.RequestedBy,
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            FilterJson = JsonSerializer.Serialize(new { request.CustomerId, request.ProjectId, request.DateFrom, request.DateTo }, _jsonOptions),
            MaskingApplied = maskingApplied
        };
        store.ReportGenerationJobs.Add(job);
        audit.Write(request.CustomerId, request.ProjectId, "REPORT_EXPORT_REQUESTED", nameof(ReportGenerationJob), job.Id, new { job, queueReady = true });
        await jobs.EnqueueAsync("report.generate", new { job.Id, request.CustomerId, request.ProjectId }, cancellationToken);

        job.Status = ReportGenerationStatus.Running;
        job.StartedAt = DateTimeOffset.UtcNow;
        var payload = BuildPayload(request, template, version);
        var payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
        if (maskingApplied)
        {
            payloadJson = masking.Mask(payloadJson);
        }

        var file = new ReportExportFile
        {
            CustomerId = request.CustomerId,
            ProjectId = request.ProjectId,
            ReportJobId = job.Id,
            TemplateId = template.Id,
            TemplateVersion = version.Version,
            ReportType = template.ReportType,
            OutputFormat = request.OutputFormat,
            FileName = BuildFileName(template.ReportType, request.OutputFormat),
            ContentType = ContentType(request.OutputFormat),
            StorageRef = $"reports://customers/{request.CustomerId}/projects/{request.ProjectId}/{job.Id}/{BuildFileName(template.ReportType, request.OutputFormat)}",
            SizeBytes = Encoding.UTF8.GetByteCount(payloadJson),
            Checksum = Sha256(payloadJson),
            Visibility = request.Visibility,
            MaskingApplied = maskingApplied,
            ContainsSensitiveData = template.MaxClassification >= DataClassificationLevel.Confidential,
            PublishedAt = request.Visibility == ReportVisibility.PublishedToPortal ? DateTimeOffset.UtcNow : null,
            SharedAt = request.Visibility == ReportVisibility.SharedWithCustomer ? DateTimeOffset.UtcNow : null
        };
        store.ReportExportFiles.Add(file);
        if (file.Visibility != ReportVisibility.InternalOnly)
        {
            store.PortalReportShares.Add(new PortalReportShare { CustomerId = request.CustomerId, ProjectId = request.ProjectId, ReportExportFileId = file.Id, Visibility = file.Visibility });
        }

        job.Status = ReportGenerationStatus.Completed;
        job.CompletedAt = DateTimeOffset.UtcNow;
        audit.Write(request.CustomerId, request.ProjectId, "REPORT_EXPORT_GENERATED", nameof(ReportExportFile), file.Id, new { file, binaryStoredInDatabase = false });
        return new ReportGenerationResult(job, file);
    }

    public Task<DashboardSnapshot> GenerateAiSummaryAsync(ReportingDashboardFilter filter, string requestedBy, CancellationToken cancellationToken)
    {
        var executive = GetExecutiveDashboard(filter);
        var project = GetProjectDashboard(filter);
        var health = GetCustomerHealthDashboard(filter);
        var input = JsonSerializer.Serialize(new { executive, project, health }, _jsonOptions);
        input = masking.Mask(input);
        var run = aiRuns.Start(filter.CustomerId, filter.ProjectId, "ExecutiveDashboardSummary", "Executive dashboard summary generated from customer/project scoped data.", "dashboard:phase13", null);
        run.MaskedInputPreview = input.Length > 700 ? input[..700] : input;
        var summary = BuildSummaryText(filter, executive, project, health);
        aiRuns.Complete(run, summary, "dashboard-snapshot");
        var snapshot = new DashboardSnapshot
        {
            CustomerId = filter.CustomerId,
            ProjectId = filter.ProjectId,
            SnapshotType = filter.ProjectId.HasValue ? DashboardSnapshotType.Project : DashboardSnapshotType.Executive,
            DateFrom = filter.DateFrom,
            DateTo = filter.DateTo,
            HealthScore = ReadDecimal(executive, "healthScore"),
            DeliveryScore = ReadDecimal(project, "traceability"),
            SlaScore = ReadDecimal(health, "slaMet"),
            RiskScore = ReadDecimal(executive, "highRiskIssues"),
            AiRunId = run.Id,
            AiSummary = summary,
            SnapshotJson = JsonSerializer.Serialize(new { executive, project, health }, _jsonOptions)
        };
        store.DashboardSnapshots.Add(snapshot);
        audit.Write(filter.CustomerId, filter.ProjectId, "EXECUTIVE_DASHBOARD_AI_SUMMARY_GENERATED", nameof(DashboardSnapshot), snapshot.Id, new { snapshot, requestedBy });
        return Task.FromResult(snapshot);
    }

    private ReportTemplate ResolveTemplate(ReportGenerationRequest request)
    {
        var query = store.ReportTemplates.Where(x => x.CustomerId == request.CustomerId && x.Active && (x.ProjectId == null || x.ProjectId == request.ProjectId));
        return request.TemplateId.HasValue
            ? query.SingleOrDefault(x => x.Id == request.TemplateId.Value) ?? throw new InvalidOperationException("Report template not found.")
            : query.FirstOrDefault(x => x.ReportType == request.ReportType) ?? throw new InvalidOperationException("Report template not found.");
    }

    private ReportTemplateVersion? ActiveVersion(Guid customerId, Guid templateId) =>
        store.ReportTemplateVersions
            .Where(x => x.CustomerId == customerId && x.TemplateId == templateId && x.Active)
            .OrderByDescending(x => x.Version)
            .FirstOrDefault();

    private object BuildPayload(ReportGenerationRequest request, ReportTemplate template, ReportTemplateVersion version)
    {
        var filter = new ReportingDashboardFilter(request.CustomerId, request.ProjectId, request.DateFrom, request.DateTo);
        var projectId = request.ProjectId!.Value;
        return new
        {
            template = new { template.TemplateKey, template.Name, template.ReportType, version.Version, version.LayoutDefinitionJson },
            filter,
            generatedAt = DateTimeOffset.UtcNow,
            data = template.ReportType switch
            {
                ReportDocumentType.Urs => store.UrsDocuments.Where(x => x.CustomerId == request.CustomerId && x.ProjectId == projectId).OrderByDescending(x => x.Version).ToList(),
                ReportDocumentType.Blueprint => store.Blueprints.Where(x => x.CustomerId == request.CustomerId && x.ProjectId == projectId).OrderByDescending(x => x.Version).ToList(),
                ReportDocumentType.ConfigSpec => store.ConfigSpecs.Where(x => x.CustomerId == request.CustomerId && x.ProjectId == projectId).OrderByDescending(x => x.Version).ToList(),
                ReportDocumentType.UatTestCase => store.RegressionTestPlans.Where(x => x.CustomerId == request.CustomerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt).ToList(),
                ReportDocumentType.ReleaseReadiness => store.ReleaseReadinessReports.Where(x => x.CustomerId == request.CustomerId && x.ProjectId == projectId && InRange(x.GeneratedAt, filter)).OrderByDescending(x => x.GeneratedAt).ToList(),
                ReportDocumentType.ProductionRelease => store.ProductionReleasePackages.Where(x => x.CustomerId == request.CustomerId && x.ProjectId == projectId && InRange(x.CreatedAt, filter)).OrderByDescending(x => x.CreatedAt).ToList(),
                ReportDocumentType.CustomerService => store.CustomerServiceReports.Where(x => x.CustomerId == request.CustomerId && InRange(x.PeriodEnd, filter)).OrderByDescending(x => x.PeriodEnd).ToList(),
                ReportDocumentType.Sla => store.CustomerPortalTickets.Where(x => x.CustomerId == request.CustomerId && x.ProjectId == projectId && InRange(x.SubmittedAt, filter)).OrderByDescending(x => x.SubmittedAt).ToList(),
                ReportDocumentType.Billing => new { drafts = store.BillingDrafts.Where(x => x.CustomerId == request.CustomerId && InRange(x.PeriodEnd, filter)), invoices = store.InvoiceDrafts.Where(x => x.CustomerId == request.CustomerId && InRange(x.IssueDate, filter)) },
                ReportDocumentType.Audit => store.AuditLogs.Where(x => x.CustomerId == request.CustomerId && x.ProjectId == projectId && InRange(x.CreatedAt, filter)).OrderByDescending(x => x.CreatedAt).ToList(),
                ReportDocumentType.Security => new { evidence = store.ComplianceEvidence.Where(x => x.CustomerId == request.CustomerId && x.ProjectId == projectId), classifications = store.DataClassificationRules.Where(x => x.CustomerId == request.CustomerId && (x.ProjectId == null || x.ProjectId == projectId)) },
                ReportDocumentType.Knowledge => store.KnowledgeLearningItems.Where(x => x.CustomerId == request.CustomerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt).ToList(),
                ReportDocumentType.ExecutiveSummary => GetExecutiveDashboard(filter),
                _ => GetExecutiveDashboard(filter)
            }
        };
    }

    private HashSet<Guid> ProjectIds(ReportingDashboardFilter filter)
    {
        if (filter.ProjectId.HasValue)
        {
            return store.Projects.Any(x => x.CustomerId == filter.CustomerId && x.Id == filter.ProjectId.Value)
                ? [filter.ProjectId.Value]
                : [];
        }

        return store.Projects.Where(x => x.CustomerId == filter.CustomerId).Select(x => x.Id).ToHashSet();
    }

    private static bool InRange(DateTimeOffset value, ReportingDashboardFilter filter) =>
        value >= filter.DateFrom && value <= filter.DateTo;

    private static decimal Clamp(decimal score) => Math.Max(0, Math.Min(100, score));

    private static string BuildFileName(ReportDocumentType reportType, ReportOutputFormat format) =>
        $"{reportType.ToString().ToLowerInvariant()}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.{Extension(format)}";

    private static string Extension(ReportOutputFormat format) => format switch
    {
        ReportOutputFormat.Word => "docx",
        ReportOutputFormat.Excel => "xlsx",
        _ => "pdf"
    };

    private static string ContentType(ReportOutputFormat format) => format switch
    {
        ReportOutputFormat.Word => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ReportOutputFormat.Excel => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        _ => "application/pdf"
    };

    private static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string BuildSummaryText(ReportingDashboardFilter filter, object executive, object project, object health)
    {
        var healthScore = ReadDecimal(executive, "healthScore");
        var highRisk = ReadDecimal(executive, "highRiskIssues");
        var openTickets = ReadDecimal(health, "openTickets");
        return $"AI executive summary for customer {filter.CustomerId} and project {filter.ProjectId?.ToString() ?? "all"}: health score {healthScore:0.##}, high-risk issues {highRisk:0}, open tickets {openTickets:0}. Recommended focus: review SLA exceptions, close traceability gaps and publish only masked customer-visible exports.";
    }

    private static decimal ReadDecimal(object value, string propertyName)
    {
        var property = value.GetType().GetProperty(propertyName);
        if (property?.GetValue(value) is null) return 0;
        return Convert.ToDecimal(property.GetValue(value));
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
