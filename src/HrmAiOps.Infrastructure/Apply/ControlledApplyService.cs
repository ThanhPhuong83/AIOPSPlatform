using System.Diagnostics;
using System.Text.Json;
using HrmAiOps.Application.Abstractions;
using HrmAiOps.Domain.Core;

namespace HrmAiOps.Infrastructure.Apply;

public sealed class ControlledApplyService(IAppStore store, IAuditWriter audit, IDataMaskingService masking) : IControlledApplyService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<ApplyRun> DryRunAsync(ControlledApplyRequest request, CancellationToken cancellationToken)
    {
        var environment = RequireTestOrUatEnvironment(request.CustomerId, request.ProjectId, request.EnvironmentId);
        var source = ResolveSource(request.CustomerId, request.ProjectId, request.SourceType, request.SourceId);
        var connector = ResolveConnector(request.CustomerId, request.ProjectId, environment.Id, request.ConnectorId);
        var policy = ResolvePolicy(request.CustomerId, request.ProjectId, environment.Id, connector);

        var applyRun = new ApplyRun
        {
            CustomerId = request.CustomerId,
            ProjectId = request.ProjectId,
            EnvironmentId = environment.Id,
            ConnectorId = connector.Id,
            ApplyRunNo = store.NextNumber("TAP"),
            SourceType = request.SourceType,
            SourceId = request.SourceId,
            FixProposalId = source.FixProposalId,
            ChangeRequestId = source.ChangeRequestId,
            RiskLevel = source.RiskLevel,
            RequiresApproval = source.RiskLevel is RiskLevel.High or RiskLevel.Critical,
            RequestedBy = string.IsNullOrWhiteSpace(request.RequestedBy) ? "ops.user" : request.RequestedBy,
            Summary = $"Dry run prepared for {source.Title} on {environment.Name}."
        };
        store.ApplyRuns.Add(applyRun);

        var rollback = CreateRollbackPlan(applyRun, environment, source);
        applyRun.RollbackPlanId = rollback.Id;

        AddLog(applyRun, "Info", $"Dry run requested for {source.Title} using read/write-disabled mock TestApply executor.");
        AddStep(applyRun, 1, "Validate environment scope", ApplyStepStatus.Succeeded, "Target is Test/UAT. Production is blocked in Phase 6.");
        AddStep(applyRun, 2, "Validate connector policy", policy.AllowTestApply ? ApplyStepStatus.Succeeded : ApplyStepStatus.Failed, "Connector policy must allow TestApply.");

        if (!policy.AllowTestApply)
        {
            applyRun.Status = ApplyRunStatus.DryRunFailed;
            applyRun.CompletedAt = DateTimeOffset.UtcNow;
            AddLog(applyRun, "Error", "Connector policy does not allow TestApply.");
            audit.Write(request.CustomerId, request.ProjectId, "TEST_APPLY_DRY_RUN_FAILED", nameof(ApplyRun), applyRun.Id, applyRun);
            return Task.FromResult(applyRun);
        }

        if (applyRun.RequiresApproval && !HasApprovedApplyApproval(applyRun))
        {
            var approval = new ApprovalRequest
            {
                CustomerId = request.CustomerId,
                ProjectId = request.ProjectId,
                EntityType = nameof(ApplyRun),
                EntityId = applyRun.Id,
                TargetEnvironmentId = environment.Id,
                RequestedBy = applyRun.RequestedBy
            };
            store.ApprovalRequests.Add(approval);
            store.ApprovalSteps.Add(new ApprovalStep
            {
                CustomerId = request.CustomerId,
                ApprovalRequestId = approval.Id,
                StepOrder = 1,
                ApproverUserId = "test-apply.approver"
            });
            applyRun.ApprovalRequestId = approval.Id;
            applyRun.Status = ApplyRunStatus.ApprovalRequired;
            AddLog(applyRun, "Warning", "High/Critical risk test apply requires approval before dry run can be treated as executable.");
            audit.Write(request.CustomerId, request.ProjectId, "TEST_APPLY_APPROVAL_REQUIRED", nameof(ApplyRun), applyRun.Id, applyRun);
            return Task.FromResult(applyRun);
        }

        var connectorRun = CreateConnectorRun(request.CustomerId, request.ProjectId, environment.Id, connector.Id, "DryRun", $"Dry run {applyRun.ApplyRunNo}");
        connectorRun.Status = ConnectorRunStatus.Completed;
        connectorRun.OutputSummary = "Mock dry run completed. No customer data was changed.";
        connectorRun.MaskedPreview = masking.Mask($"{source.Title}\n{source.Description}");
        CompleteConnectorRun(connectorRun);

        AddStep(applyRun, 3, "Mock connector dry run", ApplyStepStatus.Succeeded, "No write operation was executed.");
        applyRun.Status = ApplyRunStatus.DryRunSucceeded;
        applyRun.CompletedAt = DateTimeOffset.UtcNow;
        AddLog(applyRun, "Info", "Dry run succeeded. Apply can now be executed in Test/UAT.");
        audit.Write(request.CustomerId, request.ProjectId, "TEST_APPLY_DRY_RUN_SUCCEEDED", nameof(ApplyRun), applyRun.Id, applyRun);
        return Task.FromResult(applyRun);
    }

    public Task<ApplyRun> ApplyAsync(Guid customerId, Guid projectId, Guid applyRunId, string requestedBy, CancellationToken cancellationToken)
    {
        var applyRun = store.ApplyRuns.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == applyRunId)
            ?? throw new InvalidOperationException("Apply run not found.");
        var environment = RequireTestOrUatEnvironment(customerId, projectId, applyRun.EnvironmentId);
        var connector = ResolveConnector(customerId, projectId, environment.Id, applyRun.ConnectorId);
        var policy = ResolvePolicy(customerId, projectId, environment.Id, connector);
        var source = ResolveSource(customerId, projectId, applyRun.SourceType, applyRun.SourceId);

        if (!policy.AllowTestApply)
        {
            throw new InvalidOperationException("Connector policy does not allow TestApply.");
        }

        if (applyRun.RequiresApproval && !HasApprovedApplyApproval(applyRun))
        {
            applyRun.Status = ApplyRunStatus.ApprovalRequired;
            AddLog(applyRun, "Warning", "Apply blocked because approval is still pending.");
            audit.Write(customerId, projectId, "TEST_APPLY_BLOCKED_PENDING_APPROVAL", nameof(ApplyRun), applyRun.Id, applyRun);
            return Task.FromResult(applyRun);
        }

        if (applyRun.Status is not ApplyRunStatus.DryRunSucceeded and not ApplyRunStatus.ApprovalRequired)
        {
            throw new InvalidOperationException("Apply requires a successful dry run first.");
        }

        applyRun.Status = ApplyRunStatus.Applying;
        applyRun.StartedAt = DateTimeOffset.UtcNow;
        applyRun.CompletedAt = null;
        AddStep(applyRun, 10, "Lock apply run", ApplyStepStatus.Succeeded, "Apply run moved to Applying.");

        try
        {
            var preConnectorRun = CreateConnectorRun(customerId, projectId, environment.Id, connector.Id, "PreApplySnapshot", $"Pre snapshot for {applyRun.ApplyRunNo}");
            var preSnapshot = CreateSnapshot(applyRun, connector.Id, preConnectorRun.Id, SnapshotStage.PreApply, source, "Before controlled test apply.");
            CompleteConnectorRun(preConnectorRun);
            applyRun.PreSnapshotId = preSnapshot.Id;
            AddStep(applyRun, 11, "Create pre-apply snapshot", ApplyStepStatus.Succeeded, preSnapshot.MaskedSummary);

            var applyConnectorRun = CreateConnectorRun(customerId, projectId, environment.Id, connector.Id, "ControlledTestApply", $"Apply {applyRun.ApplyRunNo}");
            AddStep(applyRun, 12, "Execute mock TestApply", ApplyStepStatus.Succeeded, "Mock connector applied a controlled change to Test/UAT metadata only.");
            applyConnectorRun.Status = ConnectorRunStatus.Completed;
            applyConnectorRun.OutputSummary = "Controlled mock apply completed.";
            applyConnectorRun.MaskedPreview = masking.Mask(source.Description);
            CompleteConnectorRun(applyConnectorRun);

            var postConnectorRun = CreateConnectorRun(customerId, projectId, environment.Id, connector.Id, "PostApplySnapshot", $"Post snapshot for {applyRun.ApplyRunNo}");
            var postSnapshot = CreateSnapshot(applyRun, connector.Id, postConnectorRun.Id, SnapshotStage.PostApply, source, "After controlled test apply.");
            CompleteConnectorRun(postConnectorRun);
            applyRun.PostSnapshotId = postSnapshot.Id;
            AddStep(applyRun, 13, "Create post-apply snapshot", ApplyStepStatus.Succeeded, postSnapshot.MaskedSummary);

            var diff = CreateDiff(applyRun, preSnapshot, postSnapshot, source);
            applyRun.SnapshotDiffId = diff.Id;
            AddStep(applyRun, 14, "Compare snapshot diff", ApplyStepStatus.Succeeded, diff.DiffSummary);

            var regression = RunRegression(applyRun, source);
            applyRun.RegressionTestRunId = regression.Id;
            AddStep(applyRun, 15, "Run regression automation", regression.Status == RegressionRunStatus.Passed ? ApplyStepStatus.Succeeded : ApplyStepStatus.Failed, regression.Summary);

            if (regression.Status != RegressionRunStatus.Passed)
            {
                applyRun.Status = ApplyRunStatus.RegressionFailed;
                applyRun.CompletedAt = DateTimeOffset.UtcNow;
                AddLog(applyRun, "Error", "Regression failed. Release readiness report was not marked Ready.");
                audit.Write(customerId, projectId, "TEST_APPLY_REGRESSION_FAILED", nameof(ApplyRun), applyRun.Id, applyRun);
                return Task.FromResult(applyRun);
            }

            var readiness = new ReleaseReadinessReport
            {
                CustomerId = customerId,
                ProjectId = projectId,
                EnvironmentId = environment.Id,
                ApplyRunId = applyRun.Id,
                SnapshotDiffId = diff.Id,
                RegressionTestRunId = regression.Id,
                ReportNo = store.NextNumber("RRR"),
                Status = ReleaseReadinessStatus.Ready,
                Summary = "Test/UAT apply completed, diff reviewed, regression passed. Ready for release planning only; no production release executed.",
                Blockers = ""
            };
            store.ReleaseReadinessReports.Add(readiness);
            applyRun.ReleaseReadinessReportId = readiness.Id;
            applyRun.Status = ApplyRunStatus.ReleaseReady;
            applyRun.CompletedAt = DateTimeOffset.UtcNow;
            applyRun.Summary = readiness.Summary;
            AddLog(applyRun, "Info", readiness.Summary);
            audit.Write(customerId, projectId, "TEST_APPLY_RELEASE_READY", nameof(ApplyRun), applyRun.Id, applyRun);
            return Task.FromResult(applyRun);
        }
        catch (Exception ex)
        {
            applyRun.Status = ApplyRunStatus.Failed;
            applyRun.CompletedAt = DateTimeOffset.UtcNow;
            AddStep(applyRun, 99, "Mark failure and recommend rollback", ApplyStepStatus.Failed, ex.Message);
            AddLog(applyRun, "Error", $"Apply failed. Rollback recommended. {ex.Message}");
            audit.Write(customerId, projectId, "TEST_APPLY_FAILED", nameof(ApplyRun), applyRun.Id, applyRun);
            return Task.FromResult(applyRun);
        }
    }

    private ProjectEnvironment RequireTestOrUatEnvironment(Guid customerId, Guid projectId, Guid environmentId)
    {
        var environment = store.Environments.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == environmentId)
            ?? throw new InvalidOperationException("Environment not found.");
        if (environment.Kind is not (EnvironmentKind.Test or EnvironmentKind.Uat))
        {
            throw new InvalidOperationException("Phase 6 only allows controlled apply to Test or UAT environments. Production is blocked.");
        }

        return environment;
    }

    private SourceContext ResolveSource(Guid customerId, Guid projectId, string sourceType, Guid sourceId)
    {
        if (string.Equals(sourceType, nameof(FixProposal), StringComparison.OrdinalIgnoreCase))
        {
            var fix = store.FixProposals.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == sourceId)
                ?? throw new InvalidOperationException("Fix proposal not found.");
            return new SourceContext(nameof(FixProposal), fix.Id, fix.Id, null, fix.RiskLevel, fix.Title, fix.ProposedSolution);
        }

        if (string.Equals(sourceType, nameof(ChangeRequest), StringComparison.OrdinalIgnoreCase))
        {
            var change = store.ChangeRequests.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == sourceId)
                ?? throw new InvalidOperationException("Change request not found.");
            var risk = RiskLevel.Medium;
            if (change.FixProposalId.HasValue)
            {
                risk = store.FixProposals.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == change.FixProposalId.Value)?.RiskLevel ?? risk;
            }
            else if (change.IssueId.HasValue)
            {
                risk = store.Issues.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == change.IssueId.Value)?.RiskLevel ?? risk;
            }

            return new SourceContext(nameof(ChangeRequest), change.Id, change.FixProposalId, change.Id, risk, change.Title, change.Description);
        }

        throw new InvalidOperationException("Controlled apply source must be FixProposal or ChangeRequest.");
    }

    private CustomerConnector ResolveConnector(Guid customerId, Guid projectId, Guid environmentId, Guid? connectorId)
    {
        if (connectorId.HasValue)
        {
            return store.CustomerConnectors.SingleOrDefault(x =>
                x.CustomerId == customerId &&
                x.Id == connectorId.Value &&
                (x.ProjectId == projectId || x.ProjectId is null) &&
                (x.EnvironmentId == environmentId || x.EnvironmentId is null))
                ?? throw new InvalidOperationException("Connector not found for customer/project/environment.");
        }

        return store.CustomerConnectors.FirstOrDefault(x =>
            x.CustomerId == customerId &&
            (x.ProjectId == projectId || x.ProjectId is null) &&
            (x.EnvironmentId == environmentId || x.EnvironmentId is null) &&
            x.Status == "Active")
            ?? CreateMockConnector(customerId, projectId, environmentId);
    }

    private CustomerConnector CreateMockConnector(Guid customerId, Guid projectId, Guid environmentId)
    {
        var policy = new ConnectorPermissionPolicy
        {
            CustomerId = customerId,
            ProjectId = projectId,
            EnvironmentId = environmentId,
            Name = "Mock Test/UAT Apply Policy",
            AllowTestApply = true
        };
        store.ConnectorPermissionPolicies.Add(policy);
        var connector = new CustomerConnector
        {
            CustomerId = customerId,
            ProjectId = projectId,
            EnvironmentId = environmentId,
            PermissionPolicyId = policy.Id,
            ConnectorType = "MockTestApply",
            Name = "Local Mock Test Apply Connector",
            SecretRef = "secret://mock/test-apply",
            ConfigJson = """{"mode":"mock","writeScope":"test-uat-only"}""",
            LastHealthStatus = "Healthy"
        };
        store.CustomerConnectors.Add(connector);
        audit.Write(customerId, projectId, "MOCK_TEST_APPLY_CONNECTOR_CREATED", nameof(CustomerConnector), connector.Id, connector);
        return connector;
    }

    private ConnectorPermissionPolicy ResolvePolicy(Guid customerId, Guid projectId, Guid environmentId, CustomerConnector connector)
    {
        if (connector.PermissionPolicyId.HasValue)
        {
            var policy = store.ConnectorPermissionPolicies.SingleOrDefault(x =>
                x.CustomerId == customerId &&
                x.ProjectId == projectId &&
                x.Id == connector.PermissionPolicyId.Value);
            if (policy is not null)
            {
                return policy;
            }
        }

        var fallback = store.ConnectorPermissionPolicies.FirstOrDefault(x =>
            x.CustomerId == customerId &&
            x.ProjectId == projectId &&
            (x.EnvironmentId == environmentId || x.EnvironmentId is null) &&
            x.Status == "Active");
        if (fallback is not null)
        {
            return fallback;
        }

        var defaultPolicy = new ConnectorPermissionPolicy
        {
            CustomerId = customerId,
            ProjectId = projectId,
            EnvironmentId = environmentId,
            Name = "Default Mock TestApply Policy",
            AllowTestApply = true
        };
        store.ConnectorPermissionPolicies.Add(defaultPolicy);
        connector.PermissionPolicyId = defaultPolicy.Id;
        return defaultPolicy;
    }

    private RollbackPlan CreateRollbackPlan(ApplyRun applyRun, ProjectEnvironment environment, SourceContext source)
    {
        var rollback = new RollbackPlan
        {
            CustomerId = applyRun.CustomerId,
            ProjectId = applyRun.ProjectId,
            EnvironmentId = environment.Id,
            ApplyRunId = applyRun.Id,
            PlanNo = store.NextNumber("RBP"),
            Strategy = "Restore pre-apply mock snapshot and revert controlled Test/UAT configuration changes.",
            Steps = $"1. Stop further test apply actions for {environment.Name}.\n2. Restore pre-apply snapshot.\n3. Re-run smoke tests for {source.Title}.\n4. Confirm no production action was executed.",
            ValidationChecklist = "Pre/post snapshot available; connector run logs reviewed; no production environment touched."
        };
        store.RollbackPlans.Add(rollback);
        audit.Write(applyRun.CustomerId, applyRun.ProjectId, "ROLLBACK_PLAN_CREATED", nameof(RollbackPlan), rollback.Id, rollback);
        return rollback;
    }

    private ConnectorRun CreateConnectorRun(Guid customerId, Guid projectId, Guid environmentId, Guid connectorId, string runType, string inputSummary)
    {
        var run = new ConnectorRun
        {
            CustomerId = customerId,
            ProjectId = projectId,
            EnvironmentId = environmentId,
            ConnectorId = connectorId,
            RunType = runType,
            Status = ConnectorRunStatus.Running,
            StartedAt = DateTimeOffset.UtcNow,
            InputSummary = inputSummary
        };
        store.ConnectorRuns.Add(run);
        audit.Write(customerId, projectId, "CONNECTOR_RUN_STARTED", nameof(ConnectorRun), run.Id, run);
        return run;
    }

    private void CompleteConnectorRun(ConnectorRun run)
    {
        run.Status = ConnectorRunStatus.Completed;
        run.CompletedAt = DateTimeOffset.UtcNow;
        run.DurationMs = (int)Math.Max(1, (run.CompletedAt.Value - run.StartedAt).TotalMilliseconds);
        var connector = store.CustomerConnectors.SingleOrDefault(x => x.CustomerId == run.CustomerId && x.Id == run.ConnectorId);
        if (connector is not null)
        {
            connector.LastRunAt = run.CompletedAt;
            connector.LastHealthStatus = "Healthy";
        }
        audit.Write(run.CustomerId, run.ProjectId, "CONNECTOR_RUN_COMPLETED", nameof(ConnectorRun), run.Id, run);
    }

    private EnvironmentSnapshot CreateSnapshot(ApplyRun applyRun, Guid connectorId, Guid connectorRunId, SnapshotStage stage, SourceContext source, string stageSummary)
    {
        var snapshotData = new
        {
            applyRun.ApplyRunNo,
            source.SourceType,
            source.SourceId,
            source.Title,
            source.RiskLevel,
            stage,
            stageSummary,
            capturedAt = DateTimeOffset.UtcNow,
            mockStateVersion = stage == SnapshotStage.PostApply ? 2 : 1
        };
        var json = JsonSerializer.Serialize(snapshotData, JsonOptions);
        var masked = masking.Mask($"{stage}: {source.Title}. {stageSummary}");
        var snapshot = new EnvironmentSnapshot
        {
            CustomerId = applyRun.CustomerId,
            ProjectId = applyRun.ProjectId,
            EnvironmentId = applyRun.EnvironmentId,
            ConnectorId = connectorId,
            ConnectorRunId = connectorRunId,
            ApplyRunId = applyRun.Id,
            SnapshotNo = store.NextNumber(stage == SnapshotStage.PreApply ? "PRE" : "POST"),
            Kind = SnapshotKind.ApplyComposite,
            Stage = stage,
            Summary = stageSummary,
            SnapshotJson = json,
            MaskedSummary = masked
        };
        store.EnvironmentSnapshots.Add(snapshot);
        audit.Write(applyRun.CustomerId, applyRun.ProjectId, "ENVIRONMENT_SNAPSHOT_CAPTURED", nameof(EnvironmentSnapshot), snapshot.Id, snapshot);
        return snapshot;
    }

    private SnapshotDiff CreateDiff(ApplyRun applyRun, EnvironmentSnapshot preSnapshot, EnvironmentSnapshot postSnapshot, SourceContext source)
    {
        var diff = new SnapshotDiff
        {
            CustomerId = applyRun.CustomerId,
            ProjectId = applyRun.ProjectId,
            EnvironmentId = applyRun.EnvironmentId,
            FromSnapshotId = preSnapshot.Id,
            ToSnapshotId = postSnapshot.Id,
            SnapshotKind = SnapshotKind.ApplyComposite,
            RiskLevel = source.RiskLevel,
            DiffSummary = "Mock diff detected controlled Test/UAT metadata change only. No production database/source changes were executed.",
            DiffJson = JsonSerializer.Serialize(new
            {
                changed = new[] { "testApply.mockStateVersion", "releaseReadiness.candidate" },
                source.SourceType,
                source.SourceId,
                noProductionChange = true
            }, JsonOptions)
        };
        store.SnapshotDiffs.Add(diff);
        audit.Write(applyRun.CustomerId, applyRun.ProjectId, "SNAPSHOT_DIFF_CREATED", nameof(SnapshotDiff), diff.Id, diff);
        return diff;
    }

    private RegressionTestRun RunRegression(ApplyRun applyRun, SourceContext source)
    {
        var shouldFail = source.Description.Contains("regressionfail", StringComparison.OrdinalIgnoreCase);
        var plan = store.RegressionTestPlans.FirstOrDefault(x =>
            x.CustomerId == applyRun.CustomerId &&
            x.ProjectId == applyRun.ProjectId &&
            (!applyRun.FixProposalId.HasValue || x.IssueId == store.FixProposals.FirstOrDefault(f => f.Id == applyRun.FixProposalId.Value)?.IssueId));
        var run = new RegressionTestRun
        {
            CustomerId = applyRun.CustomerId,
            ProjectId = applyRun.ProjectId,
            EnvironmentId = applyRun.EnvironmentId,
            ApplyRunId = applyRun.Id,
            RegressionTestPlanId = plan?.Id,
            RunNo = store.NextNumber("RTR"),
            Status = shouldFail ? RegressionRunStatus.Failed : RegressionRunStatus.Passed,
            TotalTests = 5,
            PassedTests = shouldFail ? 4 : 5,
            FailedTests = shouldFail ? 1 : 0,
            Summary = shouldFail
                ? "Regression failed on mock scenario. Release readiness is blocked."
                : "Regression passed for leave workflow, permission boundary, integration smoke, audit trail, and rollback readiness.",
            CompletedAt = DateTimeOffset.UtcNow
        };
        store.RegressionTestRuns.Add(run);
        audit.Write(applyRun.CustomerId, applyRun.ProjectId, "REGRESSION_TEST_RUN_COMPLETED", nameof(RegressionTestRun), run.Id, run);
        return run;
    }

    private void AddStep(ApplyRun applyRun, int order, string name, ApplyStepStatus status, string details)
    {
        store.ApplySteps.Add(new ApplyStep
        {
            CustomerId = applyRun.CustomerId,
            ProjectId = applyRun.ProjectId,
            EnvironmentId = applyRun.EnvironmentId,
            ApplyRunId = applyRun.Id,
            StepOrder = order,
            Name = name,
            Status = status,
            Details = details,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        });
    }

    private void AddLog(ApplyRun applyRun, string level, string message)
    {
        store.ApplyLogs.Add(new ApplyLog
        {
            CustomerId = applyRun.CustomerId,
            ProjectId = applyRun.ProjectId,
            EnvironmentId = applyRun.EnvironmentId,
            ApplyRunId = applyRun.Id,
            Level = level,
            Message = message,
            MaskedPayload = masking.Mask(message)
        });
    }

    private bool HasApprovedApplyApproval(ApplyRun applyRun)
    {
        if (applyRun.ApprovalRequestId.HasValue)
        {
            return store.ApprovalRequests.Any(x =>
                x.CustomerId == applyRun.CustomerId &&
                x.ProjectId == applyRun.ProjectId &&
                x.Id == applyRun.ApprovalRequestId.Value &&
                x.Status == ApprovalStatus.Approved);
        }

        return store.ApprovalRequests.Any(x =>
            x.CustomerId == applyRun.CustomerId &&
            x.ProjectId == applyRun.ProjectId &&
            x.EntityType == nameof(ApplyRun) &&
            x.EntityId == applyRun.Id &&
            x.Status == ApprovalStatus.Approved);
    }

    private sealed record SourceContext(
        string SourceType,
        Guid SourceId,
        Guid? FixProposalId,
        Guid? ChangeRequestId,
        RiskLevel RiskLevel,
        string Title,
        string Description);
}
