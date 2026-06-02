using System.Text.Json;
using System.Text.Json.Serialization;
using HrmAiOps.Application.Abstractions;
using HrmAiOps.Domain.Core;

namespace HrmAiOps.Infrastructure.Observability;

public sealed class ObservabilityService(
    IAppStore store,
    IAuditWriter audit,
    IDataMaskingService masking,
    IAiRunRecorder aiRuns,
    IBackgroundJobDispatcher jobs) : IObservabilityService
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public object GetDashboard(ObservabilityDashboardFilter filter)
    {
        var from = filter.DateFrom ?? DateTimeOffset.UtcNow.AddHours(-24);
        var to = filter.DateTo ?? DateTimeOffset.UtcNow;
        var sources = store.TelemetrySources
            .Where(x => x.CustomerId == filter.CustomerId && (!filter.ProjectId.HasValue || x.ProjectId == filter.ProjectId || x.ProjectId == null) && (!filter.EnvironmentId.HasValue || x.EnvironmentId == filter.EnvironmentId))
            .ToList();
        var sourceIds = sources.Select(x => x.Id).ToHashSet();
        var samples = store.RuntimeTelemetrySamples
            .Where(x => x.CustomerId == filter.CustomerId && sourceIds.Contains(x.TelemetrySourceId) && x.ObservedAt >= from && x.ObservedAt <= to && (!filter.ProjectId.HasValue || x.ProjectId == filter.ProjectId || x.ProjectId == null))
            .ToList();
        var alerts = store.AlertEvents
            .Where(x => x.CustomerId == filter.CustomerId && (!filter.ProjectId.HasValue || x.ProjectId == filter.ProjectId || x.ProjectId == null) && x.TriggeredAt >= from && x.TriggeredAt <= to)
            .ToList();
        var incidents = store.IncidentRecords
            .Where(x => x.CustomerId == filter.CustomerId && (!filter.ProjectId.HasValue || x.ProjectId == filter.ProjectId || x.ProjectId == null) && x.DetectedAt >= from && x.DetectedAt <= to)
            .ToList();

        return new
        {
            filter = new { filter.CustomerId, filter.ProjectId, filter.EnvironmentId, dateFrom = from, dateTo = to },
            telemetrySources = sources.Count,
            healthySources = LatestBySource(samples).Count(x => x.HealthStatus == TelemetryHealthStatus.Healthy),
            degradedSources = LatestBySource(samples).Count(x => x.HealthStatus == TelemetryHealthStatus.Degraded),
            unhealthySources = LatestBySource(samples).Count(x => x.HealthStatus == TelemetryHealthStatus.Unhealthy),
            samples = samples.Count,
            averageLatencyMs = samples.Where(x => x.ApiLatencyMs.HasValue).Select(x => x.ApiLatencyMs!.Value).DefaultIfEmpty(0).Average(),
            averageUptimePercent = samples.Where(x => x.UptimePercent.HasValue).Select(x => x.UptimePercent!.Value).DefaultIfEmpty(100).Average(),
            openAlerts = alerts.Count(x => x.Status == AlertStatus.Open),
            criticalAlerts = alerts.Count(x => x.Severity == AlertSeverity.Critical && x.Status == AlertStatus.Open),
            openIncidents = incidents.Count(x => x.Status is IncidentStatus.Open or IncidentStatus.Investigating),
            criticalIncidents = incidents.Count(x => x.Severity == AlertSeverity.Critical && x.Status is (IncidentStatus.Open or IncidentStatus.Investigating)),
            latestTelemetry = samples.OrderByDescending(x => x.ObservedAt).Take(10),
            latestAlerts = alerts.OrderByDescending(x => x.TriggeredAt).Take(10),
            latestIncidents = incidents.OrderByDescending(x => x.DetectedAt).Take(10)
        };
    }

    public TelemetrySource RegisterSource(TelemetrySourceRegistrationRequest request)
    {
        ValidateScope(request.CustomerId, request.ProjectId, request.EnvironmentId, request.ConnectorId);
        if (request.PollIntervalSeconds <= 0 || request.TimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("Telemetry poll interval and timeout must be greater than zero.");
        }
        var source = new TelemetrySource
        {
            CustomerId = request.CustomerId,
            ProjectId = request.ProjectId,
            EnvironmentId = request.EnvironmentId,
            ConnectorId = request.ConnectorId,
            ProductionReleasePackageId = request.ProductionReleasePackageId,
            ProductionDeploymentRunId = request.ProductionDeploymentRunId,
            SourceKey = request.SourceKey,
            Name = request.Name,
            SourceType = request.SourceType,
            EndpointRef = request.EndpointRef,
            PollIntervalSeconds = request.PollIntervalSeconds,
            TimeoutSeconds = request.TimeoutSeconds,
            MaskLogs = request.MaskLogs
        };
        store.TelemetrySources.Add(source);
        audit.Write(request.CustomerId, request.ProjectId, "OBSERVABILITY_SOURCE_REGISTERED", nameof(TelemetrySource), source.Id, source);
        return source;
    }

    public async Task<RuntimeTelemetrySample> CollectMockTelemetryAsync(Guid customerId, Guid? projectId, Guid telemetrySourceId, string requestedBy, CancellationToken cancellationToken)
    {
        var source = ResolveSource(customerId, projectId, telemetrySourceId);
        await jobs.EnqueueAsync("observability.mock_collect", new { customerId, projectId, telemetrySourceId }, cancellationToken);
        var index = store.RuntimeTelemetrySamples.Count(x => x.CustomerId == customerId && x.TelemetrySourceId == source.Id);
        var degraded = source.SourceType is TelemetrySourceType.Database or TelemetrySourceType.Connector && index % 3 == 1;
        var unhealthy = source.SourceType == TelemetrySourceType.Deployment && index % 4 == 2;
        var latency = unhealthy ? 2800 : degraded ? 1350 : 180 + (index * 17 % 90);
        var status = unhealthy ? TelemetryHealthStatus.Unhealthy : degraded ? TelemetryHealthStatus.Degraded : TelemetryHealthStatus.Healthy;
        var payload = JsonSerializer.Serialize(new
        {
            source = source.SourceKey,
            status,
            latencyMs = latency,
            uptime = unhealthy ? 92.4m : degraded ? 97.8m : 99.95m,
            log = unhealthy ? "error token=abc123 production deployment validation failed for employee@example.com" : "mock health check ok"
        }, JsonOptions);

        var sample = IngestTelemetry(new TelemetryIngestRequest(
            customerId,
            source.ProjectId,
            source.Id,
            source.EnvironmentId,
            source.ConnectorId,
            source.ProductionReleasePackageId,
            source.ProductionDeploymentRunId,
            TelemetrySignalType.HealthCheck,
            status,
            "api_latency_ms",
            latency,
            "ms",
            latency,
            unhealthy ? 92.4m : degraded ? 97.8m : 99.95m,
            $"{source.Name} mock telemetry: {status}",
            payload,
            null),
            requestedBy);
        audit.Write(customerId, sample.ProjectId, "OBSERVABILITY_MOCK_TELEMETRY_COLLECTED", nameof(RuntimeTelemetrySample), sample.Id, sample);
        return sample;
    }

    public RuntimeTelemetrySample IngestTelemetry(TelemetryIngestRequest request, string requestedBy)
    {
        var source = ResolveSource(request.CustomerId, request.ProjectId, request.TelemetrySourceId);
        if (request.ProjectId.HasValue && source.ProjectId.HasValue && source.ProjectId != request.ProjectId)
        {
            throw new InvalidOperationException("Telemetry source belongs to another project.");
        }
        var maskedPayload = source.MaskLogs ? masking.Mask(request.PayloadJson) : request.PayloadJson;
        var sample = new RuntimeTelemetrySample
        {
            CustomerId = request.CustomerId,
            ProjectId = request.ProjectId ?? source.ProjectId,
            TelemetrySourceId = source.Id,
            EnvironmentId = request.EnvironmentId ?? source.EnvironmentId,
            ConnectorId = request.ConnectorId ?? source.ConnectorId,
            ProductionReleasePackageId = request.ProductionReleasePackageId ?? source.ProductionReleasePackageId,
            ProductionDeploymentRunId = request.ProductionDeploymentRunId ?? source.ProductionDeploymentRunId,
            SignalType = request.SignalType,
            HealthStatus = request.HealthStatus,
            MetricName = request.MetricName,
            MetricValue = request.MetricValue,
            Unit = request.Unit,
            ApiLatencyMs = request.ApiLatencyMs,
            UptimePercent = request.UptimePercent,
            Summary = masking.Mask(request.Summary),
            MaskedPayloadJson = Limit(maskedPayload, 4000),
            CorrelationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? Guid.NewGuid().ToString("N") : request.CorrelationId,
            TraceId = Guid.NewGuid().ToString("N"),
            ObservedAt = DateTimeOffset.UtcNow
        };
        store.RuntimeTelemetrySamples.Add(sample);
        if (request.SignalType is TelemetrySignalType.LogSummary or TelemetrySignalType.ErrorEvent || sample.HealthStatus == TelemetryHealthStatus.Unhealthy)
        {
            store.TelemetryLogSummaries.Add(new TelemetryLogSummary
            {
                CustomerId = sample.CustomerId,
                ProjectId = sample.ProjectId,
                TelemetrySourceId = sample.TelemetrySourceId,
                TotalLines = sample.HealthStatus == TelemetryHealthStatus.Unhealthy ? 320 : 80,
                ErrorCount = sample.HealthStatus == TelemetryHealthStatus.Unhealthy ? 12 : 1,
                WarningCount = sample.HealthStatus == TelemetryHealthStatus.Degraded ? 6 : 2,
                MaskedSummary = sample.Summary,
                TopErrorsJson = JsonSerializer.Serialize(new[] { new { message = sample.Summary, payload = sample.MaskedPayloadJson } }, JsonOptions)
            });
        }
        audit.Write(request.CustomerId, sample.ProjectId, "OBSERVABILITY_TELEMETRY_INGESTED", nameof(RuntimeTelemetrySample), sample.Id, sample);
        return sample;
    }

    public IReadOnlyList<AlertEvent> EvaluateMonitoring(MonitoringEvaluationRequest request)
    {
        var samples = store.RuntimeTelemetrySamples
            .Where(x => x.CustomerId == request.CustomerId && (!request.ProjectId.HasValue || x.ProjectId == request.ProjectId || x.ProjectId == null) && (!request.TelemetrySourceId.HasValue || x.TelemetrySourceId == request.TelemetrySourceId))
            .GroupBy(x => x.TelemetrySourceId)
            .Select(x => x.OrderByDescending(y => y.ObservedAt).First())
            .ToList();
        var rules = store.MonitoringRules
            .Where(x => x.CustomerId == request.CustomerId && x.Active && (!request.ProjectId.HasValue || x.ProjectId == request.ProjectId || x.ProjectId == null) && (!request.TelemetrySourceId.HasValue || x.TelemetrySourceId == null || x.TelemetrySourceId == request.TelemetrySourceId))
            .ToList();
        var created = new List<AlertEvent>();
        foreach (var sample in samples)
        {
            foreach (var rule in rules.Where(x => !x.TelemetrySourceId.HasValue || x.TelemetrySourceId == sample.TelemetrySourceId))
            {
                if (!RuleMatches(rule, sample))
                {
                    continue;
                }
                if (store.AlertEvents.Any(x => x.CustomerId == request.CustomerId && x.Status == AlertStatus.Open && x.MonitoringRuleId == rule.Id && x.TelemetrySampleId == sample.Id))
                {
                    continue;
                }

                var alertRule = ResolveAlertRule(request.CustomerId, request.ProjectId, rule);
                var alert = new AlertEvent
                {
                    CustomerId = sample.CustomerId,
                    ProjectId = sample.ProjectId,
                    MonitoringRuleId = rule.Id,
                    AlertRuleId = alertRule?.Id,
                    TelemetrySampleId = sample.Id,
                    Severity = rule.Severity,
                    Title = $"{rule.Name}: {sample.Summary}",
                    Message = $"Rule {rule.RuleKey} matched {sample.MetricName}={sample.MetricValue} {sample.Unit}; health={sample.HealthStatus}.",
                    CorrelationId = sample.CorrelationId,
                    TraceId = sample.TraceId,
                    MaskedPayloadJson = sample.MaskedPayloadJson
                };
                store.AlertEvents.Add(alert);
                created.Add(alert);
                audit.Write(alert.CustomerId, alert.ProjectId, "OBSERVABILITY_ALERT_CREATED", nameof(AlertEvent), alert.Id, alert);

                if (rule.AutoCreateIncident)
                {
                    var incident = CreateIncidentFromAlert(alert, sample, rule, request.RequestedBy);
                    alert.IncidentId = incident.Id;
                    if (rule.AutoCreateIssue && incident.ProjectId.HasValue)
                    {
                        ConvertIncidentToIssue(incident.CustomerId, incident.ProjectId.Value, incident.Id, request.RequestedBy);
                    }
                }
                DispatchAlert(alert, alertRule, request.RequestedBy);
            }
        }
        return created;
    }

    public IncidentRecord CreateIncident(IncidentCreateRequest request)
    {
        var incident = new IncidentRecord
        {
            CustomerId = request.CustomerId,
            ProjectId = request.ProjectId,
            EnvironmentId = request.EnvironmentId,
            ConnectorId = request.ConnectorId,
            ProductionReleasePackageId = request.ProductionReleasePackageId,
            ProductionDeploymentRunId = request.ProductionDeploymentRunId,
            IssueId = request.IssueId,
            SlaPolicyId = request.SlaPolicyId,
            AlertEventId = request.AlertEventId,
            IncidentNo = store.NextNumber("INC"),
            Title = masking.Mask(request.Title),
            Description = masking.Mask(request.Description),
            Severity = request.Severity,
            Priority = request.Priority,
            ImpactSummary = masking.Mask(request.ImpactSummary),
            Status = IncidentStatus.Open
        };
        store.IncidentRecords.Add(incident);
        AddIncidentAction(incident, IncidentActionType.ManualNote, request.RequestedBy, "Incident created.", "{}");
        AttachSlaIfAvailable(incident);
        if (incident.Severity == AlertSeverity.Critical)
        {
            DispatchCriticalIncident(incident, request.RequestedBy);
        }
        audit.Write(request.CustomerId, request.ProjectId, "OBSERVABILITY_INCIDENT_CREATED", nameof(IncidentRecord), incident.Id, incident);
        return incident;
    }

    public Issue ConvertIncidentToIssue(Guid customerId, Guid projectId, Guid incidentId, string requestedBy)
    {
        var incident = ResolveIncident(customerId, projectId, incidentId);
        if (incident.IssueId.HasValue)
        {
            return store.Issues.Single(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == incident.IssueId.Value);
        }
        var issue = new Issue
        {
            CustomerId = customerId,
            ProjectId = projectId,
            EnvironmentId = incident.EnvironmentId,
            LinkedEntityType = nameof(IncidentRecord),
            LinkedEntityId = incident.Id,
            IssueNo = store.NextNumber("ISS"),
            Title = incident.Title,
            Description = incident.Description,
            Category = IssueCategory.Other,
            RiskLevel = incident.Severity switch { AlertSeverity.Critical => RiskLevel.Critical, AlertSeverity.High => RiskLevel.High, AlertSeverity.Warning => RiskLevel.Medium, _ => RiskLevel.Low },
            Severity = incident.Severity switch { AlertSeverity.Critical => IssueSeverity.Critical, AlertSeverity.High => IssueSeverity.High, AlertSeverity.Warning => IssueSeverity.Medium, _ => IssueSeverity.Low },
            Priority = Enum.TryParse<IssuePriority>(incident.Priority.ToString(), out var p) ? p : IssuePriority.P2,
            ReportedBy = requestedBy,
            Status = IssueStatus.Open
        };
        store.Issues.Add(issue);
        incident.IssueId = issue.Id;
        AddIncidentAction(incident, IncidentActionType.CreateIssue, requestedBy, $"Converted to issue {issue.IssueNo}.", JsonSerializer.Serialize(new { issue.Id, issue.IssueNo }, JsonOptions));
        audit.Write(customerId, projectId, "OBSERVABILITY_INCIDENT_CONVERTED_TO_ISSUE", nameof(Issue), issue.Id, new { incident, issue });
        return issue;
    }

    public async Task<AiIncidentDiagnosis> DiagnoseIncidentAsync(Guid customerId, Guid? projectId, Guid incidentId, string requestedBy, CancellationToken cancellationToken)
    {
        var incident = ResolveIncident(customerId, projectId, incidentId);
        var relatedSamples = incident.AlertEventId.HasValue
            ? SamplesForAlert(customerId, incident.AlertEventId.Value)
            : store.RuntimeTelemetrySamples.Where(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == projectId)).OrderByDescending(x => x.ObservedAt).Take(5).ToList();
        var context = masking.Mask(JsonSerializer.Serialize(new { incident, relatedSamples }, JsonOptions));
        var run = aiRuns.Start(customerId, projectId, "AiIncidentDiagnosis", $"Incident {incident.IncidentNo} diagnosis", $"incident:{incident.Id}", null);
        run.MaskedInputPreview = Limit(context, 700);
        await jobs.EnqueueAsync("observability.ai_incident_diagnosis", new { customerId, projectId, incidentId }, cancellationToken);

        var rootCause = BuildRootCause(incident, relatedSamples);
        var recommended = "Validate latest deployment, connector health, database availability and API latency. Apply mitigation through approved production governance only.";
        var diagnosis = new AiIncidentDiagnosis
        {
            CustomerId = customerId,
            ProjectId = projectId ?? incident.ProjectId,
            IncidentId = incident.Id,
            AiRunId = run.Id,
            RootCauseHypothesis = rootCause,
            RecommendedActions = recommended,
            EvidenceSummary = $"Analyzed {relatedSamples.Count} masked telemetry sample(s); AI did not execute production changes.",
            ConfidenceScore = incident.Severity == AlertSeverity.Critical ? 0.78m : 0.66m,
            ProductionFixExecuted = false
        };
        store.AiIncidentDiagnoses.Add(diagnosis);
        incident.AiRunId = run.Id;
        AddIncidentAction(incident, IncidentActionType.AiDiagnose, requestedBy, "AI incident diagnosis created. No production fix executed.", JsonSerializer.Serialize(diagnosis, JsonOptions));
        aiRuns.Complete(run, $"{diagnosis.RootCauseHypothesis}\n{diagnosis.RecommendedActions}", $"ai_incident_diagnosis:{diagnosis.Id}");
        audit.Write(customerId, diagnosis.ProjectId, "OBSERVABILITY_AI_INCIDENT_DIAGNOSIS_CREATED", nameof(AiIncidentDiagnosis), diagnosis.Id, diagnosis);
        return diagnosis;
    }

    public PostIncidentReview CreatePostIncidentReview(Guid customerId, Guid? projectId, Guid incidentId, string createdBy)
    {
        var incident = ResolveIncident(customerId, projectId, incidentId);
        var actions = store.IncidentActions.Where(x => x.CustomerId == customerId && x.IncidentId == incident.Id).OrderBy(x => x.CreatedAt).ToList();
        var article = new KnowledgeArticle
        {
            CustomerId = customerId,
            ProjectId = incident.ProjectId,
            IssueId = incident.IssueId,
            Title = $"Post-incident knowledge: {incident.IncidentNo}",
            Category = "Incident",
            Content = $"Incident {incident.Title}\nImpact: {incident.ImpactSummary}\nMitigation: {incident.CurrentMitigation}",
            Visibility = "Internal",
            Status = WorkStatus.Draft
        };
        store.KnowledgeArticles.Add(article);
        var review = new PostIncidentReview
        {
            CustomerId = customerId,
            ProjectId = incident.ProjectId,
            IncidentId = incident.Id,
            AiRunId = incident.AiRunId,
            KnowledgeArticleId = article.Id,
            ReviewNo = store.NextNumber("PIR"),
            Summary = $"Review for {incident.IncidentNo}: {incident.Title}",
            TimelineJson = JsonSerializer.Serialize(actions.Select(x => new { x.CreatedAt, x.ActionType, x.Summary }), JsonOptions),
            PreventiveActions = "Tune monitoring thresholds, validate rollback readiness, and update runbook.",
            Status = WorkStatus.Draft,
            CreatedBy = createdBy
        };
        store.PostIncidentReviews.Add(review);
        AddIncidentAction(incident, IncidentActionType.CreatePostIncidentReview, createdBy, $"Post-incident review {review.ReviewNo} created.", JsonSerializer.Serialize(new { reviewId = review.Id, articleId = article.Id }, JsonOptions));
        AddIncidentAction(incident, IncidentActionType.UpdateKnowledge, createdBy, $"Knowledge article {article.Title} drafted.", JsonSerializer.Serialize(new { articleId = article.Id }, JsonOptions));
        audit.Write(customerId, incident.ProjectId, "OBSERVABILITY_POST_INCIDENT_REVIEW_CREATED", nameof(PostIncidentReview), review.Id, review);
        return review;
    }

    public AlertEvent AcknowledgeAlert(Guid customerId, Guid? projectId, Guid alertId, string actor)
    {
        var alert = store.AlertEvents.SingleOrDefault(x => x.CustomerId == customerId && x.Id == alertId && (!projectId.HasValue || x.ProjectId == projectId || x.ProjectId == null))
            ?? throw new InvalidOperationException("Alert not found.");
        alert.Status = AlertStatus.Acknowledged;
        alert.AcknowledgedAt = DateTimeOffset.UtcNow;
        alert.AcknowledgedBy = actor;
        audit.Write(customerId, alert.ProjectId, "OBSERVABILITY_ALERT_ACKNOWLEDGED", nameof(AlertEvent), alert.Id, alert);
        return alert;
    }

    public IncidentRecord ResolveIncident(Guid customerId, Guid? projectId, Guid incidentId, string actor, string resolution)
    {
        var incident = ResolveIncident(customerId, projectId, incidentId);
        incident.Status = IncidentStatus.Resolved;
        incident.CurrentMitigation = masking.Mask(resolution);
        incident.ResolvedAt = DateTimeOffset.UtcNow;
        var binding = store.IncidentSlaBindings.FirstOrDefault(x => x.CustomerId == customerId && x.IncidentId == incident.Id);
        if (binding is not null)
        {
            binding.Status = DateTimeOffset.UtcNow <= binding.ResolutionDueAt ? SlaStatus.Met : SlaStatus.Breached;
            binding.ResolvedAt = DateTimeOffset.UtcNow;
        }
        AddIncidentAction(incident, IncidentActionType.ManualNote, actor, $"Incident resolved: {resolution}", "{}");
        audit.Write(customerId, incident.ProjectId, "OBSERVABILITY_INCIDENT_RESOLVED", nameof(IncidentRecord), incident.Id, incident);
        return incident;
    }

    private IncidentRecord CreateIncidentFromAlert(AlertEvent alert, RuntimeTelemetrySample sample, MonitoringRule rule, string actor)
    {
        var priority = alert.Severity switch { AlertSeverity.Critical => IncidentPriority.P0, AlertSeverity.High => IncidentPriority.P1, AlertSeverity.Warning => IncidentPriority.P2, _ => IncidentPriority.P3 };
        return CreateIncident(new IncidentCreateRequest(
            alert.CustomerId,
            alert.ProjectId,
            sample.EnvironmentId,
            sample.ConnectorId,
            sample.ProductionReleasePackageId,
            sample.ProductionDeploymentRunId,
            null,
            null,
            alert.Id,
            alert.Title,
            alert.Message,
            alert.Severity,
            priority,
            $"Telemetry rule {rule.RuleKey} triggered from {sample.SignalType}.",
            actor));
    }

    private void DispatchAlert(AlertEvent alert, AlertRule? alertRule, string actor)
    {
        if (alertRule is null || !alertRule.CreateNotification || !alert.ProjectId.HasValue)
        {
            return;
        }
        store.PortalNotifications.Add(new PortalNotification
        {
            CustomerId = alert.CustomerId,
            ProjectId = alert.ProjectId.Value,
            NotificationType = alert.Severity is AlertSeverity.Critical or AlertSeverity.High ? NotificationType.SlaWarning : NotificationType.SystemAnnouncement,
            Title = $"Observability alert: {alert.Severity}",
            Message = alert.Message,
            SourceEntityType = nameof(AlertEvent),
            SourceEntityId = alert.Id
        });
        audit.Write(alert.CustomerId, alert.ProjectId, "OBSERVABILITY_ALERT_NOTIFICATION_CREATED", nameof(AlertEvent), alert.Id, alert);
    }

    private void DispatchCriticalIncident(IncidentRecord incident, string actor)
    {
        if (!incident.ProjectId.HasValue)
        {
            return;
        }
        store.PortalNotifications.Add(new PortalNotification
        {
            CustomerId = incident.CustomerId,
            ProjectId = incident.ProjectId.Value,
            NotificationType = NotificationType.SlaBreached,
            Title = $"Critical incident {incident.IncidentNo}",
            Message = incident.Title,
            SourceEntityType = nameof(IncidentRecord),
            SourceEntityId = incident.Id
        });
        store.CollaborationTasks.Add(new CollaborationTask
        {
            CustomerId = incident.CustomerId,
            ProjectId = incident.ProjectId.Value,
            TaskNo = store.NextNumber("TASK"),
            Title = $"Critical incident response: {incident.IncidentNo}",
            Description = incident.Description,
            AssigneeUserId = "sre.oncall",
            AssigneeType = NotificationRecipientType.InternalUser,
            Priority = CollaborationTaskPriority.Critical,
            DueAt = DateTimeOffset.UtcNow.AddMinutes(30),
            SourceEntityType = nameof(IncidentRecord),
            SourceEntityId = incident.Id,
            Escalated = true
        });
        store.EscalationEvents.Add(new EscalationEvent
        {
            CustomerId = incident.CustomerId,
            ProjectId = incident.ProjectId.Value,
            SourceEntityType = nameof(IncidentRecord),
            SourceEntityId = incident.Id,
            Reason = "Critical observability incident",
            EscalatedToUserId = "sre.manager",
            Status = EscalationStatus.Notified
        });
        AddIncidentAction(incident, IncidentActionType.Escalate, actor, "Critical incident notification, task and escalation created.", "{}");
    }

    private void AttachSlaIfAvailable(IncidentRecord incident)
    {
        if (incident.SlaPolicyId is null)
        {
            var severity = incident.Severity switch { AlertSeverity.Critical => IssueSeverity.Critical, AlertSeverity.High => IssueSeverity.High, AlertSeverity.Warning => IssueSeverity.Medium, _ => IssueSeverity.Low };
            incident.SlaPolicyId = store.SlaPolicies
                .Where(x => x.CustomerId == incident.CustomerId && x.Severity == severity)
                .OrderBy(x => x.ResponseHours)
                .FirstOrDefault()?.Id;
        }
        if (!incident.SlaPolicyId.HasValue || store.IncidentSlaBindings.Any(x => x.CustomerId == incident.CustomerId && x.IncidentId == incident.Id))
        {
            return;
        }
        var policy = store.SlaPolicies.SingleOrDefault(x => x.CustomerId == incident.CustomerId && x.Id == incident.SlaPolicyId.Value);
        if (policy is null)
        {
            return;
        }
        store.IncidentSlaBindings.Add(new IncidentSlaBinding
        {
            CustomerId = incident.CustomerId,
            ProjectId = incident.ProjectId,
            IncidentId = incident.Id,
            SlaPolicyId = policy.Id,
            ResponseDueAt = incident.DetectedAt.AddHours(policy.ResponseHours),
            ResolutionDueAt = incident.DetectedAt.AddHours(policy.ResolutionHours),
            Status = SlaStatus.OnTrack
        });
        AddIncidentAction(incident, IncidentActionType.AttachSla, "system", $"SLA {policy.PolicyNo} attached.", JsonSerializer.Serialize(policy, JsonOptions));
    }

    private void AddIncidentAction(IncidentRecord incident, IncidentActionType type, string actor, string summary, string resultJson)
    {
        store.IncidentActions.Add(new IncidentAction
        {
            CustomerId = incident.CustomerId,
            ProjectId = incident.ProjectId,
            IncidentId = incident.Id,
            ActionType = type,
            ActorUserId = actor,
            Summary = masking.Mask(summary),
            ResultJson = Limit(masking.Mask(resultJson), 4000)
        });
    }

    private bool RuleMatches(MonitoringRule rule, RuntimeTelemetrySample sample)
    {
        if (rule.SignalType != sample.SignalType && rule.SignalType != TelemetrySignalType.HealthCheck)
        {
            return false;
        }
        if (!string.IsNullOrWhiteSpace(rule.MetricName) && !string.Equals(rule.MetricName, sample.MetricName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (!string.IsNullOrWhiteSpace(rule.MatchText) && sample.MaskedPayloadJson.Contains(rule.MatchText, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (rule.ThresholdValue.HasValue && sample.MetricValue.HasValue)
        {
            return rule.Operator switch
            {
                MonitoringRuleOperator.GreaterThan => sample.MetricValue.Value > rule.ThresholdValue.Value,
                MonitoringRuleOperator.GreaterThanOrEqual => sample.MetricValue.Value >= rule.ThresholdValue.Value,
                MonitoringRuleOperator.LessThan => sample.MetricValue.Value < rule.ThresholdValue.Value,
                MonitoringRuleOperator.LessThanOrEqual => sample.MetricValue.Value <= rule.ThresholdValue.Value,
                MonitoringRuleOperator.Equals => sample.MetricValue.Value == rule.ThresholdValue.Value,
                _ => false
            };
        }
        return sample.HealthStatus == TelemetryHealthStatus.Unhealthy && rule.Severity is AlertSeverity.High or AlertSeverity.Critical;
    }

    private AlertRule? ResolveAlertRule(Guid customerId, Guid? projectId, MonitoringRule rule) =>
        store.AlertRules
            .Where(x => x.CustomerId == customerId && x.Active && (!projectId.HasValue || x.ProjectId == projectId || x.ProjectId == null) && (!x.MonitoringRuleId.HasValue || x.MonitoringRuleId == rule.Id) && (int)x.MinimumSeverity <= (int)rule.Severity)
            .OrderByDescending(x => x.MonitoringRuleId.HasValue)
            .FirstOrDefault();

    private TelemetrySource ResolveSource(Guid customerId, Guid? projectId, Guid sourceId) =>
        store.TelemetrySources.SingleOrDefault(x => x.CustomerId == customerId && x.Id == sourceId && x.Active && (!projectId.HasValue || x.ProjectId == projectId || x.ProjectId == null))
        ?? throw new InvalidOperationException("Telemetry source not found for this customer/project.");

    private IncidentRecord ResolveIncident(Guid customerId, Guid? projectId, Guid incidentId) =>
        store.IncidentRecords.SingleOrDefault(x => x.CustomerId == customerId && x.Id == incidentId && (!projectId.HasValue || x.ProjectId == projectId || x.ProjectId == null))
        ?? throw new InvalidOperationException("Incident not found.");

    private List<RuntimeTelemetrySample> SamplesForAlert(Guid customerId, Guid alertId)
    {
        var alert = store.AlertEvents.SingleOrDefault(x => x.CustomerId == customerId && x.Id == alertId);
        if (alert?.TelemetrySampleId is null)
        {
            return [];
        }
        var sample = store.RuntimeTelemetrySamples.SingleOrDefault(x => x.CustomerId == customerId && x.Id == alert.TelemetrySampleId.Value);
        return sample is null
            ? []
            : store.RuntimeTelemetrySamples.Where(x => x.CustomerId == customerId && x.TelemetrySourceId == sample.TelemetrySourceId).OrderByDescending(x => x.ObservedAt).Take(5).ToList();
    }

    private void ValidateScope(Guid customerId, Guid? projectId, Guid? environmentId, Guid? connectorId)
    {
        if (projectId.HasValue && !store.Projects.Any(x => x.CustomerId == customerId && x.Id == projectId.Value))
        {
            throw new InvalidOperationException("Project does not belong to customer.");
        }
        if (environmentId.HasValue && !store.Environments.Any(x => x.CustomerId == customerId && x.Id == environmentId.Value && (!projectId.HasValue || x.ProjectId == projectId.Value)))
        {
            throw new InvalidOperationException("Environment does not belong to customer/project.");
        }
        if (connectorId.HasValue && !store.CustomerConnectors.Any(x => x.CustomerId == customerId && x.Id == connectorId.Value && (!projectId.HasValue || x.ProjectId == projectId.Value)))
        {
            throw new InvalidOperationException("Connector does not belong to customer/project.");
        }
    }

    private static IReadOnlyList<RuntimeTelemetrySample> LatestBySource(IReadOnlyList<RuntimeTelemetrySample> samples) =>
        samples.GroupBy(x => x.TelemetrySourceId).Select(x => x.OrderByDescending(y => y.ObservedAt).First()).ToArray();

    private static string BuildRootCause(IncidentRecord incident, IReadOnlyList<RuntimeTelemetrySample> samples)
    {
        var text = $"{incident.Title} {incident.Description} {string.Join(" ", samples.Select(x => x.Summary))}";
        if (text.Contains("deployment", StringComparison.OrdinalIgnoreCase)) return "Likely deployment validation or release health regression.";
        if (text.Contains("database", StringComparison.OrdinalIgnoreCase)) return "Likely database health or query latency degradation.";
        if (text.Contains("connector", StringComparison.OrdinalIgnoreCase) || text.Contains("integration", StringComparison.OrdinalIgnoreCase)) return "Likely connector or integration endpoint degradation.";
        if (text.Contains("latency", StringComparison.OrdinalIgnoreCase)) return "Likely API latency spike from runtime dependency pressure.";
        return "Likely runtime health degradation; validate recent telemetry and release activity.";
    }

    private static string Limit(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value ?? "" : value[..max];

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
