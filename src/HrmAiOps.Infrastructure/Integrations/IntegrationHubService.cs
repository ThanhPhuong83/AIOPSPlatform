using System.Text.Json;
using System.Text.Json.Serialization;
using HrmAiOps.Application.Abstractions;
using HrmAiOps.Domain.Core;

namespace HrmAiOps.Infrastructure.Integrations;

public sealed class IntegrationHubService(
    IAppStore store,
    IAuditWriter audit,
    IDataMaskingService masking,
    IBackgroundJobDispatcher jobs) : IIntegrationHubService
{
    private readonly JsonSerializerOptions _jsonOptions = CreateJsonOptions();

    public object GetDashboard(IntegrationDashboardFilter filter)
    {
        var providers = store.IntegrationProviders.Where(x => x.CustomerId == filter.CustomerId && (!filter.ProjectId.HasValue || x.ProjectId == null || x.ProjectId == filter.ProjectId)).ToList();
        var providerIds = providers.Select(x => x.Id).ToHashSet();
        var runs = store.IntegrationRuns.Where(x => x.CustomerId == filter.CustomerId && (!filter.ProjectId.HasValue || x.ProjectId == null || x.ProjectId == filter.ProjectId)).ToList();
        var failed = runs.Count(x => x.Status is IntegrationRunStatus.Failed or IntegrationRunStatus.TimedOut or IntegrationRunStatus.Rejected);
        return new
        {
            filter,
            providers = providers.Count,
            activeEndpoints = store.IntegrationEndpoints.Count(x => x.CustomerId == filter.CustomerId && providerIds.Contains(x.ProviderId) && x.Active),
            inboundWebhooks = store.IntegrationEndpoints.Count(x => x.CustomerId == filter.CustomerId && providerIds.Contains(x.ProviderId) && x.Direction == IntegrationDirection.Inbound && x.Active),
            outboundSubscriptions = store.WebhookOutboundSubscriptions.Count(x => x.CustomerId == filter.CustomerId && providerIds.Contains(x.ProviderId) && x.Active),
            gatewayRoutes = store.ApiGatewayRoutes.Count(x => x.CustomerId == filter.CustomerId && (!filter.ProjectId.HasValue || x.ProjectId == null || x.ProjectId == filter.ProjectId) && x.Active),
            runs = runs.Count,
            failedRuns = failed,
            retryingRuns = runs.Count(x => x.Status == IntegrationRunStatus.Retrying),
            successRate = runs.Count == 0 ? 100 : Math.Round((decimal)runs.Count(x => x.Status == IntegrationRunStatus.Succeeded) / runs.Count * 100, 2),
            latestRuns = runs.OrderByDescending(x => x.StartedAt).Take(10),
            latestErrors = runs.Where(x => x.Status is IntegrationRunStatus.Failed or IntegrationRunStatus.TimedOut or IntegrationRunStatus.Rejected).OrderByDescending(x => x.StartedAt).Take(5)
        };
    }

    public async Task<IntegrationRun> ExecuteOutboundAsync(IntegrationActionRequest request, CancellationToken cancellationToken)
    {
        var endpoint = store.IntegrationEndpoints.SingleOrDefault(x => x.CustomerId == request.CustomerId && x.Id == request.EndpointId && x.Active)
            ?? throw new InvalidOperationException("Integration endpoint not found.");
        if (endpoint.Direction != IntegrationDirection.Outbound)
        {
            throw new InvalidOperationException("Endpoint is not configured for outbound integration.");
        }
        if (request.ProjectId.HasValue && endpoint.ProjectId.HasValue && endpoint.ProjectId != request.ProjectId)
        {
            throw new InvalidOperationException("Endpoint belongs to another project.");
        }
        ValidateSecretRef(endpoint.AuthType, endpoint.SecretRef);
        ValidateTimeout(endpoint.TimeoutSeconds);

        var provider = ResolveProvider(request.CustomerId, endpoint.ProviderId);
        var subscription = store.WebhookOutboundSubscriptions
            .Where(x => x.CustomerId == request.CustomerId && x.Active && x.ProviderId == provider.Id && x.EventType == request.EventType && (!request.ProjectId.HasValue || x.ProjectId == null || x.ProjectId == request.ProjectId))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();
        var maxAttempts = subscription?.MaxRetryAttempts ?? (provider.SupportsRetry ? 3 : 1);
        var run = CreateRun(request.CustomerId, request.ProjectId ?? endpoint.ProjectId, provider.Id, endpoint.Id, null, subscription?.Id, null, IntegrationDirection.Outbound, request.EventType, request.PayloadJson, endpoint.TimeoutSeconds, maxAttempts, request.CorrelationId);
        await jobs.EnqueueAsync("integration.outbound", new { runId = run.Id, request.CustomerId, endpointId = endpoint.Id }, cancellationToken);
        CompleteMockRun(run, endpoint.PathOrUrl, endpoint.MaskOutboundPayloads, subscription?.RetryBackoffSeconds ?? 30);
        audit.Write(run.CustomerId, run.ProjectId, "INTEGRATION_OUTBOUND_EXECUTED", nameof(IntegrationRun), run.Id, run);
        HandleFailureAutomation(run, request.RequestedBy);
        return run;
    }

    public Task<IntegrationRun> ReceiveWebhookAsync(InboundWebhookRequest request, CancellationToken cancellationToken)
    {
        var provider = ResolveProvider(request.CustomerId, request.ProviderId);
        var endpoint = request.EndpointId.HasValue
            ? store.IntegrationEndpoints.SingleOrDefault(x => x.CustomerId == request.CustomerId && x.Id == request.EndpointId.Value && x.ProviderId == provider.Id && x.Active)
            : store.IntegrationEndpoints.FirstOrDefault(x => x.CustomerId == request.CustomerId && x.ProviderId == provider.Id && x.Direction == IntegrationDirection.Inbound && x.Active);
        if (endpoint is null)
        {
            throw new InvalidOperationException("Inbound endpoint not found.");
        }
        if (endpoint.Direction != IntegrationDirection.Inbound)
        {
            throw new InvalidOperationException("Endpoint is not configured for inbound webhook.");
        }
        ValidateTimeout(endpoint.TimeoutSeconds);
        var signatureVerified = VerifySignature(provider, endpoint, request.Signature);
        var run = CreateRun(request.CustomerId, request.ProjectId ?? endpoint.ProjectId, provider.Id, endpoint.Id, null, null, null, IntegrationDirection.Inbound, request.EventType, request.PayloadJson, endpoint.TimeoutSeconds, 1, request.CorrelationId);
        if (!signatureVerified)
        {
            RejectRun(run, "Webhook signature verification failed.");
        }
        else
        {
            run.Status = IntegrationRunStatus.Succeeded;
            run.ResponseSummary = "Inbound webhook accepted by mock provider.";
            run.CompletedAt = DateTimeOffset.UtcNow;
            AddLog(run, "Info", "Inbound webhook signature accepted and payload mapped.", run.MaskedPayload);
        }
        audit.Write(run.CustomerId, run.ProjectId, "INTEGRATION_WEBHOOK_RECEIVED", nameof(IntegrationRun), run.Id, new { run, signatureVerified });
        HandleFailureAutomation(run, request.RequestedBy);
        return Task.FromResult(run);
    }

    public Task<IntegrationRun> InvokeGatewayAsync(GatewayInvocationRequest request, CancellationToken cancellationToken)
    {
        var route = store.ApiGatewayRoutes.SingleOrDefault(x => x.CustomerId == request.CustomerId && x.Id == request.RouteId && x.Active)
            ?? throw new InvalidOperationException("API gateway route not found.");
        if (request.ProjectId.HasValue && route.ProjectId.HasValue && route.ProjectId != request.ProjectId)
        {
            throw new InvalidOperationException("Gateway route belongs to another project.");
        }
        ValidateSecretRef(IntegrationAuthType.SecretRefToken, route.TokenSecretRef);
        ValidateTimeout(route.TimeoutSeconds);
        var externalAllowed = string.Equals(route.AllowedExternalSystem, request.ExternalSystem, StringComparison.OrdinalIgnoreCase);
        var tokenAllowed = string.Equals(route.TokenSecretRef, request.TokenSecretRef, StringComparison.OrdinalIgnoreCase);
        var run = CreateRun(request.CustomerId, request.ProjectId ?? route.ProjectId, null, null, null, null, route.Id, IntegrationDirection.Gateway, IntegrationEventType.GatewayInvoked, request.PayloadJson, route.TimeoutSeconds, 1, request.CorrelationId);
        if (!externalAllowed || !tokenAllowed)
        {
            RejectRun(run, "API gateway access policy rejected external call.");
        }
        else
        {
            run.Status = IntegrationRunStatus.Succeeded;
            run.ResponseSummary = $"Gateway route {route.RouteKey} invoked internal target {route.InternalTarget}.";
            run.CompletedAt = DateTimeOffset.UtcNow;
            AddLog(run, "Info", "API gateway policy accepted external call.", run.MaskedPayload);
        }
        audit.Write(run.CustomerId, run.ProjectId, "INTEGRATION_GATEWAY_INVOKED", nameof(IntegrationRun), run.Id, new { run, externalAllowed, tokenAllowed });
        HandleFailureAutomation(run, request.RequestedBy);
        return Task.FromResult(run);
    }

    public async Task<IReadOnlyList<IntegrationRun>> ProcessRetriesAsync(Guid customerId, Guid? projectId, CancellationToken cancellationToken)
    {
        var due = store.IntegrationRuns
            .Where(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == projectId) && x.Status == IntegrationRunStatus.Retrying && x.NextRetryAt <= DateTimeOffset.UtcNow)
            .OrderBy(x => x.NextRetryAt)
            .ToList();
        foreach (var run in due)
        {
            run.Attempt += 1;
            run.Status = IntegrationRunStatus.Running;
            AddLog(run, "Info", $"Retry attempt {run.Attempt} started.", run.MaskedPayload);
            await jobs.EnqueueAsync("integration.retry", new { run.Id, run.Attempt }, cancellationToken);
            if (run.Attempt >= run.MaxAttempts)
            {
                run.Status = IntegrationRunStatus.Failed;
                run.ErrorMessage = "Mock retry policy exhausted.";
                run.CompletedAt = DateTimeOffset.UtcNow;
                run.NextRetryAt = null;
                AddLog(run, "Error", "Retry policy exhausted.", run.MaskedPayload);
                HandleFailureAutomation(run, "integration.retry");
            }
            else
            {
                run.Status = IntegrationRunStatus.Succeeded;
                run.ResponseSummary = $"Retry attempt {run.Attempt} succeeded.";
                run.CompletedAt = DateTimeOffset.UtcNow;
                run.NextRetryAt = null;
                AddLog(run, "Info", "Retry completed successfully.", run.MaskedPayload);
            }
            audit.Write(run.CustomerId, run.ProjectId, "INTEGRATION_RETRY_PROCESSED", nameof(IntegrationRun), run.Id, run);
        }
        return due;
    }

    private IntegrationProvider ResolveProvider(Guid customerId, Guid providerId) =>
        store.IntegrationProviders.SingleOrDefault(x => x.CustomerId == customerId && x.Id == providerId && x.Active)
        ?? throw new InvalidOperationException("Integration provider not found.");

    private IntegrationRun CreateRun(Guid customerId, Guid? projectId, Guid? providerId, Guid? endpointId, Guid? eventSubscriptionId, Guid? webhookSubscriptionId, Guid? routeId, IntegrationDirection direction, IntegrationEventType eventType, string payloadJson, int timeoutSeconds, int maxAttempts, string? correlationId)
    {
        var maskedPayload = masking.Mask(payloadJson);
        var run = new IntegrationRun
        {
            CustomerId = customerId,
            ProjectId = projectId,
            ProviderId = providerId,
            EndpointId = endpointId,
            EventSubscriptionId = eventSubscriptionId,
            WebhookSubscriptionId = webhookSubscriptionId,
            ApiGatewayRouteId = routeId,
            Direction = direction,
            EventType = eventType,
            Status = IntegrationRunStatus.Running,
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId,
            TraceId = Guid.NewGuid().ToString("N"),
            TimeoutSeconds = timeoutSeconds,
            MaxAttempts = Math.Max(1, maxAttempts),
            RequestSummary = $"{direction} {eventType}",
            MaskedPayload = maskedPayload.Length > 2000 ? maskedPayload[..2000] : maskedPayload,
            StartedAt = DateTimeOffset.UtcNow
        };
        store.IntegrationRuns.Add(run);
        AddLog(run, "Info", "Integration run started.", run.MaskedPayload);
        return run;
    }

    private void CompleteMockRun(IntegrationRun run, string pathOrUrl, bool maskPayload, int retryBackoffSeconds)
    {
        if (run.TimeoutSeconds <= 0 || ContainsAny(run.MaskedPayload, "simulateTimeout", "timeout:true"))
        {
            run.Status = IntegrationRunStatus.TimedOut;
            run.ErrorMessage = "Mock integration timed out.";
            run.CompletedAt = DateTimeOffset.UtcNow;
            AddLog(run, "Error", "Integration timeout occurred.", run.MaskedPayload);
            return;
        }
        if (ContainsAny(run.MaskedPayload, "forceFailure", "fail:true") || pathOrUrl.Contains("fail", StringComparison.OrdinalIgnoreCase))
        {
            if (run.Attempt < run.MaxAttempts)
            {
                run.Status = IntegrationRunStatus.Retrying;
                run.ErrorMessage = "Mock provider failure; retry scheduled.";
                run.NextRetryAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(1, retryBackoffSeconds));
                AddLog(run, "Warning", "Outbound integration failed and retry was scheduled.", run.MaskedPayload);
            }
            else
            {
                run.Status = IntegrationRunStatus.Failed;
                run.ErrorMessage = "Mock provider failure.";
                run.CompletedAt = DateTimeOffset.UtcNow;
                AddLog(run, "Error", "Outbound integration failed.", run.MaskedPayload);
            }
            return;
        }

        run.Status = IntegrationRunStatus.Succeeded;
        run.ResponseSummary = maskPayload
            ? "Mock outbound integration accepted masked payload."
            : "Mock outbound integration accepted payload.";
        run.CompletedAt = DateTimeOffset.UtcNow;
        AddLog(run, "Info", "Outbound integration completed.", run.MaskedPayload);
    }

    private bool VerifySignature(IntegrationProvider provider, IntegrationEndpoint endpoint, string? signature)
    {
        if (!provider.SupportsSignatureVerification)
        {
            return true;
        }
        return endpoint.AuthType == IntegrationAuthType.WebhookSignatureSecretRef &&
            !string.IsNullOrWhiteSpace(endpoint.SecretRef) &&
            string.Equals(signature, "mock-valid-signature", StringComparison.OrdinalIgnoreCase);
    }

    private void RejectRun(IntegrationRun run, string reason)
    {
        run.Status = IntegrationRunStatus.Rejected;
        run.ErrorMessage = reason;
        run.CompletedAt = DateTimeOffset.UtcNow;
        AddLog(run, "Error", reason, run.MaskedPayload);
    }

    private void HandleFailureAutomation(IntegrationRun run, string actor)
    {
        if (run.Status is not (IntegrationRunStatus.Failed or IntegrationRunStatus.TimedOut or IntegrationRunStatus.Rejected))
        {
            return;
        }
        if (!run.ProjectId.HasValue)
        {
            return;
        }

        var triggers = store.IntegrationAutomationTriggers
            .Where(x => x.CustomerId == run.CustomerId && x.Active && x.CreateOnFailureOnly && x.EventType == run.EventType && (!x.ProjectId.HasValue || x.ProjectId == run.ProjectId) && (!x.ProviderId.HasValue || x.ProviderId == run.ProviderId))
            .ToList();
        foreach (var trigger in triggers)
        {
            if (trigger.ActionType == IntegrationAutomationActionType.CreateNotification)
            {
                store.PortalNotifications.Add(new PortalNotification
                {
                    CustomerId = run.CustomerId,
                    ProjectId = run.ProjectId.Value,
                    NotificationType = NotificationType.SystemAnnouncement,
                    Title = "Integration failure",
                    Message = $"{run.EventType} failed: {run.ErrorMessage}",
                    SourceEntityType = nameof(IntegrationRun),
                    SourceEntityId = run.Id
                });
            }
            if (trigger.ActionType == IntegrationAutomationActionType.CreateTask)
            {
                store.CollaborationTasks.Add(new CollaborationTask
                {
                    CustomerId = run.CustomerId,
                    ProjectId = run.ProjectId.Value,
                    TaskNo = store.NextNumber("TASK"),
                    Title = $"Investigate integration failure: {run.EventType}",
                    Description = $"CorrelationId={run.CorrelationId}; TraceId={run.TraceId}; Error={run.ErrorMessage}",
                    AssigneeUserId = actor,
                    AssigneeType = NotificationRecipientType.InternalUser,
                    Priority = CollaborationTaskPriority.High,
                    DueAt = DateTimeOffset.UtcNow.AddHours(4),
                    SourceEntityType = nameof(IntegrationRun),
                    SourceEntityId = run.Id
                });
            }
            AddLog(run, "Warning", $"Failure automation executed: {trigger.TriggerKey}/{trigger.ActionType}.", "{}");
        }
    }

    private void AddLog(IntegrationRun run, string level, string message, string payload)
    {
        store.IntegrationRunLogs.Add(new IntegrationRunLog
        {
            CustomerId = run.CustomerId,
            ProjectId = run.ProjectId,
            IntegrationRunId = run.Id,
            Level = level,
            Message = message,
            MaskedPayload = payload.Length > 2000 ? payload[..2000] : payload
        });
    }

    private static void ValidateSecretRef(IntegrationAuthType authType, string? secretRef)
    {
        if (authType == IntegrationAuthType.None)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(secretRef) || !secretRef.StartsWith("secret://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Integration authentication must store only secret_ref values.");
        }
    }

    private static void ValidateTimeout(int timeoutSeconds)
    {
        if (timeoutSeconds <= 0)
        {
            throw new InvalidOperationException("Integration timeout must be greater than zero.");
        }
    }

    private static bool ContainsAny(string value, params string[] tokens) =>
        tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
