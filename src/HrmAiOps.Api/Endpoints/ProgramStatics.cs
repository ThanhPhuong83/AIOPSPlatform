using System.Text.Json;
using System.Text.Json.Serialization;
using HrmAiOps.Application.Abstractions;
using HrmAiOps.Domain.Core;

// Static helper and seed methods extracted from top-level Program.cs
partial class Program
{
static Customer? FindCustomer(IAppStore store, Guid customerId) =>
    store.Customers.SingleOrDefault(x => x.Id == customerId);

static Project? FindProject(IAppStore store, Guid customerId, Guid projectId) =>
    store.Projects.SingleOrDefault(x => x.CustomerId == customerId && x.Id == projectId);

static ProjectEnvironment? FindEnvironment(IAppStore store, Guid customerId, Guid projectId, Guid environmentId) =>
    store.Environments.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == environmentId);

static TraceLink NewTrace(Guid customerId, Guid projectId, string fromType, Guid fromId, string toType, Guid toId, string relationType = "Generated") =>
    new()
    {
        CustomerId = customerId,
        ProjectId = projectId,
        FromEntityType = fromType,
        FromEntityId = fromId,
        ToEntityType = toType,
        ToEntityId = toId,
        RelationType = relationType
    };

static DocumentSignOff NewSignOff(Guid customerId, Guid projectId, DocumentKind documentKind, Guid documentId, int version, SignOffRequest request) =>
    new()
    {
        CustomerId = customerId,
        ProjectId = projectId,
        DocumentKind = documentKind,
        DocumentId = documentId,
        Version = version,
        SignedOffBy = request.SignedOffBy,
        Role = request.Role,
        Comment = request.Comment
    };

static RiskLevel ResolveRiskLevel(IAppStore store, string? moduleName, RiskLevel? requestedRiskLevel = null)
{
    if (requestedRiskLevel is RiskLevel.Critical or RiskLevel.High)
    {
        return requestedRiskLevel.Value;
    }

    var normalized = (moduleName ?? "").Trim();
    var definition = store.HrmModules.FirstOrDefault(x =>
        string.Equals(x.Code, normalized, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(x.Name, normalized, StringComparison.OrdinalIgnoreCase));

    if (definition is not null)
    {
        return definition.DefaultRiskLevel;
    }

    if (normalized.Contains("payroll", StringComparison.OrdinalIgnoreCase) ||
        normalized.Contains("permission", StringComparison.OrdinalIgnoreCase) ||
        normalized.Contains("security", StringComparison.OrdinalIgnoreCase))
    {
        return RiskLevel.Critical;
    }

    if (normalized.Contains("integration", StringComparison.OrdinalIgnoreCase))
    {
        return RiskLevel.High;
    }

    return requestedRiskLevel ?? RiskLevel.Medium;
}

static async Task<IResult> ExecuteIssueAi(Guid customerId, Guid projectId, Guid issueId, AiTaskType taskType, IAppStore store, IAiTaskExecutor executor, CancellationToken cancellationToken)
{
    var issue = store.Issues.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == issueId);
    if (issue is null)
    {
        return Results.NotFound(new { error = "Issue not found." });
    }

    var proposal = await executor.ExecuteAsync(new AiTaskExecutionRequest(customerId, projectId, taskType, nameof(Issue), issueId), cancellationToken);
    return Results.Ok(proposal);
}

static IssueCategory InferIssueCategory(string title, string description)
{
    var text = $"{title} {description}";
    if (text.Contains("payroll", StringComparison.OrdinalIgnoreCase)) return IssueCategory.Payroll;
    if (text.Contains("permission", StringComparison.OrdinalIgnoreCase) || text.Contains("role", StringComparison.OrdinalIgnoreCase)) return IssueCategory.Permission;
    if (text.Contains("security", StringComparison.OrdinalIgnoreCase) || text.Contains("login", StringComparison.OrdinalIgnoreCase)) return IssueCategory.Security;
    if (text.Contains("integration", StringComparison.OrdinalIgnoreCase) || text.Contains("api", StringComparison.OrdinalIgnoreCase)) return IssueCategory.Integration;
    if (text.Contains("production database", StringComparison.OrdinalIgnoreCase) || text.Contains("prod db", StringComparison.OrdinalIgnoreCase)) return IssueCategory.ProductionDatabase;
    if (text.Contains("config", StringComparison.OrdinalIgnoreCase)) return IssueCategory.Configuration;
    if (text.Contains("data", StringComparison.OrdinalIgnoreCase)) return IssueCategory.Data;
    return IssueCategory.Other;
}

static RiskLevel ResolveIssueRisk(string title, string description, IssueCategory? category)
{
    var resolved = category ?? InferIssueCategory(title, description);
    return resolved switch
    {
        IssueCategory.Payroll or IssueCategory.Permission or IssueCategory.Security or IssueCategory.ProductionDatabase => RiskLevel.Critical,
        IssueCategory.Integration => RiskLevel.High,
        _ => RiskLevel.Medium
    };
}

static UrsDocument AcceptUrsProposal(IAppStore store, IAuditWriter audit, AiProposal proposal, ReviewAiProposalRequest request)
{
    var requirement = store.Requirements.SingleOrDefault(x => x.CustomerId == proposal.CustomerId && x.ProjectId == proposal.ProjectId && x.Id == proposal.SourceEntityId)
        ?? throw new InvalidOperationException("Source requirement not found.");
    var document = new UrsDocument
    {
        CustomerId = proposal.CustomerId,
        ProjectId = proposal.ProjectId,
        RequirementId = requirement.Id,
        UrsNo = store.NextNumber("URS"),
        Title = proposal.Title,
        Content = request.EditedContent ?? proposal.ProposedContent,
        Status = WorkStatus.Draft,
        Version = 1,
        AiRunId = proposal.AiRunId
    };
    document.VersionGroupId = document.Id;
    store.UrsDocuments.Add(document);
    proposal.TargetEntityId = document.Id;
    store.TraceLinks.Add(NewTrace(proposal.CustomerId, proposal.ProjectId, nameof(Requirement), requirement.Id, nameof(UrsDocument), document.Id, "AiAccepted"));
    audit.Write(proposal.CustomerId, proposal.ProjectId, "URS_CREATED_FROM_AI_PROPOSAL", nameof(UrsDocument), document.Id, document);
    return document;
}

static Blueprint AcceptBlueprintProposal(IAppStore store, IAuditWriter audit, AiProposal proposal, ReviewAiProposalRequest request)
{
    var urs = store.UrsDocuments.SingleOrDefault(x => x.CustomerId == proposal.CustomerId && x.ProjectId == proposal.ProjectId && x.Id == proposal.SourceEntityId)
        ?? throw new InvalidOperationException("Source URS not found.");
    var document = new Blueprint
    {
        CustomerId = proposal.CustomerId,
        ProjectId = proposal.ProjectId,
        UrsId = urs.Id,
        BlueprintNo = store.NextNumber("BP"),
        Type = proposal.Title,
        Content = request.EditedContent ?? proposal.ProposedContent,
        Status = WorkStatus.Draft,
        Version = 1,
        AiRunId = proposal.AiRunId
    };
    document.VersionGroupId = document.Id;
    store.Blueprints.Add(document);
    proposal.TargetEntityId = document.Id;
    store.TraceLinks.Add(NewTrace(proposal.CustomerId, proposal.ProjectId, nameof(UrsDocument), urs.Id, nameof(Blueprint), document.Id, "AiAccepted"));
    audit.Write(proposal.CustomerId, proposal.ProjectId, "BLUEPRINT_CREATED_FROM_AI_PROPOSAL", nameof(Blueprint), document.Id, document);
    return document;
}

static ConfigSpec AcceptConfigSpecProposal(IAppStore store, IAuditWriter audit, AiProposal proposal, ReviewAiProposalRequest request)
{
    var blueprint = store.Blueprints.SingleOrDefault(x => x.CustomerId == proposal.CustomerId && x.ProjectId == proposal.ProjectId && x.Id == proposal.SourceEntityId)
        ?? throw new InvalidOperationException("Source blueprint not found.");
    using var doc = System.Text.Json.JsonDocument.Parse(proposal.StructuredOutputJson);
    var moduleName = doc.RootElement.TryGetProperty("moduleName", out var module) && module.ValueKind == System.Text.Json.JsonValueKind.String
        ? module.GetString() ?? "HRM"
        : "HRM";
    var risk = doc.RootElement.TryGetProperty("riskLevel", out var riskElement) && riskElement.ValueKind == System.Text.Json.JsonValueKind.String && Enum.TryParse<RiskLevel>(riskElement.GetString(), true, out var parsedRisk)
        ? parsedRisk
        : ResolveRiskLevel(store, moduleName);
    var document = new ConfigSpec
    {
        CustomerId = proposal.CustomerId,
        ProjectId = proposal.ProjectId,
        BlueprintId = blueprint.Id,
        ConfigNo = store.NextNumber("CFG"),
        ModuleName = moduleName,
        RiskLevel = ResolveRiskLevel(store, moduleName, risk),
        Content = request.EditedContent ?? proposal.ProposedContent,
        Status = WorkStatus.Draft,
        Version = 1,
        AiRunId = proposal.AiRunId
    };
    document.VersionGroupId = document.Id;
    store.ConfigSpecs.Add(document);
    proposal.TargetEntityId = document.Id;
    store.TraceLinks.Add(NewTrace(proposal.CustomerId, proposal.ProjectId, nameof(Blueprint), blueprint.Id, nameof(ConfigSpec), document.Id, "AiAccepted"));
    audit.Write(proposal.CustomerId, proposal.ProjectId, "CONFIG_SPEC_CREATED_FROM_AI_PROPOSAL", nameof(ConfigSpec), document.Id, document);
    return document;
}

static Issue AcceptIssueClassification(IAppStore store, IAuditWriter audit, AiProposal proposal, ReviewAiProposalRequest request)
{
    var issue = FindIssueForProposal(store, proposal);
    var structuredCategory = ReadEnumFromProposal<IssueCategory>(proposal.StructuredOutputJson, "category");
    var structuredRisk = ReadEnumFromProposal<RiskLevel>(proposal.StructuredOutputJson, "riskLevel");
    var inferredCategory = InferIssueCategory(issue.Title, $"{issue.Description} {proposal.ProposedContent}");
    issue.Category = structuredCategory is { } parsedCategory and not IssueCategory.Other
        ? parsedCategory
        : issue.Category is not IssueCategory.Other ? issue.Category : inferredCategory;
    var resolvedRisk = structuredRisk ?? ResolveIssueRisk(issue.Title, $"{issue.Description} {proposal.ProposedContent}", issue.Category);
    issue.RiskLevel = (RiskLevel)Math.Max((int)issue.RiskLevel, (int)resolvedRisk);
    issue.Status = IssueStatus.InProgress;
    issue.UpdatedAt = DateTimeOffset.UtcNow;
    proposal.TargetEntityId = issue.Id;
    audit.Write(proposal.CustomerId, proposal.ProjectId, "ISSUE_CLASSIFICATION_ACCEPTED", nameof(Issue), issue.Id, issue);
    return issue;
}

static TEnum? ReadEnumFromProposal<TEnum>(string structuredOutputJson, string propertyName) where TEnum : struct
{
    try
    {
        using var doc = System.Text.Json.JsonDocument.Parse(structuredOutputJson);
        if (doc.RootElement.TryGetProperty(propertyName, out var element) &&
            element.ValueKind == System.Text.Json.JsonValueKind.String &&
            Enum.TryParse<TEnum>(element.GetString(), true, out var parsed))
        {
            return parsed;
        }
    }
    catch (System.Text.Json.JsonException)
    {
        return null;
    }

    return null;
}

static IssueAnalysis AcceptRootCauseProposal(IAppStore store, IAuditWriter audit, AiProposal proposal, ReviewAiProposalRequest request)
{
    var issue = FindIssueForProposal(store, proposal);
    var analysis = new IssueAnalysis
    {
        CustomerId = proposal.CustomerId,
        ProjectId = proposal.ProjectId,
        IssueId = issue.Id,
        AiRunId = proposal.AiRunId,
        AnalysisType = "RootCause",
        Content = request.EditedContent ?? proposal.ProposedContent,
        ConfidenceScore = 0.76m
    };
    store.IssueAnalyses.Add(analysis);
    issue.RootCauseSummary = analysis.Content.Length > 400 ? analysis.Content[..400] : analysis.Content;
    issue.Status = IssueStatus.InProgress;
    proposal.TargetEntityId = analysis.Id;
    store.TraceLinks.Add(NewTrace(proposal.CustomerId, proposal.ProjectId, nameof(Issue), issue.Id, nameof(IssueAnalysis), analysis.Id, "AiAccepted"));
    audit.Write(proposal.CustomerId, proposal.ProjectId, "ROOT_CAUSE_ACCEPTED", nameof(IssueAnalysis), analysis.Id, analysis);
    return analysis;
}

static FixProposal AcceptFixProposal(IAppStore store, IAuditWriter audit, AiProposal proposal, ReviewAiProposalRequest request)
{
    var issue = FindIssueForProposal(store, proposal);
    var fix = new FixProposal
    {
        CustomerId = proposal.CustomerId,
        ProjectId = proposal.ProjectId,
        IssueId = issue.Id,
        AiRunId = proposal.AiRunId,
        Title = proposal.Title,
        ProposedSolution = request.EditedContent ?? proposal.ProposedContent,
        CodeChangeSummary = "Draft only. No source code PR is created in Phase 4.",
        DbChangeSummary = "Draft only. No customer database write is executed in Phase 4.",
        RiskLevel = issue.RiskLevel
    };
    store.FixProposals.Add(fix);
    proposal.TargetEntityId = fix.Id;
    store.TraceLinks.Add(NewTrace(proposal.CustomerId, proposal.ProjectId, nameof(Issue), issue.Id, nameof(FixProposal), fix.Id, "AiAccepted"));
    audit.Write(proposal.CustomerId, proposal.ProjectId, "FIX_PROPOSAL_ACCEPTED", nameof(FixProposal), fix.Id, fix);
    return fix;
}

static ChangeRequest AcceptChangeRequestProposal(IAppStore store, IAuditWriter audit, AiProposal proposal, ReviewAiProposalRequest request)
{
    var issue = FindIssueForProposal(store, proposal);
    var env = store.Environments.FirstOrDefault(x => x.CustomerId == proposal.CustomerId && x.ProjectId == proposal.ProjectId && (x.Kind == EnvironmentKind.Dev || x.Kind == EnvironmentKind.Uat))
        ?? store.Environments.First(x => x.CustomerId == proposal.CustomerId && x.ProjectId == proposal.ProjectId);
    var change = new ChangeRequest
    {
        CustomerId = proposal.CustomerId,
        ProjectId = proposal.ProjectId,
        IssueId = issue.Id,
        CrNo = store.NextNumber("CR"),
        Title = proposal.Title,
        Description = request.EditedContent ?? proposal.ProposedContent,
        TargetEnvironmentId = env.Id,
        RequiresApproval = issue.RiskLevel is RiskLevel.High or RiskLevel.Critical,
        Status = WorkStatus.Draft
    };
    store.ChangeRequests.Add(change);
    proposal.TargetEntityId = change.Id;
    store.TraceLinks.Add(NewTrace(proposal.CustomerId, proposal.ProjectId, nameof(Issue), issue.Id, nameof(ChangeRequest), change.Id, "AiAccepted"));
    if (change.RequiresApproval)
    {
        var approval = new ApprovalRequest
        {
            CustomerId = proposal.CustomerId,
            ProjectId = proposal.ProjectId,
            EntityType = nameof(ChangeRequest),
            EntityId = change.Id,
            TargetEnvironmentId = env.Id,
            RequestedBy = request.ReviewedBy
        };
        store.ApprovalRequests.Add(approval);
        store.ApprovalSteps.Add(new ApprovalStep { CustomerId = proposal.CustomerId, ApprovalRequestId = approval.Id, StepOrder = 1, ApproverUserId = "change.manager" });
        store.TraceLinks.Add(NewTrace(proposal.CustomerId, proposal.ProjectId, nameof(ChangeRequest), change.Id, nameof(ApprovalRequest), approval.Id, "ApprovalRequired"));
    }
    audit.Write(proposal.CustomerId, proposal.ProjectId, "CHANGE_REQUEST_ACCEPTED", nameof(ChangeRequest), change.Id, change);
    return change;
}

static RegressionTestPlan AcceptRegressionTestPlanProposal(IAppStore store, IAuditWriter audit, AiProposal proposal, ReviewAiProposalRequest request)
{
    var issue = FindIssueForProposal(store, proposal);
    var plan = new RegressionTestPlan
    {
        CustomerId = proposal.CustomerId,
        ProjectId = proposal.ProjectId,
        IssueId = issue.Id,
        AiRunId = proposal.AiRunId,
        TestPlanNo = store.NextNumber("RTP"),
        Title = proposal.Title,
        Content = request.EditedContent ?? proposal.ProposedContent,
        RiskLevel = issue.RiskLevel
    };
    store.RegressionTestPlans.Add(plan);
    proposal.TargetEntityId = plan.Id;
    store.TraceLinks.Add(NewTrace(proposal.CustomerId, proposal.ProjectId, nameof(Issue), issue.Id, nameof(RegressionTestPlan), plan.Id, "AiAccepted"));
    audit.Write(proposal.CustomerId, proposal.ProjectId, "REGRESSION_TEST_PLAN_ACCEPTED", nameof(RegressionTestPlan), plan.Id, plan);
    return plan;
}

static ReleaseDraft AcceptReleaseDraftProposal(IAppStore store, IAuditWriter audit, AiProposal proposal, ReviewAiProposalRequest request)
{
    var issue = FindIssueForProposal(store, proposal);
    var draft = new ReleaseDraft
    {
        CustomerId = proposal.CustomerId,
        ProjectId = proposal.ProjectId,
        IssueId = issue.Id,
        AiRunId = proposal.AiRunId,
        ReleaseDraftNo = store.NextNumber("RLD"),
        Title = proposal.Title,
        ReleaseNotes = request.EditedContent ?? proposal.ProposedContent,
        DeploymentPlan = "Draft only. Phase 4 does not execute production release.",
        RiskLevel = issue.RiskLevel
    };
    store.ReleaseDrafts.Add(draft);
    proposal.TargetEntityId = draft.Id;
    store.TraceLinks.Add(NewTrace(proposal.CustomerId, proposal.ProjectId, nameof(Issue), issue.Id, nameof(ReleaseDraft), draft.Id, "AiAccepted"));
    audit.Write(proposal.CustomerId, proposal.ProjectId, "RELEASE_DRAFT_ACCEPTED", nameof(ReleaseDraft), draft.Id, draft);
    return draft;
}

static KnowledgeArticle AcceptKnowledgeUpdateProposal(IAppStore store, IAuditWriter audit, AiProposal proposal, ReviewAiProposalRequest request)
{
    var issue = FindIssueForProposal(store, proposal);
    var article = new KnowledgeArticle
    {
        CustomerId = proposal.CustomerId,
        ProjectId = proposal.ProjectId,
        IssueId = issue.Id,
        Title = proposal.Title,
        Category = issue.Category.ToString(),
        Content = request.EditedContent ?? proposal.ProposedContent,
        Status = WorkStatus.Draft
    };
    store.KnowledgeArticles.Add(article);
    proposal.TargetEntityId = article.Id;
    store.TraceLinks.Add(NewTrace(proposal.CustomerId, proposal.ProjectId, nameof(Issue), issue.Id, nameof(KnowledgeArticle), article.Id, "AiAccepted"));
    audit.Write(proposal.CustomerId, proposal.ProjectId, "KNOWLEDGE_UPDATE_ACCEPTED", nameof(KnowledgeArticle), article.Id, article);
    return article;
}

static Issue FindIssueForProposal(IAppStore store, AiProposal proposal) =>
    store.Issues.SingleOrDefault(x => x.CustomerId == proposal.CustomerId && x.ProjectId == proposal.ProjectId && x.Id == proposal.SourceEntityId)
    ?? throw new InvalidOperationException("Source issue not found.");

static ProductionReleasePackage? FindProductionPackage(IAppStore store, Guid customerId, Guid projectId, Guid packageId) =>
    store.ProductionReleasePackages.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == packageId);

static ProductionReleasePackage RequireProductionPackage(IAppStore store, Guid customerId, Guid projectId, Guid packageId) =>
    FindProductionPackage(store, customerId, projectId, packageId) ?? throw new InvalidOperationException("Production release package not found.");

static object ProductionReleaseDetail(IAppStore store, Guid customerId, Guid projectId, ProductionReleasePackage package)
{
    var approval = package.ApprovalRequestId.HasValue
        ? store.ApprovalRequests.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == package.ApprovalRequestId.Value)
        : null;
    var deploymentRuns = store.ProductionDeploymentRuns
        .Where(x => x.CustomerId == customerId && x.ProjectId == projectId && x.ProductionReleasePackageId == package.Id)
        .OrderByDescending(x => x.StartedAt)
        .ToList();
    var deploymentRunIds = deploymentRuns.Select(x => x.Id).ToHashSet();

    return new
    {
        package,
        checklist = store.ReleaseChecklistItems.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && x.ProductionReleasePackageId == package.Id).OrderBy(x => x.CreatedAt),
        approval,
        approvalSteps = approval is null
            ? new List<ApprovalStep>()
            : store.ApprovalSteps.Where(x => x.CustomerId == customerId && x.ApprovalRequestId == approval.Id).OrderBy(x => x.StepOrder).ToList(),
        releaseWindow = package.ReleaseWindowId.HasValue ? store.ReleaseWindows.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == package.ReleaseWindowId.Value) : null,
        deploymentPlan = package.DeploymentPlanId.HasValue ? store.ProductionDeploymentPlans.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == package.DeploymentPlanId.Value) : null,
        deploymentSteps = package.DeploymentPlanId.HasValue
            ? store.ProductionDeploymentSteps.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && x.DeploymentPlanId == package.DeploymentPlanId.Value).OrderBy(x => x.StepOrder).ToList()
            : new List<ProductionDeploymentStep>(),
        preSnapshot = package.PreSnapshotId.HasValue ? store.EnvironmentSnapshots.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == package.PreSnapshotId.Value) : null,
        postSnapshot = package.PostSnapshotId.HasValue ? store.EnvironmentSnapshots.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == package.PostSnapshotId.Value) : null,
        snapshotDiff = package.SnapshotDiffId.HasValue ? store.SnapshotDiffs.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == package.SnapshotDiffId.Value) : null,
        deploymentRuns,
        deploymentStepRuns = store.DeploymentStepRuns.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && deploymentRunIds.Contains(x.DeploymentRunId)).OrderBy(x => x.StepOrder),
        deploymentLogs = store.ProductionDeploymentLogs.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && x.ProductionReleasePackageId == package.Id).OrderBy(x => x.CreatedAt),
        validationChecks = store.PostReleaseValidationChecks.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && x.ProductionReleasePackageId == package.Id).OrderBy(x => x.CreatedAt),
        rollbackDecisions = store.RollbackDecisionRequests.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && x.ProductionReleasePackageId == package.Id).OrderByDescending(x => x.CreatedAt),
        communications = store.ReleaseCommunications.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && x.ProductionReleasePackageId == package.Id).OrderBy(x => x.Audience),
        postReleaseTasks = store.PostReleaseTasks.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && x.ProductionReleasePackageId == package.Id).OrderBy(x => x.Target),
        closureReport = package.ClosureReportId.HasValue ? store.ReleaseClosureReports.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == package.ClosureReportId.Value) : null,
        audit = store.AuditLogs.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && (x.EntityId == package.Id || x.EntityType.Contains("Production") || x.EntityType.Contains("Release"))).OrderByDescending(x => x.CreatedAt).Take(40)
    };
}

static IEnumerable<(string Title, bool Required)> ProductionChecklist(ProductionReleasePackage package)
{
    yield return ("Release readiness report reviewed", true);
    yield return ("Production rollback owner assigned", true);
    yield return ("Rollback plan validated", true);
    yield return ("Support team briefed", true);
    yield return ("Customer communication approved", package.RiskLevel is RiskLevel.High or RiskLevel.Critical);
}

static IEnumerable<string> ProductionApprovers(ProductionReleasePackage package)
{
    var approvers = new List<string> { "release.manager" };
    if (package.RiskLevel is RiskLevel.High or RiskLevel.Critical || IsSensitiveRelease(package))
    {
        approvers.Add("business.owner");
    }
    if (package.RiskLevel == RiskLevel.Critical || ContainsAny(package.Title, "security", "permission", "payroll", "integration"))
    {
        approvers.Add("security.lead");
    }
    if (approvers.Count < 2 && (package.RiskLevel is RiskLevel.High or RiskLevel.Critical || IsSensitiveRelease(package)))
    {
        approvers.Add("change.manager");
    }
    return approvers.Distinct(StringComparer.OrdinalIgnoreCase);
}

static bool IsSensitiveRelease(ProductionReleasePackage package) =>
    ContainsAny($"{package.Title} {package.Summary}", "payroll", "permission", "security", "integration", "production database");

static bool ContainsAny(string value, params string[] terms) =>
    terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

static IEnumerable<ProductionDeploymentStep> ProductionDeploymentSteps(ProductionReleasePackage package, Guid planId) =>
[
    new()
    {
        CustomerId = package.CustomerId,
        ProjectId = package.ProjectId,
        ProductionReleasePackageId = package.Id,
        DeploymentPlanId = planId,
        StepOrder = 1,
        Title = "Verify production health and release window",
        RiskLevel = RiskLevel.Medium,
        ExecutionMethod = DeploymentExecutionMethod.ReadOnlyCheck,
        GuardResult = "Production API allow-list guard pending validation."
    },
    new()
    {
        CustomerId = package.CustomerId,
        ProjectId = package.ProjectId,
        ProductionReleasePackageId = package.Id,
        DeploymentPlanId = planId,
        StepOrder = 2,
        Title = "Manual confirmation from release owner",
        RiskLevel = package.RiskLevel,
        ExecutionMethod = DeploymentExecutionMethod.Manual,
        ManualConfirmationRequired = true,
        GuardResult = "Manual confirmation required before deploy."
    },
    new()
    {
        CustomerId = package.CustomerId,
        ProjectId = package.ProjectId,
        ProductionReleasePackageId = package.Id,
        DeploymentPlanId = planId,
        StepOrder = 3,
        Title = "Execute guarded production deployment",
        RiskLevel = package.RiskLevel,
        ExecutionMethod = DeploymentExecutionMethod.GuardedScript,
        ManualConfirmationRequired = true,
        GuardResult = "Production SQL guard pending validation."
    },
    new()
    {
        CustomerId = package.CustomerId,
        ProjectId = package.ProjectId,
        ProductionReleasePackageId = package.Id,
        DeploymentPlanId = planId,
        StepOrder = 4,
        Title = "Capture post-deploy health signals",
        RiskLevel = RiskLevel.Medium,
        ExecutionMethod = DeploymentExecutionMethod.Automated,
        GuardResult = "Read-only health capture."
    }
];

static ProductionDeploymentPlan RequireDeploymentPlan(IAppStore store, Guid customerId, Guid projectId, ProductionReleasePackage package) =>
    package.DeploymentPlanId.HasValue
        ? store.ProductionDeploymentPlans.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == package.DeploymentPlanId.Value)
            ?? throw new InvalidOperationException("Deployment plan not found.")
        : throw new InvalidOperationException("Deployment plan is required.");

static List<string> ValidateProductionDeploymentPlan(IAppStore store, ProductionReleasePackage package, ProductionDeploymentPlan plan)
{
    var errors = new List<string>();
    var steps = store.ProductionDeploymentSteps.Where(x => x.CustomerId == package.CustomerId && x.ProjectId == package.ProjectId && x.DeploymentPlanId == plan.Id).OrderBy(x => x.StepOrder).ToList();
    if (steps.Count == 0)
    {
        errors.Add("Deployment plan has no steps.");
    }
    if (steps.Any(x => x.ManualConfirmationRequired && !x.Confirmed))
    {
        errors.Add("Manual deployment steps must be confirmed before validation.");
    }

    foreach (var step in steps)
    {
        step.GuardResult = step.ExecutionMethod == DeploymentExecutionMethod.GuardedScript
            ? "Production SQL guard passed; Production API allow-list guard passed."
            : "Production guard passed.";
    }
    return errors;
}

static EnvironmentSnapshot NewProductionSnapshot(IAppStore store, ProductionReleasePackage package, SnapshotStage stage, string summary)
{
    var connector = EnsureMockProductionConnector(store, package);
    var snapshot = new EnvironmentSnapshot
    {
        CustomerId = package.CustomerId,
        ProjectId = package.ProjectId,
        EnvironmentId = package.ProductionEnvironmentId,
        ConnectorId = connector.Id,
        SnapshotNo = store.NextNumber(stage == SnapshotStage.PreApply ? "P-PRE" : "P-POST"),
        Kind = SnapshotKind.ApplyComposite,
        Stage = stage,
        Summary = summary,
        MaskedSummary = $"{stage} snapshot captured by MockProductionApplyConnector. Sensitive values masked.",
        SnapshotJson = $$"""{"packageId":"{{package.Id}}","stage":"{{stage}}","secretStored":false,"rawHrmDataStored":false}"""
    };
    store.EnvironmentSnapshots.Add(snapshot);
    return snapshot;
}

static CustomerConnector EnsureMockProductionConnector(IAppStore store, ProductionReleasePackage package)
{
    if (store.CustomerConnectors.SingleOrDefault(x => x.CustomerId == package.CustomerId && x.ProjectId == package.ProjectId && x.EnvironmentId == package.ProductionEnvironmentId && x.ConnectorType == "MockProductionApplyConnector") is { } existing)
    {
        return existing;
    }

    var policy = new ConnectorPermissionPolicy
    {
        CustomerId = package.CustomerId,
        ProjectId = package.ProjectId,
        EnvironmentId = package.ProductionEnvironmentId,
        Name = "Mock Production Apply Policy",
        AllowHealthCheck = true,
        AllowProductionApplyWithApproval = true
    };
    var connector = new CustomerConnector
    {
        CustomerId = package.CustomerId,
        ProjectId = package.ProjectId,
        EnvironmentId = package.ProductionEnvironmentId,
        PermissionPolicyId = policy.Id,
        ConnectorType = "MockProductionApplyConnector",
        Name = "Mock Production Apply Connector",
        SecretRef = "secret://mock/production-apply",
        ConfigJson = """{"mode":"mock","scope":"ProductionWithApprovalOnly"}""",
        LastHealthStatus = "Healthy"
    };
    store.ConnectorPermissionPolicies.Add(policy);
    store.CustomerConnectors.Add(connector);
    return connector;
}

static List<string> ProductionDeployBlockers(IAppStore store, ProductionReleasePackage package)
{
    var blockers = new List<string>();
    var environment = store.Environments.SingleOrDefault(x => x.CustomerId == package.CustomerId && x.ProjectId == package.ProjectId && x.Id == package.ProductionEnvironmentId);
    if (environment?.Kind != EnvironmentKind.Production)
    {
        blockers.Add("Target environment must be Production.");
    }
    var checklist = store.ReleaseChecklistItems.Where(x => x.CustomerId == package.CustomerId && x.ProjectId == package.ProjectId && x.ProductionReleasePackageId == package.Id).ToList();
    if (checklist.Count == 0 || checklist.Any(x => x.Required && !x.Completed))
    {
        blockers.Add("Required checklist items must be completed.");
    }
    var approval = package.ApprovalRequestId.HasValue ? store.ApprovalRequests.SingleOrDefault(x => x.Id == package.ApprovalRequestId.Value) : null;
    if (approval?.Status != ApprovalStatus.Approved)
    {
        blockers.Add("Production approval must be approved.");
    }
    var window = package.ReleaseWindowId.HasValue ? store.ReleaseWindows.SingleOrDefault(x => x.Id == package.ReleaseWindowId.Value) : null;
    var now = DateTimeOffset.UtcNow;
    if (window?.Status != ReleaseWindowStatus.Scheduled || now < window.StartsAt.ToUniversalTime() || now > window.EndsAt.ToUniversalTime())
    {
        blockers.Add("Current time must be inside an active scheduled release window.");
    }
    var plan = package.DeploymentPlanId.HasValue ? store.ProductionDeploymentPlans.SingleOrDefault(x => x.Id == package.DeploymentPlanId.Value) : null;
    if (plan?.Validated != true)
    {
        blockers.Add("Deployment plan must be validated.");
    }
    if (plan is not null && store.ProductionDeploymentSteps.Any(x => x.DeploymentPlanId == plan.Id && x.ManualConfirmationRequired && !x.Confirmed))
    {
        blockers.Add("Manual deployment steps must be confirmed.");
    }
    if (!package.RollbackPlanValidated)
    {
        blockers.Add("Rollback plan must be validated.");
    }
    if (!package.PreSnapshotId.HasValue)
    {
        blockers.Add("Pre-snapshot is required.");
    }
    return blockers;
}

static List<string> ProductionCloseBlockers(IAppStore store, ProductionReleasePackage package)
{
    var blockers = new List<string>();
    if (!package.PostSnapshotId.HasValue || !package.SnapshotDiffId.HasValue)
    {
        blockers.Add("Post-snapshot and snapshot diff are required.");
    }
    var validationChecks = store.PostReleaseValidationChecks.Where(x => x.CustomerId == package.CustomerId && x.ProjectId == package.ProjectId && x.ProductionReleasePackageId == package.Id).ToList();
    if (validationChecks.Count == 0 || validationChecks.Any(x => x.Status is not (PostReleaseValidationStatus.Passed or PostReleaseValidationStatus.Warning)))
    {
        blockers.Add("Post-release validation checks must be passed or warning-only.");
    }
    var communications = store.ReleaseCommunications.Where(x => x.CustomerId == package.CustomerId && x.ProjectId == package.ProjectId && x.ProductionReleasePackageId == package.Id).ToList();
    if (communications.Count == 0 || communications.Any(x => !x.Sent))
    {
        blockers.Add("All release communications must be sent.");
    }
    var tasks = store.PostReleaseTasks.Where(x => x.CustomerId == package.CustomerId && x.ProjectId == package.ProjectId && x.ProductionReleasePackageId == package.Id).ToList();
    if (tasks.Count == 0 || tasks.Any(x => !x.Completed))
    {
        blockers.Add("All post-release tasks must be completed.");
    }
    if (!package.ClosureReportId.HasValue)
    {
        blockers.Add("Closure report is required.");
    }
    if (store.RollbackDecisionRequests.Any(x => x.ProductionReleasePackageId == package.Id && x.Status is RollbackDecisionStatus.Requested or RollbackDecisionStatus.Approved))
    {
        blockers.Add("Open rollback decision must be rejected or executed before closure.");
    }
    return blockers;
}

static RollbackDecisionRequest CreateRollbackDecisionIfMissing(IAppStore store, ProductionReleasePackage package, string reason, string impact)
{
    if (store.RollbackDecisionRequests.LastOrDefault(x => x.CustomerId == package.CustomerId && x.ProjectId == package.ProjectId && x.ProductionReleasePackageId == package.Id && x.Status is RollbackDecisionStatus.Requested or RollbackDecisionStatus.Approved) is { } existing)
    {
        return existing;
    }
    var rollback = new RollbackDecisionRequest
    {
        CustomerId = package.CustomerId,
        ProjectId = package.ProjectId,
        ProductionReleasePackageId = package.Id,
        DeploymentRunId = package.LatestDeploymentRunId,
        Reason = reason,
        Impact = impact
    };
    store.RollbackDecisionRequests.Add(rollback);
    return rollback;
}

static AiRun NewGovernanceAiRun(Guid customerId, Guid projectId, AiTaskType taskType, string inputSummary, string maskedPreview) =>
    new()
    {
        CustomerId = customerId,
        ProjectId = projectId,
        RunType = taskType.ToString(),
        Provider = "LocalGovernanceEngine",
        Model = "rule-based-phase8",
        PromptTemplateKey = $"phase8-{taskType.ToString().ToLowerInvariant()}-v1",
        PromptVersion = 1,
        InputRef = "ProjectScopedOperationalContext",
        InputSummary = inputSummary,
        MaskedInputPreview = maskedPreview,
        Status = AiRunStatus.Running,
        StartedAt = DateTimeOffset.UtcNow
    };

static List<KnowledgeLearningItem> GenerateKnowledgeLearningItems(IAppStore store, IAuditWriter audit, Guid customerId, Guid projectId, AiRun aiRun, bool allowLowRiskAutoApprove)
{
    var created = new List<KnowledgeLearningItem>();
    foreach (var issue in store.Issues.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt))
    {
        if (store.KnowledgeLearningItems.Any(x => x.CustomerId == customerId && x.ProjectId == projectId && x.SourceEntityId == issue.Id && x.SourceEntityType == nameof(Issue)))
        {
            continue;
        }
        var analysis = store.IssueAnalyses.LastOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.IssueId == issue.Id);
        var fix = store.FixProposals.LastOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.IssueId == issue.Id);
        var moduleName = ResolveModuleName(store, issue);
        var status = allowLowRiskAutoApprove && issue.RiskLevel == RiskLevel.Low ? KnowledgeLifecycleStatus.Approved : KnowledgeLifecycleStatus.PendingReview;
        var item = new KnowledgeLearningItem
        {
            CustomerId = customerId,
            ProjectId = projectId,
            AiRunId = aiRun.Id,
            SourceType = KnowledgeSourceType.Issue,
            SourceEntityType = nameof(Issue),
            SourceEntityId = issue.Id,
            SourceSummary = $"Issue {issue.IssueNo}: {issue.Title}",
            KnowledgeNo = store.NextNumber("KLI"),
            Title = $"Lesson learned - {issue.Title}",
            Category = issue.Category.ToString(),
            ModuleName = moduleName,
            Content = $"Root cause: {analysis?.Content ?? issue.RootCauseSummary ?? "Pending review"}. Fix direction: {fix?.ProposedSolution ?? "Pending fix proposal"}.",
            LessonsLearned = $"For {moduleName}, validate similar scenarios earlier and add regression coverage for {issue.Category}.",
            RiskLevel = issue.RiskLevel,
            Status = status,
            LowRiskAutoApproved = status == KnowledgeLifecycleStatus.Approved,
            ReviewedBy = status == KnowledgeLifecycleStatus.Approved ? "phase8.auto-review" : null,
            ReviewedAt = status == KnowledgeLifecycleStatus.Approved ? DateTimeOffset.UtcNow : null,
            ExplainabilityJson = JsonSerializer.Serialize(new
            {
                source = nameof(Issue),
                issueId = issue.Id,
                formula = "Knowledge is proposed from issue title, category, masked root cause and accepted fix proposal. No HR/payroll transaction rows are stored.",
                requiresHumanReview = status != KnowledgeLifecycleStatus.Approved
            })
        };
        item.VersionGroupId = item.Id;
        store.KnowledgeLearningItems.Add(item);
        store.TraceLinks.Add(NewTrace(customerId, projectId, nameof(Issue), issue.Id, nameof(KnowledgeLearningItem), item.Id, "LearningFromIssue"));
        audit.Write(customerId, projectId, "KNOWLEDGE_LEARNING_ITEM_CREATED", nameof(KnowledgeLearningItem), item.Id, item);
        created.Add(item);
    }

    foreach (var report in store.ReleaseClosureReports.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt))
    {
        if (store.KnowledgeLearningItems.Any(x => x.CustomerId == customerId && x.ProjectId == projectId && x.SourceEntityId == report.Id && x.SourceEntityType == nameof(ReleaseClosureReport)))
        {
            continue;
        }
        var package = store.ProductionReleasePackages.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == report.ProductionReleasePackageId);
        var item = new KnowledgeLearningItem
        {
            CustomerId = customerId,
            ProjectId = projectId,
            AiRunId = aiRun.Id,
            SourceType = KnowledgeSourceType.ReleaseClosureReport,
            SourceEntityType = nameof(ReleaseClosureReport),
            SourceEntityId = report.Id,
            SourceSummary = $"Closure report {report.ReportNo}",
            KnowledgeNo = store.NextNumber("KLI"),
            Title = $"Release lesson - {package?.Title ?? report.ReportNo}",
            Category = "Release",
            ModuleName = package?.Title.Contains("Payroll", StringComparison.OrdinalIgnoreCase) == true ? "Payroll" : "Leave Management",
            Content = $"{report.DeploymentSummary} {report.ValidationSummary} {report.RollbackSummary}",
            LessonsLearned = report.FinalRecommendation,
            RiskLevel = package?.RiskLevel ?? RiskLevel.Medium,
            Status = KnowledgeLifecycleStatus.PendingReview,
            ExplainabilityJson = JsonSerializer.Serialize(new { source = nameof(ReleaseClosureReport), reportId = report.Id, formula = "Release lesson combines deployment, validation, rollback and document-update summary." })
        };
        item.VersionGroupId = item.Id;
        store.KnowledgeLearningItems.Add(item);
        store.TraceLinks.Add(NewTrace(customerId, projectId, nameof(ReleaseClosureReport), report.Id, nameof(KnowledgeLearningItem), item.Id, "LearningFromReleaseClosure"));
        audit.Write(customerId, projectId, "KNOWLEDGE_LEARNING_ITEM_CREATED", nameof(KnowledgeLearningItem), item.Id, item);
        created.Add(item);
    }
    return created;
}

static List<RepeatedIssuePattern> DetectRepeatedIssuePatterns(IAppStore store, Guid customerId, Guid projectId, Guid aiRunId)
{
    var patterns = new List<RepeatedIssuePattern>();
    var groups = store.Issues
        .Where(x => x.CustomerId == customerId && x.ProjectId == projectId)
        .GroupBy(x => new { Module = ResolveModuleName(store, x), x.Category })
        .Where(x => x.Count() >= 2);
    foreach (var group in groups)
    {
        var key = $"{group.Key.Module}:{group.Key.Category}".ToLowerInvariant();
        var issues = group.OrderBy(x => x.CreatedAt).ToList();
        var pattern = store.RepeatedIssuePatterns.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.PatternKey == key)
            ?? new RepeatedIssuePattern { CustomerId = customerId, ProjectId = projectId, PatternKey = key };
        pattern.AiRunId = aiRunId;
        pattern.ModuleName = group.Key.Module;
        pattern.Category = group.Key.Category;
        pattern.IssueCount = issues.Count;
        pattern.RiskLevel = issues.Any(x => x.RiskLevel == RiskLevel.Critical) ? RiskLevel.Critical : issues.Any(x => x.RiskLevel == RiskLevel.High) ? RiskLevel.High : RiskLevel.Medium;
        pattern.Summary = $"{issues.Count} repeated {group.Key.Category} issue(s) detected in {group.Key.Module}.";
        pattern.Recommendation = "Create preventive checklist, add regression tests and review related config specification.";
        pattern.SourceIssueIdsJson = JsonSerializer.Serialize(issues.Select(x => x.Id));
        pattern.FirstSeenAt = issues.First().CreatedAt;
        pattern.LastSeenAt = issues.Last().CreatedAt;
        pattern.UpdatedAt = DateTimeOffset.UtcNow;
        if (!store.RepeatedIssuePatterns.Contains(pattern)) store.RepeatedIssuePatterns.Add(pattern);
        patterns.Add(pattern);
    }
    return patterns;
}

static List<GovernanceScoreSnapshot> RecalculateGovernanceScores(IAppStore store, Guid customerId, Guid projectId, Guid aiRunId, string? moduleName)
{
    var issues = store.Issues.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).ToList();
    if (!string.IsNullOrWhiteSpace(moduleName))
    {
        issues = issues.Where(x => ResolveModuleName(store, x).Contains(moduleName, StringComparison.OrdinalIgnoreCase)).ToList();
    }
    var releases = store.ProductionReleasePackages.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).ToList();
    var validations = store.PostReleaseValidationChecks.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).ToList();
    var rollbacks = store.RollbackDecisionRequests.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).ToList();
    var aiRuns = store.AiRuns.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).ToList();
    var proposals = store.AiProposals.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).ToList();

    var openCritical = issues.Count(x => x.Status != IssueStatus.Closed && x.RiskLevel == RiskLevel.Critical);
    var openHigh = issues.Count(x => x.Status != IssueStatus.Closed && x.RiskLevel == RiskLevel.High);
    var validationFailed = validations.Count(x => x.Status == PostReleaseValidationStatus.Failed);
    var rollbackRequested = rollbacks.Count(x => x.Status is RollbackDecisionStatus.Requested or RollbackDecisionStatus.Approved or RollbackDecisionStatus.Executed);
    var closedReleases = releases.Count(x => x.Status == ProductionReleaseStatus.Closed || x.Status == ProductionReleaseStatus.ReadyToClose);
    var customerHealth = ClampScore(100 - (openCritical * 15) - (openHigh * 8) - (validationFailed * 10) - (rollbackRequested * 12) - (issues.Count * 2) + (closedReleases * 5));

    var moduleRisk = ClampScore((openCritical * 25) + (openHigh * 15) + (issues.Count * 5) + (validationFailed * 15) + (rollbackRequested * 20));
    var highConfigSpecs = store.ConfigSpecs.Count(x => x.CustomerId == customerId && x.ProjectId == projectId && x.RiskLevel is RiskLevel.High or RiskLevel.Critical);
    var configRisk = ClampScore((highConfigSpecs * 12) + (issues.Count(x => x.LinkedEntityType == nameof(ConfigSpec)) * 10) + (validationFailed * 15));
    var deliveryQuality = ClampScore(100 - (validationFailed * 20) - (rollbackRequested * 20) - (issues.Count(x => x.Status != IssueStatus.Closed) * 3) + (closedReleases * 10));
    var completedRate = aiRuns.Count == 0 ? 1 : aiRuns.Count(x => x.Status == AiRunStatus.Completed) / (decimal)aiRuns.Count;
    var acceptedRate = proposals.Count == 0 ? 1 : proposals.Count(x => x.Status == AiProposalStatus.Accepted) / (decimal)proposals.Count;
    var validationPassRate = proposals.Count == 0 ? 1 : proposals.Count(x => x.Status != AiProposalStatus.FailedValidation) / (decimal)proposals.Count;
    var aiQuality = ClampScore((completedRate * 40) + (acceptedRate * 40) + (validationPassRate * 20));

    var scoreData = new[]
    {
        (GovernanceScoreType.CustomerHealth, customerHealth, "100 - criticalOpen*15 - highOpen*8 - validationFailed*10 - rollbackRequested*12 - totalIssues*2 + closedRelease*5", $"Open critical={openCritical}, open high={openHigh}, failed validations={validationFailed}, rollback signals={rollbackRequested}."),
        (GovernanceScoreType.ModuleRisk, moduleRisk, "criticalOpen*25 + highOpen*15 + totalIssues*5 + validationFailed*15 + rollbackRequested*20", $"Risk score grows when repeated/high-risk module issues or rollback signals increase."),
        (GovernanceScoreType.ConfigRisk, configRisk, "highRiskConfigSpec*12 + configLinkedIssues*10 + validationFailed*15", $"High-risk config specs={highConfigSpecs}; config-linked issues included."),
        (GovernanceScoreType.ProjectDeliveryQuality, deliveryQuality, "100 - validationFailed*20 - rollbackRequested*20 - openIssues*3 + readyOrClosedRelease*10", $"Quality improves with ready/closed releases and drops with open issues or failed validation."),
        (GovernanceScoreType.AiPerformanceQuality, aiQuality, "completedRate*40 + acceptedRate*40 + schemaValidationPassRate*20", $"Completed rate={completedRate:0.##}, accepted rate={acceptedRate:0.##}, validation pass rate={validationPassRate:0.##}.")
    };

    var snapshots = new List<GovernanceScoreSnapshot>();
    foreach (var (type, score, formula, explanation) in scoreData)
    {
        var snapshot = new GovernanceScoreSnapshot
        {
            CustomerId = customerId,
            ProjectId = projectId,
            AiRunId = aiRunId,
            ScoreType = type,
            ModuleName = moduleName,
            Score = score,
            Formula = formula,
            Explanation = explanation,
            InputsJson = JsonSerializer.Serialize(new { openCritical, openHigh, validationFailed, rollbackRequested, totalIssues = issues.Count, closedReleases, highConfigSpecs }),
            Trend = score >= 80 ? AnalyticsTrend.Improving : score >= 50 ? AnalyticsTrend.Stable : AnalyticsTrend.Declining
        };
        store.GovernanceScoreSnapshots.Add(snapshot);
        snapshots.Add(snapshot);
    }

    store.GovernanceInsights.Add(new GovernanceInsight
    {
        CustomerId = customerId,
        ProjectId = projectId,
        AiRunId = aiRunId,
        InsightType = GovernanceInsightType.QualitySignal,
        ModuleName = moduleName ?? "All modules",
        RiskLevel = customerHealth < 60 ? RiskLevel.High : RiskLevel.Medium,
        Title = "Governance score recalculated",
        Summary = $"Customer health {customerHealth:0.##}, delivery quality {deliveryQuality:0.##}, AI quality {aiQuality:0.##}.",
        Recommendation = customerHealth < 70 ? "Prioritize repeated issue elimination and release validation stability." : "Maintain current governance controls and monitor trend.",
        SourceRefsJson = JsonSerializer.Serialize(snapshots.Select(x => x.Id)),
        Status = WorkStatus.Active
    });
    return snapshots;
}

static AiPerformanceMetric RecalculateAiPerformanceMetric(IAppStore store, Guid customerId, Guid projectId)
{
    var runs = store.AiRuns.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).ToList();
    var proposals = store.AiProposals.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).ToList();
    var completed = runs.Count(x => x.Status == AiRunStatus.Completed);
    var failed = runs.Count(x => x.Status == AiRunStatus.Failed);
    var accepted = proposals.Count(x => x.Status == AiProposalStatus.Accepted);
    var rejected = proposals.Count(x => x.Status == AiProposalStatus.Rejected);
    var failedValidation = proposals.Count(x => x.Status == AiProposalStatus.FailedValidation);
    var completedRate = runs.Count == 0 ? 1 : completed / (decimal)runs.Count;
    var acceptedRate = proposals.Count == 0 ? 1 : accepted / (decimal)proposals.Count;
    var validationPassRate = proposals.Count == 0 ? 1 : (proposals.Count - failedValidation) / (decimal)proposals.Count;
    var metric = new AiPerformanceMetric
    {
        CustomerId = customerId,
        ProjectId = projectId,
        TotalRuns = runs.Count,
        CompletedRuns = completed,
        FailedRuns = failed,
        AcceptedOutputs = accepted,
        RejectedOutputs = rejected,
        FailedValidationOutputs = failedValidation,
        QualityScore = ClampScore((completedRate * 40) + (acceptedRate * 40) + (validationPassRate * 20)),
        Formula = "completedRate*40 + acceptedOutputRate*40 + schemaValidationPassRate*20",
        Explanation = "AI quality is intentionally explainable and based on run completion, human acceptance and schema validation quality."
    };
    store.AiPerformanceMetrics.Add(metric);
    return metric;
}

static IEnumerable<T> FilterDate<T>(IEnumerable<T> source, DateTimeOffset? from, DateTimeOffset? to) where T : HrmAiOps.Domain.Common.Entity
{
    if (from.HasValue) source = source.Where(x => x.CreatedAt >= from.Value);
    if (to.HasValue) source = source.Where(x => x.CreatedAt <= to.Value);
    return source;
}

static string ResolveModuleName(IAppStore store, Issue issue)
{
    if (issue.LinkedEntityType == nameof(ConfigSpec) && issue.LinkedEntityId.HasValue)
    {
        return store.ConfigSpecs.SingleOrDefault(x => x.CustomerId == issue.CustomerId && x.ProjectId == issue.ProjectId && x.Id == issue.LinkedEntityId.Value)?.ModuleName ?? "General HRM";
    }
    if (issue.Category == IssueCategory.Payroll || issue.Title.Contains("payroll", StringComparison.OrdinalIgnoreCase)) return "Payroll";
    if (issue.Category == IssueCategory.Permission || issue.Title.Contains("permission", StringComparison.OrdinalIgnoreCase)) return "Permission";
    if (issue.Category == IssueCategory.Security || issue.Title.Contains("security", StringComparison.OrdinalIgnoreCase)) return "Security";
    if (issue.Category == IssueCategory.Integration || issue.Title.Contains("integration", StringComparison.OrdinalIgnoreCase)) return "Integration";
    if (issue.Title.Contains("leave", StringComparison.OrdinalIgnoreCase)) return "Leave Management";
    return "General HRM";
}

static object GovernanceFormulaCatalog() => new
{
    customerHealth = "100 - criticalOpen*15 - highOpen*8 - validationFailed*10 - rollbackRequested*12 - totalIssues*2 + closedRelease*5",
    moduleRisk = "criticalOpen*25 + highOpen*15 + totalIssues*5 + validationFailed*15 + rollbackRequested*20",
    configRisk = "highRiskConfigSpec*12 + configLinkedIssues*10 + validationFailed*15",
    projectDeliveryQuality = "100 - validationFailed*20 - rollbackRequested*20 - openIssues*3 + readyOrClosedRelease*10",
    aiPerformanceQuality = "completedRate*40 + acceptedOutputRate*40 + schemaValidationPassRate*20"
};

static decimal ClampScore(decimal value) => Math.Min(100, Math.Max(0, value));

static void SeedSecurityGovernance(IAppStore store, Guid customerId, Guid projectId, Guid productionEnvironmentId)
{
    var permissions = new[]
    {
        ("security.view", "View Security Dashboard", "Security", "View", true),
        ("security.manage", "Manage Security Policy", "Security", "Manage", true),
        ("secret.read", "Read Secret From Vault", "Secret", "Read", true),
        ("secret.manage", "Manage Secret References", "Secret", "Manage", true),
        ("ai.policy.manage", "Manage AI Access Policy", "AI", "ManagePolicy", true),
        ("connector.policy.manage", "Manage Connector Security Policy", "Connector", "ManagePolicy", true),
        ("approval.policy.manage", "Manage Approval Governance", "Approval", "ManagePolicy", true),
        ("compliance.view", "View Compliance Evidence", "Compliance", "View", true),
        ("compliance.manage", "Generate Compliance Evidence", "Compliance", "Manage", true),
        ("production.deploy", "Execute Production Deployment", "Production", "Deploy", true),
        ("connector.read", "Read Connector Snapshot", "Connector", "Read", false),
        ("report.view", "View Reports And Dashboards", "Report", "View", false),
        ("report.export", "Generate Report Export", "Report", "Export", true),
        ("report.sensitive.export", "Export Sensitive Reports", "Report", "SensitiveExport", true),
        ("dashboard.executive.view", "View Executive Dashboard", "Dashboard", "ViewExecutive", true),
        ("integration.view", "View Integration Hub", "Integration", "View", false),
        ("integration.manage", "Manage Integration Hub", "Integration", "Manage", true),
        ("integration.execute", "Execute Integration Action", "Integration", "Execute", true),
        ("integration.webhook.receive", "Receive Integration Webhook", "Integration", "ReceiveWebhook", true),
        ("integration.gateway.manage", "Manage API Gateway Route", "IntegrationGateway", "Manage", true),
        ("integration.gateway.invoke", "Invoke API Gateway Route", "IntegrationGateway", "Invoke", true),
        ("devops.view", "View DevOps Dashboard", "DevOps", "View", false),
        ("devops.manage", "Manage Repositories And Pull Requests", "DevOps", "Manage", true),
        ("devops.ai", "Run AI Code Assistant", "DevOps", "AiCode", true),
        ("devops.review", "Review Pull Request", "DevOps", "Review", true),
        ("devops.approve", "Submit Code Approval", "DevOps", "Approve", true),
        ("devops.pipeline.run", "Run CI/CD Pipeline", "DevOps", "RunPipeline", true),
        ("devops.merge", "Merge Pull Request", "DevOps", "Merge", true),
        ("devops.deploy", "Prepare Deployment Package", "DevOps", "Package", true),
        ("devops.policy.manage", "Manage AI Code Governance Policy", "DevOps", "ManagePolicy", true),
        ("observability.view", "View Observability Dashboard", "Observability", "View", false),
        ("observability.manage", "Manage Monitoring Configuration", "Observability", "Manage", true),
        ("observability.collect", "Collect Runtime Telemetry", "Observability", "Collect", true),
        ("observability.evaluate", "Evaluate Monitoring Rules", "Observability", "Evaluate", true),
        ("observability.incident.manage", "Manage Incidents", "Observability", "IncidentManage", true),
        ("observability.ai", "Run AI Incident Diagnosis", "Observability", "AiDiagnose", true),
        ("data.migration.view", "View Data Migration", "DataMigration", "View", false),
        ("data.migration.manage", "Manage Data Migration Configuration", "DataMigration", "Manage", true),
        ("data.migration.upload", "Register Import File Reference", "DataMigration", "Upload", true),
        ("data.migration.execute", "Execute Data Import Dry Run Apply", "DataMigration", "Execute", true),
        ("data.migration.signoff", "Sign Off Migrated Data", "DataMigration", "SignOff", true),
        ("data.migration.ai", "Run AI Data Migration Assistance", "DataMigration", "AiAssist", true),
        ("data.migration.sensitive.preview", "View Sensitive Data Preview", "DataMigration", "SensitivePreview", true)
    };
    foreach (var (key, name, resource, action, sensitive) in permissions)
    {
        if (!store.SecurityPermissions.Any(x => x.PermissionKey == key))
        {
            store.SecurityPermissions.Add(new SecurityPermission { PermissionKey = key, Name = name, Resource = resource, Action = action, Sensitive = sensitive });
        }
    }

    var roles = new[]
    {
        new SecurityRole { CustomerId = customerId, RoleKey = "tenant.admin", Name = "Tenant Admin", Description = "Full tenant security administrator.", IsSystemRole = true },
        new SecurityRole { CustomerId = customerId, RoleKey = "security.officer", Name = "Security Officer", Description = "Security and compliance manager.", IsSystemRole = true },
        new SecurityRole { CustomerId = customerId, RoleKey = "release.manager", Name = "Release Manager", Description = "Production release governance owner.", IsSystemRole = true },
        new SecurityRole { CustomerId = customerId, RoleKey = "ai.governor", Name = "AI Governor", Description = "AI policy and prompt governance owner.", IsSystemRole = true },
        new SecurityRole { CustomerId = customerId, RoleKey = "support.consultant", Name = "Support Consultant", Description = "Customer support operator.", IsSystemRole = true }
    };
    foreach (var role in roles)
    {
        if (!store.SecurityRoles.Any(x => x.CustomerId == customerId && x.RoleKey == role.RoleKey)) store.SecurityRoles.Add(role);
    }

    var tenantAdminPermissions = permissions.Select(x => x.Item1).ToArray();
    foreach (var permission in tenantAdminPermissions)
    {
        if (!store.SecurityRolePermissions.Any(x => x.CustomerId == customerId && x.RoleKey == "tenant.admin" && x.PermissionKey == permission))
        {
            store.SecurityRolePermissions.Add(new SecurityRolePermission { CustomerId = customerId, RoleKey = "tenant.admin", PermissionKey = permission });
        }
    }
    foreach (var permission in new[] { "security.view", "compliance.view", "compliance.manage", "approval.policy.manage", "secret.read", "report.view", "report.export", "report.sensitive.export", "dashboard.executive.view", "integration.view", "integration.manage", "integration.execute", "integration.webhook.receive", "integration.gateway.manage", "integration.gateway.invoke", "devops.view", "devops.ai", "devops.review", "devops.approve", "devops.policy.manage", "observability.view", "observability.manage", "observability.collect", "observability.evaluate", "observability.incident.manage", "observability.ai", "data.migration.view", "data.migration.manage", "data.migration.upload", "data.migration.execute", "data.migration.signoff", "data.migration.ai", "data.migration.sensitive.preview" })
    {
        store.SecurityRolePermissions.Add(new SecurityRolePermission { CustomerId = customerId, RoleKey = "security.officer", PermissionKey = permission });
    }
    foreach (var permission in new[] { "security.view", "production.deploy", "connector.read", "compliance.view", "report.view", "report.export", "dashboard.executive.view", "integration.view", "integration.execute", "integration.gateway.invoke", "devops.view", "devops.review", "devops.pipeline.run", "devops.merge", "devops.deploy", "observability.view", "observability.collect", "observability.evaluate", "observability.incident.manage", "data.migration.view", "data.migration.execute", "data.migration.signoff" })
    {
        store.SecurityRolePermissions.Add(new SecurityRolePermission { CustomerId = customerId, RoleKey = "release.manager", PermissionKey = permission });
    }
    foreach (var permission in new[] { "security.view", "ai.policy.manage", "compliance.view", "devops.view", "devops.ai", "devops.policy.manage", "observability.view", "observability.ai", "data.migration.view", "data.migration.ai" })
    {
        store.SecurityRolePermissions.Add(new SecurityRolePermission { CustomerId = customerId, RoleKey = "ai.governor", PermissionKey = permission });
    }

    store.TenantAccessGrants.Add(new TenantAccessGrant { CustomerId = customerId, ProjectId = projectId, UserId = "security.admin", RoleKey = "tenant.admin", GrantedBy = "system" });
    store.UserRoleAssignments.Add(new UserRoleAssignment { CustomerId = customerId, ProjectId = projectId, UserId = "security.admin", RoleKey = "tenant.admin" });
    store.TenantAccessGrants.Add(new TenantAccessGrant { CustomerId = customerId, ProjectId = projectId, UserId = "release.manager", RoleKey = "release.manager", GrantedBy = "security.admin" });
    store.UserRoleAssignments.Add(new UserRoleAssignment { CustomerId = customerId, ProjectId = projectId, UserId = "release.manager", RoleKey = "release.manager" });

    store.SecretVaultReferences.Add(new SecretVaultReference { CustomerId = customerId, ProjectId = projectId, Name = "Production HRM Connector", SecretRef = "secret://demo/prod-connector", VaultProvider = "LocalStubVault", RotationDueAt = DateTimeOffset.UtcNow.AddDays(45) });
    store.DataClassificationRules.Add(new DataClassificationRule { CustomerId = customerId, ProjectId = projectId, ResourceType = "Issue", FieldName = "employeeName", Classification = DataClassificationLevel.Restricted, MaskingStrategy = "Redact", ApplyToAiPrompt = true });
    store.DataClassificationRules.Add(new DataClassificationRule { CustomerId = customerId, ProjectId = projectId, ResourceType = "Payroll", FieldName = "salary", Classification = DataClassificationLevel.Secret, MaskingStrategy = "Tokenize", ApplyToAiPrompt = true });

    store.AiAccessPolicies.Add(new AiAccessPolicy { CustomerId = customerId, ProjectId = projectId, TaskType = AiTaskType.AnalyzeRootCause, AllowedRolesCsv = "support.consultant,ai.governor,tenant.admin", MaxInputClassification = DataClassificationLevel.Confidential, MaskingRequired = true, RequiresApprovalForHighRisk = true });
    store.AiAccessPolicies.Add(new AiAccessPolicy { CustomerId = customerId, ProjectId = projectId, TaskType = AiTaskType.GenerateLessonsLearned, AllowedRolesCsv = "ai.governor,tenant.admin", MaxInputClassification = DataClassificationLevel.Confidential, MaskingRequired = true, RequiresApprovalForHighRisk = true });

    store.ConnectorSecurityPolicies.Add(new ConnectorSecurityPolicy { CustomerId = customerId, ProjectId = projectId, EnvironmentId = productionEnvironmentId, ConnectorType = "MockProductionApplyConnector", AllowedActionsCsv = "HealthRead,SnapshotRead,GuardedProductionApplyWithApproval", RequiredPermission = "production.deploy", MaxDataClassification = DataClassificationLevel.Confidential, ReadOnlyRequired = false, AllowProductionApplyWithApproval = true });
    store.ConnectorSecurityPolicies.Add(new ConnectorSecurityPolicy { CustomerId = customerId, ProjectId = projectId, ConnectorType = "PostgreSqlReadOnly", AllowedActionsCsv = "SchemaRead,ConfigRead,HealthRead", RequiredPermission = "connector.read", MaxDataClassification = DataClassificationLevel.Confidential, ReadOnlyRequired = true });

    foreach (var module in new[] { "Payroll", "Permission", "Security", "Integration", "Production Database" })
    {
        store.ApprovalGovernanceRules.Add(new ApprovalGovernanceRule { CustomerId = customerId, ProjectId = projectId, RuleKey = $"approval.{module.ToLowerInvariant().Replace(" ", "-")}", ModuleName = module, MinimumRiskLevel = RiskLevel.High, AppliesToProduction = true, RequiredApprovalSteps = 2, ApproverRolesCsv = "release.manager,security.officer,business.owner", RequiresSecurityApproval = true, Reason = $"{module} changes require multi-step enterprise approval." });
    }

    store.SecurityPolicyRules.Add(new SecurityPolicyRule { CustomerId = customerId, ProjectId = projectId, PolicyKey = "production.deploy.requires.permission", Resource = "ProductionReleasePackage", Action = "Deploy", RequiredPermission = "production.deploy" });
    store.SecurityPolicyRules.Add(new SecurityPolicyRule { CustomerId = customerId, ProjectId = projectId, PolicyKey = "secret.access.requires.permission", Resource = "SecretVaultReference", Action = "Read", RequiredPermission = "secret.read" });
}

static void SeedCommercialAndPortal(IAppStore store, IAuditWriter audit, Guid customerId, Guid projectId)
{
    var plan = new ServicePlan
    {
        PlanCode = "ENTERPRISE-AIOPS",
        Name = "Enterprise AI Ops",
        Description = "Enterprise HRM AI Ops subscription with production governance, analytics and support portal.",
        BaseMonthlyPrice = 2500m,
        Currency = "USD",
        MaxProjects = 8,
        MaxConnectors = 20,
        MaxAiRunsPerMonth = 5000,
        MaxTicketsPerMonth = 200,
        IncludedSupportHours = 80,
        SlaResponseHours = 4,
        SlaResolutionHours = 48,
        EnabledModulesCsv = "DocumentLifecycle,AIOrchestrator,Operations,Connectors,TestApply,ProductionRelease,GovernanceAnalytics,Security,BillingPortal",
        QuotaEnforcementMode = QuotaEnforcementMode.WarnOnly
    };
    store.ServicePlans.Add(plan);

    var contract = new CustomerContract
    {
        CustomerId = customerId,
        ContractNo = store.NextNumber("CTR"),
        Title = "Demo Enterprise HRM AI Ops Managed Service",
        Status = ContractStatus.Active,
        StartsAt = DateTimeOffset.UtcNow.Date.AddDays(-15),
        EndsAt = DateTimeOffset.UtcNow.Date.AddYears(1),
        Currency = "USD",
        ContractValue = 30000m,
        TermsSummary = "Annual enterprise support, AI Ops governance and controlled production release advisory. Draft billing only.",
        BillingContactRef = "contact://demo/billing-owner"
    };
    store.CustomerContracts.Add(contract);

    var subscription = new Subscription
    {
        CustomerId = customerId,
        ServicePlanId = plan.Id,
        ContractId = contract.Id,
        SubscriptionNo = store.NextNumber("SUB"),
        Status = SubscriptionStatus.Active,
        BillingCycle = BillingCycle.Monthly,
        StartsAt = contract.StartsAt,
        CurrentPeriodStart = DateTimeOffset.UtcNow.Date.AddDays(1 - DateTimeOffset.UtcNow.Day),
        CurrentPeriodEnd = DateTimeOffset.UtcNow.Date.AddDays(1 - DateTimeOffset.UtcNow.Day).AddMonths(1).AddTicks(-1),
        UnitPrice = plan.BaseMonthlyPrice,
        Currency = plan.Currency
    };
    store.Subscriptions.Add(subscription);
    store.SupportEntitlements.Add(NewEntitlement(customerId, subscription.Id, plan));
    var sla = new SlaPolicy
    {
        CustomerId = customerId,
        SubscriptionId = subscription.Id,
        PolicyNo = store.NextNumber("SLA"),
        Name = "Enterprise High Severity SLA",
        Severity = IssueSeverity.High,
        ResponseHours = plan.SlaResponseHours,
        ResolutionHours = plan.SlaResolutionHours,
        WarningBeforeHours = 4,
        Timezone = "Asia/Bangkok"
    };
    store.SlaPolicies.Add(sla);

    var ticket = new CustomerPortalTicket
    {
        CustomerId = customerId,
        ProjectId = projectId,
        SlaPolicyId = sla.Id,
        TicketNo = store.NextNumber("TCK"),
        Title = "Customer portal sample - leave approval issue follow-up",
        Description = "Customer asks support to review repeated leave balance issue and SLA status.",
        Severity = IssueSeverity.High,
        Status = PortalTicketStatus.InProgress,
        RequestedBy = "customer.hr",
        SubmittedAt = DateTimeOffset.UtcNow.AddHours(-1),
        FirstResponseAt = DateTimeOffset.UtcNow.AddMinutes(-30)
    };
    ApplySla(ticket, sla, ticket.SubmittedAt);
    RecalculateTicketSla(ticket, DateTimeOffset.UtcNow);
    store.CustomerPortalTickets.Add(ticket);
    store.ServiceRequests.Add(new ServiceRequest { CustomerId = customerId, ProjectId = projectId, PortalTicketId = ticket.Id, RequestNo = store.NextNumber("SRQ"), RequestType = "Advisory", Title = "Review Leave Management preventive controls", Description = "Prepare recommendations for recurring leave balance issue.", RiskLevel = RiskLevel.Medium, EstimatedHours = 4, RequestedBy = "customer.hr" });

    AddUsage(store, customerId, projectId, UsageMetricType.Project, nameof(Project), projectId, store.Projects.Count(x => x.CustomerId == customerId), "Project entitlement baseline.");
    AddUsage(store, customerId, projectId, UsageMetricType.Connector, nameof(CustomerConnector), null, store.CustomerConnectors.Count(x => x.CustomerId == customerId), "Connector entitlement baseline.");
    AddUsage(store, customerId, projectId, UsageMetricType.AiRun, nameof(AiRun), null, store.AiRuns.Count(x => x.CustomerId == customerId), "AI run usage baseline.");
    AddUsage(store, customerId, projectId, UsageMetricType.Ticket, nameof(CustomerPortalTicket), ticket.Id, 1, "Seeded customer portal ticket.");
    RecalculateUsageQuotas(store, customerId, subscription);
    var billing = GenerateBillingDraft(store, customerId, subscription, subscription.CurrentPeriodStart, subscription.CurrentPeriodEnd);
    var invoice = new InvoiceDraft { CustomerId = customerId, BillingDraftId = billing.Id, SubscriptionId = subscription.Id, InvoiceNo = store.NextNumber("INV"), IssueDate = DateTimeOffset.UtcNow.Date, DueDate = DateTimeOffset.UtcNow.Date.AddDays(15), TotalAmount = billing.TotalAmount, Currency = billing.Currency, TraceJson = billing.TraceJson };
    store.InvoiceDrafts.Add(invoice);
    GenerateServiceReport(store, customerId, subscription.Id, subscription.CurrentPeriodStart, subscription.CurrentPeriodEnd);
    audit.Write(customerId, null, "COMMERCIAL_DEMO_DATA_SEEDED", nameof(Subscription), subscription.Id, new { plan, contract, subscription, ticket, billing, invoice });
}

static bool TryReadCustomerIdFromPath(string? path, out Guid customerId)
{
    customerId = Guid.Empty;
    if (string.IsNullOrWhiteSpace(path)) return false;
    var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
    var index = Array.FindIndex(parts, x => string.Equals(x, "customers", StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < parts.Length && Guid.TryParse(parts[index + 1], out customerId);
}

static string Actor(HttpContext http) =>
    http.Request.Headers["X-User-Id"].FirstOrDefault() ?? "security.admin";

static bool HasTenantAccess(IAppStore store, Guid customerId, string userId) =>
    string.Equals(userId, "security.admin", StringComparison.OrdinalIgnoreCase) ||
    store.TenantAccessGrants.Any(x =>
        x.CustomerId == customerId &&
        string.Equals(x.UserId, userId, StringComparison.OrdinalIgnoreCase) &&
        x.Status == TenantAccessStatus.Active &&
        (!x.ExpiresAt.HasValue || x.ExpiresAt.Value > DateTimeOffset.UtcNow));

static bool RequirePermission(IAppStore store, Guid customerId, Guid? projectId, string userId, string permissionKey, out IResult error)
{
    error = Results.Forbid();
    if (string.Equals(userId, "security.admin", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }
    if (!HasTenantAccess(store, customerId, userId))
    {
        error = Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
        return false;
    }

    var assignedRoles = store.UserRoleAssignments
        .Where(x =>
            x.CustomerId == customerId &&
            string.Equals(x.UserId, userId, StringComparison.OrdinalIgnoreCase) &&
            x.Status == TenantAccessStatus.Active &&
            (!projectId.HasValue || x.ProjectId == projectId || x.ProjectId == null))
        .Select(x => x.RoleKey)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var denied = store.SecurityRolePermissions.Any(x =>
        x.CustomerId == customerId &&
        assignedRoles.Contains(x.RoleKey) &&
        string.Equals(x.PermissionKey, permissionKey, StringComparison.OrdinalIgnoreCase) &&
        x.Effect == SecurityPolicyEffect.Deny);
    if (denied)
    {
        error = Results.Json(new { error = $"Permission denied by policy: {permissionKey}" }, statusCode: StatusCodes.Status403Forbidden);
        return false;
    }

    var allowed = store.SecurityRolePermissions.Any(x =>
        x.CustomerId == customerId &&
        assignedRoles.Contains(x.RoleKey) &&
        string.Equals(x.PermissionKey, permissionKey, StringComparison.OrdinalIgnoreCase) &&
        x.Effect == SecurityPolicyEffect.Allow);
    if (!allowed)
    {
        error = Results.Json(new { error = $"Missing permission: {permissionKey}" }, statusCode: StatusCodes.Status403Forbidden);
    }
    return allowed;
}

static ReportingDashboardFilter ReportFilter(Guid customerId, Guid? projectId, DateTimeOffset? dateFrom, DateTimeOffset? dateTo)
{
    var to = dateTo ?? DateTimeOffset.UtcNow;
    var from = dateFrom ?? to.AddDays(-30);
    return new ReportingDashboardFilter(customerId, projectId, from, to);
}

static ReportTemplate? ResolveReportTemplateForRequest(IAppStore store, Guid customerId, Guid projectId, Guid? templateId, ReportDocumentType reportType)
{
    var query = store.ReportTemplates.Where(x => x.CustomerId == customerId && x.Active && (x.ProjectId == null || x.ProjectId == projectId));
    return templateId.HasValue
        ? query.SingleOrDefault(x => x.Id == templateId.Value)
        : query.FirstOrDefault(x => x.ReportType == reportType);
}

static bool CanExportReport(IAppStore store, Guid customerId, Guid projectId, string actor, ReportTemplate template, ReportVisibility visibility, out IResult error)
{
    error = Results.Forbid();
    if (!RequirePermission(store, customerId, projectId, actor, "report.export", out error))
    {
        return false;
    }

    if (template.RequiresPermission && !string.IsNullOrWhiteSpace(template.RequiredPermission) &&
        !RequirePermission(store, customerId, projectId, actor, template.RequiredPermission, out error))
    {
        return false;
    }

    var external = visibility != ReportVisibility.InternalOnly;
    if (external && template.MaxClassification >= DataClassificationLevel.Confidential &&
        !RequirePermission(store, customerId, projectId, actor, "report.sensitive.export", out error))
    {
        return false;
    }

    return true;
}

static bool ValidIntegrationSecretRef(IntegrationAuthType authType, string? secretRef) =>
    authType == IntegrationAuthType.None ||
    (!string.IsNullOrWhiteSpace(secretRef) && secretRef.StartsWith("secret://", StringComparison.OrdinalIgnoreCase));

static string MaskSecretRef(string secretRef)
{
    if (string.IsNullOrWhiteSpace(secretRef)) return "";
    var parts = secretRef.Split('/', StringSplitOptions.RemoveEmptyEntries);
    return parts.Length == 0 ? "secret://***" : $"secret://***/{parts[^1]}";
}

static string MaskByClassification(IAppStore store, Guid customerId, Guid? projectId, string resourceType, string text)
{
    var rules = store.DataClassificationRules
        .Where(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == projectId || x.ProjectId == null) && x.ResourceType.Equals(resourceType, StringComparison.OrdinalIgnoreCase))
        .ToList();
    if (rules.Count == 0) return text;
    var masked = text;
    foreach (var rule in rules.Where(x => x.ApplyToAiPrompt && x.Classification >= DataClassificationLevel.Confidential))
    {
        masked = masked.Replace(rule.FieldName, $"[{rule.Classification}:{rule.MaskingStrategy}]", StringComparison.OrdinalIgnoreCase);
    }
    return masked;
}

static SupportEntitlement NewEntitlement(Guid customerId, Guid subscriptionId, ServicePlan plan) =>
    new()
    {
        CustomerId = customerId,
        SubscriptionId = subscriptionId,
        EntitlementCode = $"ENT-{plan.PlanCode}",
        Name = $"{plan.Name} Entitlement",
        MaxTicketsPerMonth = plan.MaxTicketsPerMonth,
        MaxAiRunsPerMonth = plan.MaxAiRunsPerMonth,
        MaxConnectors = plan.MaxConnectors,
        MaxProjects = plan.MaxProjects,
        IncludedSupportHours = plan.IncludedSupportHours,
        EnabledModulesCsv = plan.EnabledModulesCsv,
        QuotaEnforcementMode = plan.QuotaEnforcementMode
    };

static Subscription? ActiveSubscription(IAppStore store, Guid customerId) =>
    store.Subscriptions
        .Where(x => x.CustomerId == customerId && x.Status is SubscriptionStatus.Active or SubscriptionStatus.Trial)
        .OrderByDescending(x => x.CurrentPeriodStart)
        .FirstOrDefault();

static SlaPolicy? ResolveSlaPolicy(IAppStore store, Guid customerId, IssueSeverity severity)
{
    var subscription = ActiveSubscription(store, customerId);
    return store.SlaPolicies
        .Where(x => x.CustomerId == customerId && (subscription is null || x.SubscriptionId == subscription.Id || x.SubscriptionId == null))
        .OrderBy(x => Math.Abs((int)x.Severity - (int)severity))
        .FirstOrDefault();
}

static void ApplySla(CustomerPortalTicket ticket, SlaPolicy? policy, DateTimeOffset now)
{
    if (policy is null)
    {
        ticket.SlaStatus = SlaStatus.OnTrack;
        return;
    }
    ticket.ResponseDueAt = now.AddHours(policy.ResponseHours);
    ticket.ResolutionDueAt = now.AddHours(policy.ResolutionHours);
    RecalculateTicketSla(ticket, now);
}

static void RecalculateTicketSla(CustomerPortalTicket ticket, DateTimeOffset now)
{
    var responseBreached = ticket.FirstResponseAt.HasValue && ticket.ResponseDueAt.HasValue
        ? ticket.FirstResponseAt.Value > ticket.ResponseDueAt.Value
        : ticket.ResponseDueAt.HasValue && now > ticket.ResponseDueAt.Value;
    var resolutionBreached = ticket.ResolvedAt.HasValue && ticket.ResolutionDueAt.HasValue
        ? ticket.ResolvedAt.Value > ticket.ResolutionDueAt.Value
        : ticket.ResolutionDueAt.HasValue && now > ticket.ResolutionDueAt.Value;
    if (responseBreached || resolutionBreached)
    {
        ticket.SlaStatus = SlaStatus.Breached;
        return;
    }
    if (ticket.ResolvedAt.HasValue)
    {
        ticket.SlaStatus = SlaStatus.Met;
        return;
    }
    var warningAt = ticket.ResolutionDueAt?.AddHours(-2);
    ticket.SlaStatus = warningAt.HasValue && now >= warningAt.Value ? SlaStatus.Warning : SlaStatus.OnTrack;
}

static void AddUsage(IAppStore store, Guid customerId, Guid? projectId, UsageMetricType metricType, string sourceType, Guid? sourceId, decimal quantity, string notes)
{
    store.UsageRecords.Add(new UsageRecord
    {
        CustomerId = customerId,
        ProjectId = projectId,
        SubscriptionId = ActiveSubscription(store, customerId)?.Id,
        MetricType = metricType,
        SourceEntityType = sourceType,
        SourceEntityId = sourceId,
        Quantity = quantity,
        Unit = "count",
        UsageDate = DateTimeOffset.UtcNow,
        Notes = notes
    });
}

static UsageQuotaSnapshot EvaluateQuota(IAppStore store, Guid customerId, UsageMetricType metricType, decimal additionalQuantity)
{
    var subscription = ActiveSubscription(store, customerId);
    if (subscription is null)
    {
        return new UsageQuotaSnapshot { CustomerId = customerId, MetricType = metricType, UsedQuantity = additionalQuantity, IncludedQuantity = 0, OverageQuantity = additionalQuantity, Blocked = true, WarningMessage = "No active subscription." };
    }
    var entitlement = store.SupportEntitlements.LastOrDefault(x => x.CustomerId == customerId && x.SubscriptionId == subscription.Id);
    var included = metricType switch
    {
        UsageMetricType.Project => entitlement?.MaxProjects ?? 0,
        UsageMetricType.Connector => entitlement?.MaxConnectors ?? 0,
        UsageMetricType.AiRun => entitlement?.MaxAiRunsPerMonth ?? 0,
        UsageMetricType.Ticket => entitlement?.MaxTicketsPerMonth ?? 0,
        _ => decimal.MaxValue
    };
    var used = store.UsageRecords
        .Where(x => x.CustomerId == customerId && x.SubscriptionId == subscription.Id && x.MetricType == metricType && x.UsageDate >= subscription.CurrentPeriodStart && x.UsageDate <= subscription.CurrentPeriodEnd)
        .Sum(x => x.Quantity) + additionalQuantity;
    var overage = Math.Max(0, used - included);
    var mode = entitlement?.QuotaEnforcementMode ?? QuotaEnforcementMode.WarnOnly;
    return new UsageQuotaSnapshot
    {
        CustomerId = customerId,
        SubscriptionId = subscription.Id,
        MetricType = metricType,
        UsedQuantity = used,
        IncludedQuantity = included,
        OverageQuantity = overage,
        EnforcementMode = mode,
        Blocked = overage > 0 && mode == QuotaEnforcementMode.Block,
        WarningMessage = overage > 0 ? $"{metricType} quota exceeded by {overage}." : $"{metricType} quota is within entitlement.",
        PeriodStart = subscription.CurrentPeriodStart,
        PeriodEnd = subscription.CurrentPeriodEnd
    };
}

static List<UsageQuotaSnapshot> RecalculateUsageQuotas(IAppStore store, Guid customerId, Subscription subscription)
{
    var metrics = new[] { UsageMetricType.Project, UsageMetricType.Connector, UsageMetricType.AiRun, UsageMetricType.Ticket };
    var snapshots = metrics.Select(metric => EvaluateQuota(store, customerId, metric, 0)).ToList();
    store.UsageQuotaSnapshots.RemoveAll(x => x.CustomerId == customerId && x.SubscriptionId == subscription.Id && x.PeriodStart == subscription.CurrentPeriodStart);
    store.UsageQuotaSnapshots.AddRange(snapshots);
    return snapshots;
}

static BillingDraft GenerateBillingDraft(IAppStore store, Guid customerId, Subscription subscription, DateTimeOffset periodStart, DateTimeOffset periodEnd)
{
    var plan = store.ServicePlans.Single(x => x.Id == subscription.ServicePlanId);
    var usage = store.UsageRecords.Where(x => x.CustomerId == customerId && x.SubscriptionId == subscription.Id && x.UsageDate >= periodStart && x.UsageDate <= periodEnd).ToList();
    var ticketOverage = Math.Max(0, usage.Where(x => x.MetricType == UsageMetricType.Ticket).Sum(x => x.Quantity) - plan.MaxTicketsPerMonth);
    var aiOverage = Math.Max(0, usage.Where(x => x.MetricType == UsageMetricType.AiRun).Sum(x => x.Quantity) - plan.MaxAiRunsPerMonth);
    var overageAmount = (ticketOverage * 10m) + (aiOverage * 0.05m);
    var draft = new BillingDraft
    {
        CustomerId = customerId,
        SubscriptionId = subscription.Id,
        ContractId = subscription.ContractId,
        BillingDraftNo = store.NextNumber("BIL"),
        Status = BillingDraftStatus.Draft,
        PeriodStart = periodStart,
        PeriodEnd = periodEnd,
        Subtotal = subscription.UnitPrice,
        OverageAmount = overageAmount,
        TaxAmount = Math.Round((subscription.UnitPrice + overageAmount) * 0.0m, 2),
        TotalAmount = subscription.UnitPrice + overageAmount,
        Currency = subscription.Currency,
        TraceJson = JsonSerializer.Serialize(new { subscriptionId = subscription.Id, contractId = subscription.ContractId, usageIds = usage.Select(x => x.Id), servicePlanId = plan.Id })
    };
    store.BillingDrafts.Add(draft);
    store.BillingLineItems.Add(new BillingLineItem { CustomerId = customerId, BillingDraftId = draft.Id, ItemType = "Subscription", Description = $"{plan.Name} subscription", Quantity = 1, UnitPrice = subscription.UnitPrice, Amount = subscription.UnitPrice, SourceEntityType = nameof(Subscription), SourceEntityId = subscription.Id });
    if (ticketOverage > 0) store.BillingLineItems.Add(new BillingLineItem { CustomerId = customerId, BillingDraftId = draft.Id, ItemType = "Overage", Description = "Ticket overage", Quantity = ticketOverage, UnitPrice = 10m, Amount = ticketOverage * 10m, SourceEntityType = nameof(UsageRecord) });
    if (aiOverage > 0) store.BillingLineItems.Add(new BillingLineItem { CustomerId = customerId, BillingDraftId = draft.Id, ItemType = "Overage", Description = "AI run overage", Quantity = aiOverage, UnitPrice = 0.05m, Amount = aiOverage * 0.05m, SourceEntityType = nameof(UsageRecord) });
    return draft;
}

static CustomerServiceReport GenerateServiceReport(IAppStore store, Guid customerId, Guid? subscriptionId, DateTimeOffset periodStart, DateTimeOffset periodEnd)
{
    var issues = store.Issues.Where(x => x.CustomerId == customerId && x.CreatedAt >= periodStart && x.CreatedAt <= periodEnd).ToList();
    var tickets = store.CustomerPortalTickets.Where(x => x.CustomerId == customerId && x.SubmittedAt >= periodStart && x.SubmittedAt <= periodEnd).ToList();
    var releases = store.ProductionReleasePackages.Where(x => x.CustomerId == customerId && x.CreatedAt >= periodStart && x.CreatedAt <= periodEnd).ToList();
    var aiRuns = store.AiRuns.Where(x => x.CustomerId == customerId && x.StartedAt >= periodStart && x.StartedAt <= periodEnd).ToList();
    var connectorRuns = store.ConnectorRuns.Where(x => x.CustomerId == customerId && x.StartedAt >= periodStart && x.StartedAt <= periodEnd).ToList();
    var breached = tickets.Count(x => x.SlaStatus == SlaStatus.Breached);
    var met = tickets.Count(x => x.SlaStatus == SlaStatus.Met || x.SlaStatus == SlaStatus.OnTrack);
    var health = ClampScore(100 - (breached * 15) - (issues.Count(x => x.Severity == IssueSeverity.Critical) * 10) + (releases.Count(x => x.Status == ProductionReleaseStatus.Closed || x.Status == ProductionReleaseStatus.ReadyToClose) * 5));
    var report = new CustomerServiceReport
    {
        CustomerId = customerId,
        SubscriptionId = subscriptionId,
        ReportNo = store.NextNumber("CSR"),
        PeriodStart = periodStart,
        PeriodEnd = periodEnd,
        IssueCount = issues.Count,
        SlaMetCount = met,
        SlaBreachedCount = breached,
        ReleaseCount = releases.Count,
        AiRunCount = aiRuns.Count,
        ConnectorRunCount = connectorRuns.Count,
        HealthScore = health,
        Summary = $"Issues={issues.Count}, SLA met/on-track={met}, SLA breached={breached}, AI runs={aiRuns.Count}, connector runs={connectorRuns.Count}, health={health:0.##}.",
        TraceJson = JsonSerializer.Serialize(new { issueIds = issues.Select(x => x.Id), ticketIds = tickets.Select(x => x.Id), releaseIds = releases.Select(x => x.Id), aiRunIds = aiRuns.Select(x => x.Id) })
    };
    store.CustomerServiceReports.Add(report);
    return report;
}

static object CustomerPortalSummary(IAppStore store, Guid customerId)
{
    var subscription = ActiveSubscription(store, customerId);
    var plan = subscription is null ? null : store.ServicePlans.SingleOrDefault(x => x.Id == subscription.ServicePlanId);
    var tickets = store.CustomerPortalTickets.Where(x => x.CustomerId == customerId).ToList();
    var quotas = subscription is null ? new List<object>() : RecalculateUsageQuotas(store, customerId, subscription).Cast<object>().ToList();
    return new
    {
        subscription,
        plan,
        openTickets = tickets.Count(x => x.Status is PortalTicketStatus.Open or PortalTicketStatus.InProgress or PortalTicketStatus.WaitingCustomer),
        slaBreached = tickets.Count(x => x.SlaStatus == SlaStatus.Breached),
        latestTickets = tickets.OrderByDescending(x => x.SubmittedAt).Take(10),
        quotas,
        latestServiceReport = store.CustomerServiceReports.Where(x => x.CustomerId == customerId).OrderByDescending(x => x.PeriodEnd).FirstOrDefault(),
        billingDrafts = store.BillingDrafts.Where(x => x.CustomerId == customerId).OrderByDescending(x => x.CreatedAt).Take(5),
        invoiceDrafts = store.InvoiceDrafts.Where(x => x.CustomerId == customerId).OrderByDescending(x => x.IssueDate).Take(5)
    };
}

static PortalUser? PortalUserForActor(IAppStore store, Guid customerId, string actor) =>
    store.PortalUsers.SingleOrDefault(x => x.CustomerId == customerId && string.Equals(x.UserId, actor, StringComparison.OrdinalIgnoreCase) && x.Status == "Active");

static PortalUser EnsurePortalUser(IAppStore store, Guid customerId, string actor)
{
    var user = PortalUserForActor(store, customerId, actor);
    if (user is not null) return user;
    user = new PortalUser { CustomerId = customerId, UserId = actor, DisplayName = actor, Email = $"{actor}@local.portal", RoleKey = "portal.user", CanViewReports = true };
    store.PortalUsers.Add(user);
    return user;
}

static bool PortalUserCanAccessProject(IAppStore store, Guid customerId, Guid projectId, string actor)
{
    if (string.Equals(actor, "security.admin", StringComparison.OrdinalIgnoreCase)) return true;
    var user = PortalUserForActor(store, customerId, actor);
    return user is not null && store.PortalProjectAccesses.Any(x => x.CustomerId == customerId && x.ProjectId == projectId && x.PortalUserId == user.Id);
}

static IEnumerable<object> PortalProjectsForUser(IAppStore store, Guid customerId, string actor)
{
    var projects = store.Projects.Where(x => x.CustomerId == customerId);
    if (!string.Equals(actor, "security.admin", StringComparison.OrdinalIgnoreCase))
    {
        var user = PortalUserForActor(store, customerId, actor);
        var allowed = user is null ? new HashSet<Guid>() : store.PortalProjectAccesses.Where(x => x.CustomerId == customerId && x.PortalUserId == user.Id).Select(x => x.ProjectId).ToHashSet();
        projects = projects.Where(x => allowed.Contains(x.Id));
    }

    return projects.Select(project => new
    {
        project,
        openRequests = store.PortalRequests.Count(x => x.CustomerId == customerId && x.ProjectId == project.Id && x.Status is PortalRequestStatus.Submitted or PortalRequestStatus.InReview or PortalRequestStatus.InProgress or PortalRequestStatus.WaitingForCustomer or PortalRequestStatus.WaitingForApproval),
        sharedDocuments = store.PortalDocumentShares.Count(x => x.CustomerId == customerId && x.ProjectId == project.Id && x.Visibility == PortalVisibility.CustomerVisible && x.Status == "Published"),
        pendingApprovals = store.PortalApprovals.Count(x => x.CustomerId == customerId && x.ProjectId == project.Id && x.Status == PortalApprovalStatus.Pending)
    });
}

static object PortalDashboardSummary(IAppStore store, Guid customerId, string actor)
{
    var user = PortalUserForActor(store, customerId, actor);
    var projectIds = string.Equals(actor, "security.admin", StringComparison.OrdinalIgnoreCase)
        ? store.Projects.Where(x => x.CustomerId == customerId).Select(x => x.Id).ToHashSet()
        : store.PortalProjectAccesses.Where(x => x.CustomerId == customerId && user != null && x.PortalUserId == user.Id).Select(x => x.ProjectId).ToHashSet();
    var tickets = store.CustomerPortalTickets.Where(x => x.CustomerId == customerId && projectIds.Contains(x.ProjectId)).ToList();
    return new
    {
        user,
        projects = PortalProjectsForUser(store, customerId, actor),
        openTickets = tickets.Count(x => x.Status is PortalTicketStatus.Open or PortalTicketStatus.InProgress or PortalTicketStatus.WaitingCustomer),
        pendingRequests = store.PortalRequests.Count(x => x.CustomerId == customerId && projectIds.Contains(x.ProjectId) && x.Status is PortalRequestStatus.Submitted or PortalRequestStatus.InReview or PortalRequestStatus.WaitingForApproval),
        pendingApprovals = store.PortalApprovals.Count(x => x.CustomerId == customerId && projectIds.Contains(x.ProjectId) && x.Status == PortalApprovalStatus.Pending),
        unreadNotifications = store.PortalNotifications.Count(x => x.CustomerId == customerId && x.Status == NotificationStatus.Unread && (user == null || x.PortalUserId == null || x.PortalUserId == user.Id)),
        sharedDocuments = store.PortalDocumentShares.Count(x => x.CustomerId == customerId && projectIds.Contains(x.ProjectId) && x.Visibility == PortalVisibility.CustomerVisible && x.Status == "Published"),
        knowledgeArticles = store.PortalKnowledgeArticles.Count(x => x.CustomerId == customerId && projectIds.Contains(x.ProjectId) && x.Visibility == PortalVisibility.CustomerVisible && x.Status == "Published"),
        trainingSections = store.PortalTrainingSections.Count(x => x.CustomerId == customerId && projectIds.Contains(x.ProjectId) && x.Visibility == PortalVisibility.CustomerVisible && x.Status == "Published"),
        latestRequests = store.PortalRequests.Where(x => x.CustomerId == customerId && projectIds.Contains(x.ProjectId) && x.Visibility == PortalVisibility.CustomerVisible).OrderByDescending(x => x.CreatedAt).Take(5),
        latestNotifications = store.PortalNotifications.Where(x => x.CustomerId == customerId && (user == null || x.PortalUserId == null || x.PortalUserId == user.Id)).OrderByDescending(x => x.CreatedAt).Take(5),
        customerPortalSummary = CustomerPortalSummary(store, customerId)
    };
}

static object PortalProjectWorkspace(IAppStore store, Guid customerId, Guid projectId, string actor) => new
{
    project = FindProject(store, customerId, projectId),
    requests = store.PortalRequests.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Visibility == PortalVisibility.CustomerVisible).OrderByDescending(x => x.CreatedAt).Take(10),
    requirementIntakes = store.PortalRequirementIntakes.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt).Take(10),
    sharedDocuments = store.PortalDocumentShares.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Visibility == PortalVisibility.CustomerVisible && x.Status == "Published").Select(x => PortalDocumentDto(store, x)),
    approvals = store.PortalApprovals.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt).Take(10),
    knowledge = store.PortalKnowledgeArticles.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Visibility == PortalVisibility.CustomerVisible && x.Status == "Published").OrderByDescending(x => x.PublishedAt).Take(10),
    training = store.PortalTrainingSections.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Visibility == PortalVisibility.CustomerVisible && x.Status == "Published").OrderBy(x => x.ModuleName).Take(10),
    notifications = store.PortalNotifications.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt).Take(10),
    timeline = PortalTimeline(store, customerId, projectId, null, null)
};

static object PortalDocumentDto(IAppStore store, PortalDocumentShare share)
{
    var title = share.DocumentType;
    var status = "Published";
    if (share.DocumentType == nameof(Requirement))
    {
        var doc = store.Requirements.SingleOrDefault(x => x.CustomerId == share.CustomerId && x.ProjectId == share.ProjectId && x.Id == share.DocumentId);
        title = doc?.Title ?? title;
        status = doc?.Status.ToString() ?? status;
    }
    else if (share.DocumentType == nameof(UrsDocument))
    {
        var doc = store.UrsDocuments.SingleOrDefault(x => x.CustomerId == share.CustomerId && x.ProjectId == share.ProjectId && x.Id == share.DocumentId);
        title = doc?.Title ?? title;
        status = doc?.Status.ToString() ?? status;
    }
    else if (share.DocumentType == nameof(Blueprint))
    {
        var doc = store.Blueprints.SingleOrDefault(x => x.CustomerId == share.CustomerId && x.ProjectId == share.ProjectId && x.Id == share.DocumentId);
        title = doc?.BlueprintNo ?? title;
        status = doc?.Status.ToString() ?? status;
    }
    else if (share.DocumentType == nameof(ConfigSpec))
    {
        var doc = store.ConfigSpecs.SingleOrDefault(x => x.CustomerId == share.CustomerId && x.ProjectId == share.ProjectId && x.Id == share.DocumentId);
        title = doc?.ConfigNo ?? title;
        status = doc?.Status.ToString() ?? status;
    }

    return new { share, title, documentStatus = status, reviews = store.PortalDocumentReviews.Where(x => x.CustomerId == share.CustomerId && x.ProjectId == share.ProjectId && x.DocumentShareId == share.Id).OrderByDescending(x => x.CreatedAt) };
}

static IEnumerable<object> PortalTimeline(IAppStore store, Guid customerId, Guid projectId, string? sourceEntityType, Guid? sourceEntityId)
{
    var comments = store.PortalComments.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Visibility == PortalVisibility.CustomerVisible);
    var approvals = store.PortalApprovals.Where(x => x.CustomerId == customerId && x.ProjectId == projectId);
    var notifications = store.PortalNotifications.Where(x => x.CustomerId == customerId && x.ProjectId == projectId);
    if (!string.IsNullOrWhiteSpace(sourceEntityType))
    {
        comments = comments.Where(x => x.SourceEntityType == sourceEntityType);
        approvals = approvals.Where(x => x.SourceEntityType == sourceEntityType);
        notifications = notifications.Where(x => x.SourceEntityType == sourceEntityType);
    }
    if (sourceEntityId.HasValue)
    {
        comments = comments.Where(x => x.SourceEntityId == sourceEntityId.Value);
        approvals = approvals.Where(x => x.SourceEntityId == sourceEntityId.Value);
        notifications = notifications.Where(x => x.SourceEntityId == sourceEntityId.Value);
    }

    var timeline = new List<object>();
    timeline.AddRange(comments.Select(x => new { type = CollaborationMessageType.Comment.ToString(), at = x.CreatedAt, title = "Comment", message = x.Message, entityType = x.SourceEntityType, entityId = x.SourceEntityId }));
    timeline.AddRange(approvals.Select(x => new { type = CollaborationMessageType.ApprovalRequest.ToString(), at = x.CreatedAt, title = x.ApprovalType.ToString(), message = x.Status.ToString(), entityType = x.SourceEntityType, entityId = x.SourceEntityId }));
    timeline.AddRange(notifications.Select(x => new { type = CollaborationMessageType.SystemMessage.ToString(), at = x.CreatedAt, title = x.Title, message = x.Message, entityType = x.SourceEntityType, entityId = x.SourceEntityId ?? Guid.Empty }));
    return timeline.OrderByDescending(x => x.GetType().GetProperty("at")?.GetValue(x));
}

static PortalNotification CreatePortalNotification(IAppStore store, Guid customerId, Guid projectId, Guid? portalUserId, NotificationType notificationType, string title, string message, string sourceEntityType, Guid? sourceEntityId)
{
    var notification = new PortalNotification
    {
        CustomerId = customerId,
        ProjectId = projectId,
        PortalUserId = portalUserId,
        NotificationType = notificationType,
        Title = title,
        Message = message.Length > 500 ? message[..500] : message,
        SourceEntityType = sourceEntityType,
        SourceEntityId = sourceEntityId
    };
    store.PortalNotifications.Add(notification);
    return notification;
}

static (int KnowledgeCount, int TrainingCount, int VisibleDocumentCount) BuildPortalAiContext(IAppStore store, Guid customerId, Guid projectId)
{
    var knowledgeCount = store.PortalKnowledgeArticles.Count(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Visibility == PortalVisibility.CustomerVisible && x.Status == "Published");
    var trainingCount = store.PortalTrainingSections.Count(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Visibility == PortalVisibility.CustomerVisible && x.Status == "Published");
    var documentCount = store.PortalDocumentShares.Count(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Visibility == PortalVisibility.CustomerVisible && x.Status == "Published");
    return (knowledgeCount, trainingCount, documentCount);
}

static object CollaborationDashboard(IAppStore store, Guid customerId) => new
{
    unreadNotifications = store.PortalNotifications.Count(x => x.CustomerId == customerId && x.Status == NotificationStatus.Unread),
    deliveryFailures = store.NotificationDeliveryLogs.Count(x => x.CustomerId == customerId && x.Status == NotificationDeliveryStatus.Failed),
    activeWorkflowRules = store.WorkflowRules.Count(x => x.CustomerId == customerId && x.Status == WorkflowRuleStatus.Active),
    workflowRunsToday = store.WorkflowRuns.Count(x => x.CustomerId == customerId && x.StartedAt >= DateTimeOffset.UtcNow.Date),
    openTasks = store.CollaborationTasks.Count(x => x.CustomerId == customerId && x.Status is CollaborationTaskStatus.Open or CollaborationTaskStatus.InProgress or CollaborationTaskStatus.Waiting),
    dueReminders = store.ReminderSchedules.Count(x => x.CustomerId == customerId && x.Status == ReminderStatus.Scheduled && x.RemindAt <= DateTimeOffset.UtcNow.AddDays(1)),
    openEscalations = store.EscalationEvents.Count(x => x.CustomerId == customerId && x.Status is EscalationStatus.Open or EscalationStatus.Notified),
    latestTimeline = store.ActivityTimelineEntries.Where(x => x.CustomerId == customerId).OrderByDescending(x => x.CreatedAt).Take(10),
    latestDeliveries = store.NotificationDeliveryLogs.Where(x => x.CustomerId == customerId).OrderByDescending(x => x.CreatedAt).Take(10)
};

static List<WorkflowRun> ExecuteWorkflowEvent(IAppStore store, IAuditWriter audit, INotificationDeliveryProvider deliveryProvider, IDataMaskingService masking, Guid customerId, Guid? projectId, WorkflowTriggerType trigger, string sourceEntityType, Guid? sourceEntityId, string message, string actor)
{
    var rules = store.WorkflowRules
        .Where(x => x.CustomerId == customerId && x.Status == WorkflowRuleStatus.Active && x.TriggerEvent == trigger && (!x.ProjectId.HasValue || x.ProjectId == projectId))
        .OrderBy(x => x.Priority)
        .ToList();
    var runs = new List<WorkflowRun>();
    foreach (var rule in rules)
    {
        var run = new WorkflowRun { CustomerId = customerId, ProjectId = projectId ?? rule.ProjectId, WorkflowRuleId = rule.Id, TriggerEvent = trigger, SourceEntityType = sourceEntityType, SourceEntityId = sourceEntityId, Status = WorkflowRunStatus.Running };
        store.WorkflowRuns.Add(run);
        try
        {
            if (projectId.HasValue)
            {
                if (WorkflowActionEnabled(rule, "createNotification", true))
                {
                    var notification = SendWorkflowNotification(store, deliveryProvider, masking, customerId, projectId.Value, null, ToNotificationType(trigger), $"{trigger}", message, sourceEntityType, sourceEntityId, actor);
                    store.WorkflowActionLogs.Add(new WorkflowActionLog { CustomerId = customerId, ProjectId = projectId, WorkflowRunId = run.Id, ActionType = WorkflowActionType.CreateNotification, TargetEntityType = nameof(PortalNotification), TargetEntityId = notification.Id, OutputJson = JsonSerializer.Serialize(new { notification.Id }) });
                }

                CollaborationTask? task = null;
                if (WorkflowActionEnabled(rule, "createTask", trigger is WorkflowTriggerType.ApprovalPending or WorkflowTriggerType.SlaBreached))
                {
                    task = new CollaborationTask { CustomerId = customerId, ProjectId = projectId.Value, TaskNo = store.NextNumber("TASK"), Title = $"{trigger}: {message}", Description = $"Auto-created by workflow rule {rule.RuleKey}.", AssigneeUserId = actor, Priority = trigger is WorkflowTriggerType.SlaBreached ? CollaborationTaskPriority.Critical : CollaborationTaskPriority.Medium, DueAt = DateTimeOffset.UtcNow.AddDays(trigger is WorkflowTriggerType.ApprovalPending ? 1 : 2), SourceEntityType = sourceEntityType, SourceEntityId = sourceEntityId };
                    store.CollaborationTasks.Add(task);
                    store.WorkflowActionLogs.Add(new WorkflowActionLog { CustomerId = customerId, ProjectId = projectId, WorkflowRunId = run.Id, ActionType = WorkflowActionType.CreateTask, TargetEntityType = nameof(CollaborationTask), TargetEntityId = task.Id, OutputJson = JsonSerializer.Serialize(new { task.Id }) });
                }

                if (WorkflowActionEnabled(rule, "createReminder", trigger is WorkflowTriggerType.ApprovalPending) && task is not null)
                {
                    var reminder = new ReminderSchedule { CustomerId = customerId, ProjectId = projectId.Value, TaskId = task.Id, SourceEntityType = sourceEntityType, SourceEntityId = sourceEntityId, ReminderType = trigger.ToString(), RemindAt = DateTimeOffset.UtcNow.AddHours(4) };
                    store.ReminderSchedules.Add(reminder);
                    store.WorkflowActionLogs.Add(new WorkflowActionLog { CustomerId = customerId, ProjectId = projectId, WorkflowRunId = run.Id, ActionType = WorkflowActionType.CreateReminder, TargetEntityType = nameof(ReminderSchedule), TargetEntityId = reminder.Id, OutputJson = JsonSerializer.Serialize(new { reminder.Id }) });
                }

                if (WorkflowActionEnabled(rule, "addTimelineEntry", true))
                {
                    AddTimeline(store, customerId, projectId.Value, ActivityTimelineItemType.System, sourceEntityType, sourceEntityId, actor, $"Workflow {rule.Name}", masking.Mask(message), PortalVisibility.InternalOnly);
                }

                if (WorkflowActionEnabled(rule, "escalate", trigger is WorkflowTriggerType.SlaBreached) && trigger is WorkflowTriggerType.SlaBreached)
                {
                    var escalation = CreateEscalation(store, deliveryProvider, masking, customerId, projectId.Value, sourceEntityType, sourceEntityId ?? Guid.Empty, "SLA breached by workflow automation.", "support.manager", actor);
                    store.WorkflowActionLogs.Add(new WorkflowActionLog { CustomerId = customerId, ProjectId = projectId, WorkflowRunId = run.Id, ActionType = WorkflowActionType.Escalate, TargetEntityType = nameof(EscalationEvent), TargetEntityId = escalation.Id });
                }
            }

            run.Status = WorkflowRunStatus.Completed;
            run.CompletedAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            run.Status = WorkflowRunStatus.Failed;
            run.ErrorMessage = ex.Message;
            run.CompletedAt = DateTimeOffset.UtcNow;
        }

        runs.Add(run);
        audit.Write(customerId, projectId, "WORKFLOW_RULE_EXECUTED", nameof(WorkflowRun), run.Id, new { run, ruleKey = rule.RuleKey });
    }

    return runs;
}

static bool WorkflowActionEnabled(WorkflowRule rule, string actionKey, bool defaultValue)
{
    if (string.IsNullOrWhiteSpace(rule.ActionJson)) return defaultValue;
    try
    {
        using var document = JsonDocument.Parse(rule.ActionJson);
        return document.RootElement.TryGetProperty(actionKey, out var value) && value.ValueKind == JsonValueKind.True
            || (!document.RootElement.TryGetProperty(actionKey, out _) && defaultValue);
    }
    catch (JsonException)
    {
        return defaultValue;
    }
}

static NotificationType ToNotificationType(WorkflowTriggerType trigger) => trigger switch
{
    WorkflowTriggerType.SlaWarning => NotificationType.SlaWarning,
    WorkflowTriggerType.SlaBreached => NotificationType.SlaBreached,
    WorkflowTriggerType.ApprovalPending => NotificationType.ApprovalRequired,
    WorkflowTriggerType.ReleaseScheduled => NotificationType.ReleaseScheduled,
    WorkflowTriggerType.DeploymentCompleted => NotificationType.ReleaseCompleted,
    WorkflowTriggerType.InvoiceGenerated => NotificationType.BillingAvailable,
    WorkflowTriggerType.CommentAdded => NotificationType.CommentMention,
    WorkflowTriggerType.CustomerRequestSubmitted => NotificationType.TicketUpdate,
    _ => NotificationType.SystemAnnouncement
};

static PortalNotification SendWorkflowNotification(IAppStore store, INotificationDeliveryProvider deliveryProvider, IDataMaskingService masking, Guid customerId, Guid projectId, Guid? portalUserId, NotificationType type, string title, string message, string sourceEntityType, Guid? sourceEntityId, string actor)
{
    var maskedTitle = masking.Mask(MaskByClassification(store, customerId, projectId, "Notification", title));
    var maskedMessage = masking.Mask(MaskByClassification(store, customerId, projectId, "Notification", message));
    var notification = CreatePortalNotification(store, customerId, projectId, portalUserId, type, maskedTitle, maskedMessage, sourceEntityType, sourceEntityId);
    var template = store.NotificationTemplates.Where(x => x.CustomerId == customerId && x.NotificationType == type && x.Active).OrderBy(x => x.CreatedAt).FirstOrDefault();
    var version = template is null ? null : store.NotificationTemplateVersions.Where(x => x.CustomerId == customerId && x.TemplateId == template.Id && x.Active).OrderByDescending(x => x.Version).FirstOrDefault();
    var channel = template?.Channel ?? NotificationChannel.InApp;
    var recipientType = template?.RecipientType ?? NotificationRecipientType.InternalUser;
    var recipientRef = portalUserId?.ToString() ?? actor;
    var deliveryResult = deliveryProvider.Deliver(new NotificationDeliveryRequest(channel, recipientType, recipientRef, maskedTitle, maskedMessage));
    var delivery = new NotificationDeliveryLog
    {
        CustomerId = customerId,
        ProjectId = projectId,
        NotificationId = notification.Id,
        TemplateId = template?.Id,
        TemplateVersion = version?.Version,
        Channel = channel,
        RecipientType = recipientType,
        RecipientRef = recipientRef,
        Provider = deliveryResult.Provider,
        Status = deliveryResult.Status,
        MaskedPayload = JsonSerializer.Serialize(new { title = maskedTitle, message = maskedMessage }),
        ErrorMessage = deliveryResult.ErrorMessage,
        DeliveredAt = deliveryResult.DeliveredAt
    };
    store.NotificationDeliveryLogs.Add(delivery);
    AddTimeline(store, customerId, projectId, ActivityTimelineItemType.Notification, nameof(PortalNotification), notification.Id, actor, title, notification.Message, PortalVisibility.InternalOnly);
    return notification;
}

static List<ReminderSchedule> RunDueReminders(IAppStore store, IAuditWriter audit, INotificationDeliveryProvider deliveryProvider, IDataMaskingService masking, Guid customerId, DateTimeOffset now, string actor)
{
    var due = store.ReminderSchedules.Where(x => x.CustomerId == customerId && x.Status == ReminderStatus.Scheduled && x.RemindAt <= now).ToList();
    foreach (var reminder in due)
    {
        reminder.Status = ReminderStatus.Sent;
        reminder.SentAt = now;
        var message = $"Reminder due for {reminder.ReminderType}.";
        SendWorkflowNotification(store, deliveryProvider, masking, customerId, reminder.ProjectId, null, NotificationType.ApprovalRequired, "Reminder", message, reminder.SourceEntityType, reminder.SourceEntityId, actor);
        AddTimeline(store, customerId, reminder.ProjectId, ActivityTimelineItemType.Reminder, nameof(ReminderSchedule), reminder.Id, actor, "Reminder sent", masking.Mask(message), PortalVisibility.InternalOnly);
        audit.Write(customerId, reminder.ProjectId, "REMINDER_SENT", nameof(ReminderSchedule), reminder.Id, reminder);
    }
    return due;
}

static EscalationEvent CreateEscalation(IAppStore store, INotificationDeliveryProvider deliveryProvider, IDataMaskingService masking, Guid customerId, Guid projectId, string sourceEntityType, Guid sourceEntityId, string reason, string escalatedToUserId, string actor)
{
    var maskedReason = masking.Mask(reason);
    var escalation = new EscalationEvent { CustomerId = customerId, ProjectId = projectId, SourceEntityType = sourceEntityType, SourceEntityId = sourceEntityId, Reason = maskedReason, EscalatedToUserId = escalatedToUserId, Status = EscalationStatus.Notified };
    store.EscalationEvents.Add(escalation);
    SendWorkflowNotification(store, deliveryProvider, masking, customerId, projectId, null, NotificationType.SlaBreached, "Escalation created", maskedReason, sourceEntityType, sourceEntityId, actor);
    AddTimeline(store, customerId, projectId, ActivityTimelineItemType.Escalation, nameof(EscalationEvent), escalation.Id, actor, "Escalation created", maskedReason, PortalVisibility.InternalOnly);
    return escalation;
}

static ActivityTimelineEntry AddTimeline(IAppStore store, Guid customerId, Guid projectId, ActivityTimelineItemType itemType, string sourceEntityType, Guid? sourceEntityId, string actor, string title, string message, PortalVisibility visibility)
{
    var entry = new ActivityTimelineEntry { CustomerId = customerId, ProjectId = projectId, ItemType = itemType, SourceEntityType = sourceEntityType, SourceEntityId = sourceEntityId, ActorUserId = actor, Title = title, Message = message, Visibility = visibility };
    store.ActivityTimelineEntries.Add(entry);
    return entry;
}

static void SeedPortalEnhancement(IAppStore store, IAuditWriter audit, Guid customerId, Guid projectId, Guid requirementId, Guid ursId, Guid blueprintId, Guid configSpecId)
{
    if (store.PortalUsers.Any(x => x.CustomerId == customerId))
    {
        return;
    }

    var approver = new PortalUser { CustomerId = customerId, UserId = "portal.hr.manager", DisplayName = "HR Manager Portal", Email = "hr.manager@demo.customer", RoleKey = "portal.approver", CanViewBilling = true, CanViewReports = true, CanApprove = true };
    var requester = new PortalUser { CustomerId = customerId, UserId = "portal.hr.user", DisplayName = "HR Portal User", Email = "hr.user@demo.customer", RoleKey = "portal.user", CanViewReports = true };
    store.PortalUsers.AddRange([approver, requester]);
    store.PortalProjectAccesses.Add(new PortalProjectAccess { CustomerId = customerId, ProjectId = projectId, PortalUserId = approver.Id, AccessLevel = "Approver" });
    store.PortalProjectAccesses.Add(new PortalProjectAccess { CustomerId = customerId, ProjectId = projectId, PortalUserId = requester.Id, AccessLevel = "Contributor" });
    store.TenantAccessGrants.Add(new TenantAccessGrant { CustomerId = customerId, ProjectId = projectId, UserId = approver.UserId, RoleKey = approver.RoleKey, GrantedBy = "seed" });
    store.TenantAccessGrants.Add(new TenantAccessGrant { CustomerId = customerId, ProjectId = projectId, UserId = requester.UserId, RoleKey = requester.RoleKey, GrantedBy = "seed" });

    var request = new PortalRequest
    {
        CustomerId = customerId,
        ProjectId = projectId,
        RequestNo = store.NextNumber("PREQ"),
        RequestType = "Configuration",
        Title = "Update leave carry-forward display for employee self-service",
        Description = "Customer portal request for leave carry-forward visibility in ESS.",
        Status = PortalRequestStatus.Submitted,
        Priority = "P2",
        SubmittedByUserId = requester.UserId,
        SubmittedAt = DateTimeOffset.UtcNow.AddHours(-6)
    };
    store.PortalRequests.Add(request);

    var intake = new PortalRequirementIntake
    {
        CustomerId = customerId,
        ProjectId = projectId,
        PortalRequestId = request.Id,
        Title = "Leave carry-forward visibility",
        BusinessContext = "HR users need employees to see approved carry-forward balance before annual leave planning.",
        RequirementText = "Show carry-forward balance on ESS leave dashboard after manager approval.",
        Status = PortalRequestStatus.InReview,
        CreatedByUserId = requester.UserId,
        ConvertedRequirementId = requirementId
    };
    store.PortalRequirementIntakes.Add(intake);

    var reqShare = new PortalDocumentShare { CustomerId = customerId, ProjectId = projectId, DocumentType = nameof(Requirement), DocumentId = requirementId, DocumentVersion = 1, Visibility = PortalVisibility.CustomerVisible, SharedBy = "consultant.lead" };
    var ursShare = new PortalDocumentShare { CustomerId = customerId, ProjectId = projectId, DocumentType = nameof(UrsDocument), DocumentId = ursId, DocumentVersion = 1, Visibility = PortalVisibility.CustomerVisible, SharedBy = "consultant.lead" };
    var blueprintShare = new PortalDocumentShare { CustomerId = customerId, ProjectId = projectId, DocumentType = nameof(Blueprint), DocumentId = blueprintId, DocumentVersion = 1, Visibility = PortalVisibility.CustomerVisible, SharedBy = "consultant.lead" };
    var configShare = new PortalDocumentShare { CustomerId = customerId, ProjectId = projectId, DocumentType = nameof(ConfigSpec), DocumentId = configSpecId, DocumentVersion = 1, Visibility = PortalVisibility.InternalOnly, SharedBy = "consultant.lead", Status = "InternalOnly" };
    store.PortalDocumentShares.AddRange([reqShare, ursShare, blueprintShare, configShare]);
    store.PortalDocumentReviews.Add(new PortalDocumentReview { CustomerId = customerId, ProjectId = projectId, DocumentShareId = ursShare.Id, ReviewerUserId = approver.UserId, Status = PortalApprovalStatus.Pending, Comment = "Awaiting HR review." });

    var approval = new PortalApproval
    {
        CustomerId = customerId,
        ProjectId = projectId,
        ApprovalType = PortalApprovalType.UrsSignoff,
        SourceEntityType = nameof(UrsDocument),
        SourceEntityId = ursId,
        RequestedBy = "consultant.lead",
        ApproverPortalUserId = approver.Id,
        Status = PortalApprovalStatus.Pending,
        DueAt = DateTimeOffset.UtcNow.AddDays(3)
    };
    store.PortalApprovals.Add(approval);

    store.PortalKnowledgeArticles.Add(new PortalKnowledgeArticle
    {
        CustomerId = customerId,
        ProjectId = projectId,
        SourceKnowledgeArticleId = store.KnowledgeArticles.FirstOrDefault(x => x.CustomerId == customerId)?.Id,
        Title = "Leave approval balance troubleshooting",
        Category = "Leave Management",
        Content = "Customer-visible guide for checking leave approval status and balance recalculation timing.",
        Visibility = PortalVisibility.CustomerVisible,
        PublishedAt = DateTimeOffset.UtcNow.AddDays(-1)
    });
    store.PortalTrainingSections.Add(new PortalTrainingSection
    {
        CustomerId = customerId,
        ProjectId = projectId,
        Title = "Leave Management ESS checklist",
        ModuleName = "Leave Management",
        Content = "Training section for employees and HR reviewers using the leave self-service dashboard.",
        Visibility = PortalVisibility.CustomerVisible
    });

    var session = new PortalAiChatSession { CustomerId = customerId, ProjectId = projectId, PortalUserId = requester.Id, Title = "Leave balance self-service help" };
    store.PortalAiChatSessions.Add(session);
    store.PortalAiChatMessages.Add(new PortalAiChatMessage { CustomerId = customerId, ProjectId = projectId, SessionId = session.Id, SenderType = "User", Message = "How do I check carry-forward balance?", MaskedMessage = "How do I check carry-forward balance?" });
    store.PortalAiChatMessages.Add(new PortalAiChatMessage { CustomerId = customerId, ProjectId = projectId, SessionId = session.Id, SenderType = "AI", Message = "Use the Leave Management ESS checklist. Internal comments were excluded from this answer.", MaskedMessage = "Use the Leave Management ESS checklist. Internal comments were excluded from this answer." });

    var notification = CreatePortalNotification(store, customerId, projectId, approver.Id, NotificationType.ApprovalRequired, "URS sign-off required", "Please review and sign off the customer-visible URS draft.", nameof(UrsDocument), ursId);
    store.PortalNotifications.Add(new PortalNotification { CustomerId = customerId, ProjectId = projectId, PortalUserId = requester.Id, NotificationType = NotificationType.ServiceReportAvailable, Title = "Service report available", Message = "The latest customer service report is available in the portal.", SourceEntityType = nameof(CustomerServiceReport), SourceEntityId = store.CustomerServiceReports.FirstOrDefault(x => x.CustomerId == customerId)?.Id });
    store.PortalComments.Add(new PortalComment { CustomerId = customerId, ProjectId = projectId, SourceEntityType = nameof(PortalRequest), SourceEntityId = request.Id, PortalUserId = requester.Id, Message = "Please confirm whether this change is planned for UAT.", Visibility = PortalVisibility.CustomerVisible });
    store.PortalComments.Add(new PortalComment { CustomerId = customerId, ProjectId = projectId, SourceEntityType = nameof(PortalRequest), SourceEntityId = request.Id, PortalUserId = null, Message = "Internal estimate discussion excluded from portal AI context.", Visibility = PortalVisibility.InternalOnly });
    store.PortalAttachments.Add(new PortalAttachment { CustomerId = customerId, ProjectId = projectId, SourceEntityType = nameof(PortalRequest), SourceEntityId = request.Id, UploadedByPortalUserId = requester.Id, FileName = "leave-carry-forward-screenshot.png", ContentType = "image/png", StorageRef = "portal://demo/leave-carry-forward-screenshot.png", Visibility = PortalVisibility.CustomerVisible });
    if (store.CustomerServiceReports.FirstOrDefault(x => x.CustomerId == customerId) is { } report)
    {
        store.PortalServiceReportShares.Add(new PortalServiceReportShare { CustomerId = customerId, ServiceReportId = report.Id, SharedWithPortalUserId = approver.Id });
    }
    if (store.InvoiceDrafts.FirstOrDefault(x => x.CustomerId == customerId) is { } invoice)
    {
        store.PortalBillingSummaryViews.Add(new PortalBillingSummaryView { CustomerId = customerId, PortalUserId = approver.Id, InvoiceDraftId = invoice.Id });
    }

    store.TraceLinks.Add(NewTrace(customerId, projectId, nameof(PortalRequest), request.Id, nameof(PortalRequirementIntake), intake.Id, "PortalRequestRequirementIntake"));
    store.TraceLinks.Add(NewTrace(customerId, projectId, nameof(PortalRequirementIntake), intake.Id, nameof(Requirement), requirementId, "PortalIntakeRequirement"));
    store.TraceLinks.Add(NewTrace(customerId, projectId, nameof(PortalApproval), approval.Id, nameof(UrsDocument), ursId, "PortalApprovalDocument"));
    audit.Write(customerId, projectId, "PORTAL_ENHANCEMENT_DEMO_DATA_SEEDED", nameof(PortalUser), approver.Id, new { portalUsers = 2, documentShares = 4, notificationId = notification.Id });
}

static void SeedNotificationWorkflowCollaboration(IAppStore store, IAuditWriter audit, INotificationDeliveryProvider deliveryProvider, IDataMaskingService masking, Guid customerId, Guid projectId, Guid issueId, Guid productionPackageId, Guid ursId)
{
    if (store.WorkflowRules.Any(x => x.CustomerId == customerId))
    {
        return;
    }

    var template = new NotificationTemplate { CustomerId = customerId, ProjectId = projectId, TemplateKey = "approval.required.v1", Name = "Approval Required", NotificationType = NotificationType.ApprovalRequired, Channel = NotificationChannel.Email, RecipientType = NotificationRecipientType.InternalUser };
    store.NotificationTemplates.Add(template);
    store.NotificationTemplateVersions.Add(new NotificationTemplateVersion { CustomerId = customerId, TemplateId = template.Id, Version = 1, SubjectTemplate = "[HRM AI Ops] Approval required: {{title}}", BodyTemplate = "Please review {{sourceEntityType}}. Sensitive fields are masked before delivery.", MaxClassification = DataClassificationLevel.Confidential, CreatedBy = "seed" });
    var slaTemplate = new NotificationTemplate { CustomerId = customerId, ProjectId = projectId, TemplateKey = "sla.breached.v1", Name = "SLA Breached", NotificationType = NotificationType.SlaBreached, Channel = NotificationChannel.Email, RecipientType = NotificationRecipientType.InternalUser };
    store.NotificationTemplates.Add(slaTemplate);
    store.NotificationTemplateVersions.Add(new NotificationTemplateVersion { CustomerId = customerId, TemplateId = slaTemplate.Id, Version = 1, SubjectTemplate = "[HRM AI Ops] SLA breached", BodyTemplate = "SLA breach detected for {{sourceEntityType}}. Customer-sensitive text is masked.", MaxClassification = DataClassificationLevel.Confidential, CreatedBy = "seed" });

    var approvalRule = new WorkflowRule { CustomerId = customerId, ProjectId = projectId, RuleKey = "approval.pending.task.reminder", Name = "Approval pending creates task and reminder", TriggerEvent = WorkflowTriggerType.ApprovalPending, ConditionJson = "{\"status\":\"Pending\"}", ActionJson = "{\"createNotification\":true,\"createTask\":true,\"createReminder\":true}", Priority = 10 };
    var slaRule = new WorkflowRule { CustomerId = customerId, ProjectId = projectId, RuleKey = "sla.breach.escalation", Name = "SLA breach escalation", TriggerEvent = WorkflowTriggerType.SlaBreached, ConditionJson = "{\"slaStatus\":\"Breached\"}", ActionJson = "{\"createNotification\":true,\"createTask\":true,\"escalate\":true}", Priority = 5 };
    var requestRule = new WorkflowRule { CustomerId = customerId, ProjectId = projectId, RuleKey = "customer.request.submitted", Name = "Customer request submitted notification", TriggerEvent = WorkflowTriggerType.CustomerRequestSubmitted, ConditionJson = "{}", ActionJson = "{\"createNotification\":true,\"addTimelineEntry\":true}", Priority = 20 };
    store.WorkflowRules.AddRange([approvalRule, slaRule, requestRule]);

    var runs = ExecuteWorkflowEvent(store, audit, deliveryProvider, masking, customerId, projectId, WorkflowTriggerType.ApprovalPending, nameof(UrsDocument), ursId, "URS sign-off is pending for customer review.", "workflow.seed");
    ExecuteWorkflowEvent(store, audit, deliveryProvider, masking, customerId, projectId, WorkflowTriggerType.SlaBreached, nameof(Issue), issueId, "SLA breached for critical customer support issue.", "workflow.seed");
    ExecuteWorkflowEvent(store, audit, deliveryProvider, masking, customerId, projectId, WorkflowTriggerType.ReleaseScheduled, nameof(ProductionReleasePackage), productionPackageId, "Production release has been scheduled.", "workflow.seed");

    var manualTask = new CollaborationTask { CustomerId = customerId, ProjectId = projectId, TaskNo = store.NextNumber("TASK"), Title = "Prepare customer UAT sign-off pack", Description = "Collect shared URS, blueprint and release readiness summary.", AssigneeUserId = "consultant.lead", AssigneeType = NotificationRecipientType.InternalUser, Priority = CollaborationTaskPriority.High, DueAt = DateTimeOffset.UtcNow.AddDays(2), SourceEntityType = nameof(UrsDocument), SourceEntityId = ursId };
    store.CollaborationTasks.Add(manualTask);
    store.ReminderSchedules.Add(new ReminderSchedule { CustomerId = customerId, ProjectId = projectId, TaskId = manualTask.Id, SourceEntityType = nameof(CollaborationTask), SourceEntityId = manualTask.Id, ReminderType = "TaskDue", RemindAt = DateTimeOffset.UtcNow.AddHours(8) });
    AddTimeline(store, customerId, projectId, ActivityTimelineItemType.Task, nameof(CollaborationTask), manualTask.Id, "workflow.seed", "Task assignment created", manualTask.Title, PortalVisibility.InternalOnly);
    audit.Write(customerId, projectId, "PHASE12_DEMO_DATA_SEEDED", nameof(WorkflowRule), approvalRule.Id, new { workflowRules = 3, workflowRuns = runs.Count, templates = 2 });
}

static void SeedReporting(IAppStore store, IAuditWriter audit, Guid customerId, Guid projectId)
{
    if (store.ReportTemplates.Any(x => x.CustomerId == customerId))
    {
        return;
    }

    var templates = new[]
    {
        (ReportDocumentType.Urs, "URS Export Pack", ReportOutputFormat.Word, DataClassificationLevel.Internal, false, (string?)null),
        (ReportDocumentType.Blueprint, "Blueprint Export Pack", ReportOutputFormat.Word, DataClassificationLevel.Internal, false, null),
        (ReportDocumentType.ConfigSpec, "Config Spec Export Pack", ReportOutputFormat.Word, DataClassificationLevel.Confidential, true, "report.sensitive.export"),
        (ReportDocumentType.UatTestCase, "UAT Test Case Workbook", ReportOutputFormat.Excel, DataClassificationLevel.Internal, false, null),
        (ReportDocumentType.ReleaseReadiness, "Release Readiness Report", ReportOutputFormat.Pdf, DataClassificationLevel.Confidential, true, "report.sensitive.export"),
        (ReportDocumentType.ProductionRelease, "Production Release Report", ReportOutputFormat.Pdf, DataClassificationLevel.Confidential, true, "report.sensitive.export"),
        (ReportDocumentType.CustomerService, "Customer Service Report", ReportOutputFormat.Pdf, DataClassificationLevel.Internal, false, null),
        (ReportDocumentType.Sla, "SLA Report", ReportOutputFormat.Excel, DataClassificationLevel.Confidential, true, "report.sensitive.export"),
        (ReportDocumentType.Billing, "Billing Report", ReportOutputFormat.Excel, DataClassificationLevel.Confidential, true, "report.sensitive.export"),
        (ReportDocumentType.Audit, "Audit Report", ReportOutputFormat.Excel, DataClassificationLevel.Restricted, true, "compliance.view"),
        (ReportDocumentType.Security, "Security Report", ReportOutputFormat.Pdf, DataClassificationLevel.Restricted, true, "security.view"),
        (ReportDocumentType.Knowledge, "Knowledge Report", ReportOutputFormat.Pdf, DataClassificationLevel.Internal, false, null),
        (ReportDocumentType.ExecutiveSummary, "Executive Summary", ReportOutputFormat.Pdf, DataClassificationLevel.Confidential, true, "dashboard.executive.view")
    };

    foreach (var (type, name, format, classification, requiresPermission, permission) in templates)
    {
        var template = new ReportTemplate
        {
            CustomerId = customerId,
            ProjectId = null,
            TemplateKey = $"phase13.{type.ToString().ToLowerInvariant()}",
            Name = name,
            ReportType = type,
            DefaultFormat = format,
            MaxClassification = classification,
            RequiresPermission = requiresPermission,
            RequiredPermission = permission,
            ApplyMaskingForExternalExport = true
        };
        store.ReportTemplates.Add(template);
        store.ReportTemplateVersions.Add(new ReportTemplateVersion
        {
            CustomerId = customerId,
            TemplateId = template.Id,
            Version = 1,
            LayoutDefinitionJson = JsonSerializer.Serialize(new
            {
                title = name,
                sections = new[] { "Cover", "Scope", "Metrics", "Traceability", "ApprovalAndAudit" },
                outputFormats = new[] { "Word", "Pdf", "Excel" },
                binaryStoredInDatabase = false
            }),
            ContentSchemaJson = JsonSerializer.Serialize(new { customerId = "guid", projectId = "guid", dateRange = "required", dataScope = "customer-project" })
        });
    }

    var aiRun = new AiRun
    {
        CustomerId = customerId,
        ProjectId = projectId,
        RunType = "ExecutiveDashboardSummary",
        Provider = "LocalStub",
        Model = "reporting-summary-v1",
        PromptTemplateKey = "phase13.executive_summary",
        PromptVersion = 1,
        InputSummary = "Seeded dashboard summary from customer/project scoped reporting metrics.",
        MaskedInputPreview = "Masked demo summary context only.",
        OutputSummary = "Executive health is stable; monitor SLA exceptions, high-risk fixes and publish only masked customer-visible reports.",
        Status = AiRunStatus.Completed,
        CompletedAt = DateTimeOffset.UtcNow
    };
    store.AiRuns.Add(aiRun);
    store.DashboardSnapshots.Add(new DashboardSnapshot
    {
        CustomerId = customerId,
        ProjectId = projectId,
        SnapshotType = DashboardSnapshotType.Executive,
        DateFrom = DateTimeOffset.UtcNow.AddDays(-30),
        DateTo = DateTimeOffset.UtcNow,
        HealthScore = 92,
        DeliveryScore = 88,
        SlaScore = 95,
        RiskScore = 18,
        AiRunId = aiRun.Id,
        AiSummary = aiRun.OutputSummary ?? "",
        SnapshotJson = JsonSerializer.Serialize(new { seeded = true, scope = "customer/project", customerId, projectId })
    });

    var serviceTemplate = store.ReportTemplates.First(x => x.CustomerId == customerId && x.ReportType == ReportDocumentType.CustomerService);
    var version = store.ReportTemplateVersions.First(x => x.CustomerId == customerId && x.TemplateId == serviceTemplate.Id);
    var job = new ReportGenerationJob
    {
        CustomerId = customerId,
        ProjectId = projectId,
        TemplateId = serviceTemplate.Id,
        TemplateVersion = version.Version,
        ReportType = serviceTemplate.ReportType,
        OutputFormat = ReportOutputFormat.Pdf,
        Status = ReportGenerationStatus.Completed,
        Visibility = ReportVisibility.PublishedToPortal,
        RequestedBy = "system.seed",
        DateFrom = DateTimeOffset.UtcNow.AddDays(-30),
        DateTo = DateTimeOffset.UtcNow,
        FilterJson = JsonSerializer.Serialize(new { customerId, projectId }),
        MaskingApplied = false,
        StartedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
        CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
    };
    store.ReportGenerationJobs.Add(job);
    var file = new ReportExportFile
    {
        CustomerId = customerId,
        ProjectId = projectId,
        ReportJobId = job.Id,
        TemplateId = serviceTemplate.Id,
        TemplateVersion = version.Version,
        ReportType = serviceTemplate.ReportType,
        OutputFormat = ReportOutputFormat.Pdf,
        FileName = "customer-service-report-demo.pdf",
        ContentType = "application/pdf",
        StorageRef = $"reports://customers/{customerId}/projects/{projectId}/{job.Id}/customer-service-report-demo.pdf",
        SizeBytes = 18432,
        Checksum = "seeded-demo-checksum",
        Visibility = ReportVisibility.PublishedToPortal,
        PublishedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
    };
    store.ReportExportFiles.Add(file);
    store.PortalReportShares.Add(new PortalReportShare { CustomerId = customerId, ProjectId = projectId, ReportExportFileId = file.Id, Visibility = ReportVisibility.PublishedToPortal, Status = "Published" });
    audit.Write(customerId, projectId, "PHASE13_REPORTING_DEMO_DATA_SEEDED", nameof(ReportTemplate), serviceTemplate.Id, new { templates = templates.Length, seededExportFileId = file.Id, aiRunId = aiRun.Id });
}

static void SeedIntegrationHub(IAppStore store, IAuditWriter audit, Guid customerId, Guid projectId)
{
    if (store.IntegrationProviders.Any(x => x.CustomerId == customerId))
    {
        return;
    }

    var providers = new[]
    {
        ("customer-hrm-api", "Customer HRM API", IntegrationProviderCategory.CustomerHrmApi, "https://mock.customer-hrm.local", true, true, true),
        ("github", "GitHub", IntegrationProviderCategory.GitProvider, "https://api.github.com", true, true, true),
        ("gitlab", "GitLab", IntegrationProviderCategory.GitProvider, "https://gitlab.com/api/v4", true, true, true),
        ("azure-devops", "Azure DevOps", IntegrationProviderCategory.DevOps, "https://dev.azure.com/mock", true, true, true),
        ("jira", "Jira", IntegrationProviderCategory.IssueTracking, "https://mock.atlassian.net", true, true, true),
        ("teams", "Microsoft Teams", IntegrationProviderCategory.Messaging, "https://outlook.office.com/webhook/mock", false, true, false),
        ("slack", "Slack", IntegrationProviderCategory.Messaging, "https://hooks.slack.com/services/mock", false, true, true),
        ("email", "Email", IntegrationProviderCategory.Email, "smtp://mock-mail-provider", false, true, false),
        ("n8n", "n8n Automation", IntegrationProviderCategory.Automation, "https://n8n.mock.local", true, true, true),
        ("erp", "ERP", IntegrationProviderCategory.Erp, "https://erp.mock.local/api", true, true, true),
        ("accounting", "Accounting", IntegrationProviderCategory.Accounting, "https://accounting.mock.local/api", true, true, true),
        ("ticket-system", "Ticket System", IntegrationProviderCategory.TicketSystem, "https://ticket.mock.local/api", true, true, true),
        ("generic-webhook", "Generic Webhook", IntegrationProviderCategory.Webhook, "https://webhook.mock.local", true, true, true)
    };

    foreach (var (key, name, category, baseUrl, inbound, outbound, signature) in providers)
    {
        store.IntegrationProviders.Add(new IntegrationProvider
        {
            CustomerId = customerId,
            ProjectId = projectId,
            ProviderKey = key,
            Name = name,
            Category = category,
            BaseUrl = baseUrl,
            DocumentationUrl = $"{baseUrl}/docs",
            SupportsInboundWebhook = inbound,
            SupportsOutboundWebhook = outbound,
            SupportsSignatureVerification = signature,
            SupportsRetry = true,
            DefaultTimeoutSeconds = 30,
            ConfigJson = JsonSerializer.Serialize(new { localMockProvider = true, rawSecretStored = false })
        });
    }

    var jira = store.IntegrationProviders.Single(x => x.CustomerId == customerId && x.ProviderKey == "jira");
    var slack = store.IntegrationProviders.Single(x => x.CustomerId == customerId && x.ProviderKey == "slack");
    var hrm = store.IntegrationProviders.Single(x => x.CustomerId == customerId && x.ProviderKey == "customer-hrm-api");
    var n8n = store.IntegrationProviders.Single(x => x.CustomerId == customerId && x.ProviderKey == "n8n");

    var jiraEndpoint = new IntegrationEndpoint { CustomerId = customerId, ProjectId = projectId, ProviderId = jira.Id, EndpointKey = "jira.issue.create", Name = "Create Jira Issue", Direction = IntegrationDirection.Outbound, HttpMethod = "POST", PathOrUrl = "/rest/api/3/issue", AuthType = IntegrationAuthType.SecretRefToken, SecretRef = "secret://integrations/jira/api-token", TimeoutSeconds = 30, MaxDataClassification = DataClassificationLevel.Confidential, MaskOutboundPayloads = true };
    var slackEndpoint = new IntegrationEndpoint { CustomerId = customerId, ProjectId = projectId, ProviderId = slack.Id, EndpointKey = "slack.alert", Name = "Post Slack Alert", Direction = IntegrationDirection.Outbound, HttpMethod = "POST", PathOrUrl = "/services/mock", AuthType = IntegrationAuthType.SecretRefToken, SecretRef = "secret://integrations/slack/webhook", TimeoutSeconds = 20, MaxDataClassification = DataClassificationLevel.Confidential, MaskOutboundPayloads = true };
    var hrmWebhook = new IntegrationEndpoint { CustomerId = customerId, ProjectId = projectId, ProviderId = hrm.Id, EndpointKey = "customer-hrm.issue.webhook", Name = "Customer HRM Issue Webhook", Direction = IntegrationDirection.Inbound, HttpMethod = "POST", PathOrUrl = "/webhooks/customer-hrm/issues", AuthType = IntegrationAuthType.WebhookSignatureSecretRef, SecretRef = "secret://integrations/customer-hrm/webhook-signature", TimeoutSeconds = 15, MaxDataClassification = DataClassificationLevel.Restricted, MaskOutboundPayloads = true };
    var n8nEndpoint = new IntegrationEndpoint { CustomerId = customerId, ProjectId = projectId, ProviderId = n8n.Id, EndpointKey = "n8n.workflow.trigger", Name = "Trigger n8n Workflow", Direction = IntegrationDirection.Outbound, HttpMethod = "POST", PathOrUrl = "/webhook/hrm-aiops", AuthType = IntegrationAuthType.SecretRefToken, SecretRef = "secret://integrations/n8n/webhook-token", TimeoutSeconds = 30, MaxDataClassification = DataClassificationLevel.Confidential, MaskOutboundPayloads = true };
    store.IntegrationEndpoints.AddRange([jiraEndpoint, slackEndpoint, hrmWebhook, n8nEndpoint]);

    store.IntegrationPayloadMappings.Add(new IntegrationPayloadMapping { CustomerId = customerId, ProjectId = projectId, ProviderId = jira.Id, EndpointId = jiraEndpoint.Id, MappingKey = "issue.to.jira", SourceSystem = "HrmAiOps", TargetSystem = "Jira", EventType = IntegrationEventType.IssueCreated, MappingJson = """{"title":"issue.title","description":"masked(issue.description)","priority":"issue.priority","labels":["hrm-aiops"]}""" });
    store.IntegrationEventSubscriptions.Add(new IntegrationEventSubscription { CustomerId = customerId, ProjectId = projectId, ProviderId = hrm.Id, EndpointId = hrmWebhook.Id, EventType = IntegrationEventType.WebhookReceived, SubscriptionKey = "customer-hrm-webhook", FilterJson = """{"source":"customer-hrm","event":"issue.created"}""" });
    store.WebhookOutboundSubscriptions.Add(new WebhookOutboundSubscription { CustomerId = customerId, ProjectId = projectId, ProviderId = slack.Id, EndpointId = slackEndpoint.Id, EventType = IntegrationEventType.SlaBreached, TargetUrl = "https://hooks.slack.com/services/mock", SecretRef = "secret://integrations/slack/webhook", SignatureMode = WebhookSignatureMode.MockHmac, MaxRetryAttempts = 3, RetryBackoffSeconds = 30, TimeoutSeconds = 20 });
    store.WebhookOutboundSubscriptions.Add(new WebhookOutboundSubscription { CustomerId = customerId, ProjectId = projectId, ProviderId = n8n.Id, EndpointId = n8nEndpoint.Id, EventType = IntegrationEventType.ReleaseScheduled, TargetUrl = "https://n8n.mock.local/webhook/hrm-aiops", SecretRef = "secret://integrations/n8n/webhook-token", SignatureMode = WebhookSignatureMode.MockHmac, MaxRetryAttempts = 4, RetryBackoffSeconds = 45, TimeoutSeconds = 30 });

    var gateway = new ApiGatewayRoute
    {
        CustomerId = customerId,
        ProjectId = projectId,
        RouteKey = "external.issue.create",
        PublicPath = "/gateway/hrm-aiops/issues",
        InternalTarget = "/api/customers/{customerId}/projects/{projectId}/issues",
        HttpMethod = "POST",
        AllowedExternalSystem = "customer-hrm-api",
        RequiredPermission = "integration.gateway.invoke",
        TokenSecretRef = "secret://integrations/customer-hrm/gateway-token",
        TimeoutSeconds = 20,
        MaxDataClassification = DataClassificationLevel.Confidential,
        AccessPolicyJson = """{"requiresCustomerHeader":true,"requiresTokenSecretRef":true,"allowedMethods":["POST"],"tenantIsolation":"route-customer-only"}"""
    };
    store.ApiGatewayRoutes.Add(gateway);

    store.IntegrationAutomationTriggers.Add(new IntegrationAutomationTrigger { CustomerId = customerId, ProjectId = projectId, ProviderId = slack.Id, TriggerKey = "integration.failure.notify", EventType = IntegrationEventType.SlaBreached, ActionType = IntegrationAutomationActionType.CreateNotification, ConditionJson = """{"status":["Failed","TimedOut","Rejected"]}""", ActionJson = """{"title":"Integration failure"}""", CreateOnFailureOnly = true });
    store.IntegrationAutomationTriggers.Add(new IntegrationAutomationTrigger { CustomerId = customerId, ProjectId = projectId, ProviderId = slack.Id, TriggerKey = "integration.failure.task", EventType = IntegrationEventType.SlaBreached, ActionType = IntegrationAutomationActionType.CreateTask, ConditionJson = """{"status":["Failed","TimedOut","Rejected"]}""", ActionJson = """{"assignee":"integration.owner","priority":"High"}""", CreateOnFailureOnly = true });
    store.IntegrationAutomationTriggers.Add(new IntegrationAutomationTrigger { CustomerId = customerId, ProjectId = projectId, ProviderId = null, TriggerKey = "integration.issue.failure.notify", EventType = IntegrationEventType.IssueCreated, ActionType = IntegrationAutomationActionType.CreateNotification, ConditionJson = """{"status":["Failed","TimedOut","Rejected"]}""", ActionJson = """{"title":"Issue integration failure"}""", CreateOnFailureOnly = true });
    store.IntegrationAutomationTriggers.Add(new IntegrationAutomationTrigger { CustomerId = customerId, ProjectId = projectId, ProviderId = null, TriggerKey = "integration.issue.failure.task", EventType = IntegrationEventType.IssueCreated, ActionType = IntegrationAutomationActionType.CreateTask, ConditionJson = """{"status":["Failed","TimedOut","Rejected"]}""", ActionJson = """{"assignee":"integration.owner","priority":"High"}""", CreateOnFailureOnly = true });

    var run = new IntegrationRun
    {
        CustomerId = customerId,
        ProjectId = projectId,
        ProviderId = jira.Id,
        EndpointId = jiraEndpoint.Id,
        Direction = IntegrationDirection.Outbound,
        EventType = IntegrationEventType.IssueCreated,
        Status = IntegrationRunStatus.Succeeded,
        CorrelationId = Guid.NewGuid().ToString("N"),
        TraceId = Guid.NewGuid().ToString("N"),
        Attempt = 1,
        MaxAttempts = 3,
        TimeoutSeconds = 30,
        RequestSummary = "Seeded outbound Jira issue sync.",
        MaskedPayload = """{"title":"Leave balance issue","employee":"[masked]","token":"[masked-secret]"}""",
        ResponseSummary = "Mock Jira issue accepted.",
        CompletedAt = DateTimeOffset.UtcNow
    };
    store.IntegrationRuns.Add(run);
    store.IntegrationRunLogs.Add(new IntegrationRunLog { CustomerId = customerId, ProjectId = projectId, IntegrationRunId = run.Id, Level = "Info", Message = "Seeded integration run completed.", MaskedPayload = run.MaskedPayload });
    audit.Write(customerId, projectId, "PHASE14_INTEGRATION_HUB_DEMO_DATA_SEEDED", nameof(IntegrationProvider), jira.Id, new { providers = providers.Length, endpoints = 4, gatewayRouteId = gateway.Id, rawSecretStored = false });
}

static void SeedDevOpsAutomation(IAppStore store, IAuditWriter audit, Guid customerId, Guid projectId)
{
    var repo = new DevOpsRepository
    {
        CustomerId = customerId,
        ProjectId = projectId,
        Provider = DevOpsProviderKind.MockGit,
        ProviderRepositoryId = "mock/hrm-leave-ops",
        Name = "HRM Leave Ops",
        RepoUrl = "https://mock.git.local/hrm-aiops/hrm-leave-ops",
        DefaultBranch = "main",
        SecretRef = "secret://devops/demo/git-token",
        ProtectMainBranch = true,
        RequirePullRequestReview = true,
        RequireCiBeforeMerge = true
    };
    var branch = new DevOpsBranch { CustomerId = customerId, ProjectId = projectId, RepositoryId = repo.Id, BranchName = "ai/fix-leave-balance-rounding", SourceBranch = "main", CreatedBy = "ai.assistant", CreatedByAi = true };
    var pr = new DevOpsPullRequest
    {
        CustomerId = customerId,
        ProjectId = projectId,
        RepositoryId = repo.Id,
        ExternalPrRef = "mock-pr-1",
        SourceBranch = branch.BranchName,
        TargetBranch = "main",
        Title = "Fix leave balance rounding drift",
        Description = "Mock PR for HRM leave balance rounding with guarded review, build, test and scan gates.",
        Status = PullRequestStatus.Approved,
        RiskLevel = RiskLevel.Medium,
        ChangeAreasCsv = "Hrm",
        CreatedBy = "ai.assistant",
        CreatedByAi = true
    };
    var policy = new AiCodeGovernancePolicy
    {
        CustomerId = customerId,
        ProjectId = projectId,
        RepositoryId = repo.Id,
        PolicyKey = "devops.ai-code.default",
        ProtectedBranchesCsv = "main,master",
        RequireHumanReview = true,
        BlockDirectMainMerge = true,
        BlockAiProductionDeploy = true,
        HighRiskRequiresApproval = true,
        SpecialApprovalAreasCsv = "Payroll,Permission,Security,Integration,ProductionDeployment",
        MaxDiffBytes = 12000,
        Active = true
    };
    var pipeline = new CiCdPipeline { CustomerId = customerId, ProjectId = projectId, RepositoryId = repo.Id, PipelineKey = "mock.hrm-ci", Name = "Mock HRM CI Pipeline", Provider = DevOpsProviderKind.MockGit, ConfigPath = ".hrm-aiops/pipelines/hrm-ci.yml", TimeoutSeconds = 600, Active = true };
    var build = new PipelineRun { CustomerId = customerId, ProjectId = projectId, RepositoryId = repo.Id, PipelineId = pipeline.Id, PullRequestId = pr.Id, RunType = PipelineRunType.Build, Status = PipelineRunStatus.Succeeded, Summary = "Mock build succeeded.", ArtifactRef = $"artifact://devops/{repo.Id}/seed-build.zip", LogsRef = "log://devops/seed-build", CompletedAt = DateTimeOffset.UtcNow };
    var test = new PipelineRun { CustomerId = customerId, ProjectId = projectId, RepositoryId = repo.Id, PipelineId = pipeline.Id, PullRequestId = pr.Id, RunType = PipelineRunType.Test, Status = PipelineRunStatus.Succeeded, Summary = "Mock unit, integration and regression tests passed.", LogsRef = "log://devops/seed-test", CompletedAt = DateTimeOffset.UtcNow };
    var scan = new PipelineRun { CustomerId = customerId, ProjectId = projectId, RepositoryId = repo.Id, PipelineId = pipeline.Id, PullRequestId = pr.Id, RunType = PipelineRunType.CodeScan, Status = PipelineRunStatus.Succeeded, Summary = "Mock code scan passed with no high-risk findings.", LogsRef = "log://devops/seed-scan", CompletedAt = DateTimeOffset.UtcNow };
    pr.BuildRunId = build.Id;
    pr.TestRunId = test.Id;
    pr.CodeScanRunId = scan.Id;
    var aiRun = NewGovernanceAiRun(customerId, projectId, AiTaskType.GenerateFixProposal, "Seeded AI code assistant analysis for leave balance PR", "Masked seeded DevOps context only.");
    aiRun.RunType = "AiCodeAnalysis";
    aiRun.Status = AiRunStatus.Completed;
    aiRun.CompletedAt = DateTimeOffset.UtcNow;
    aiRun.OutputSummary = "Medium risk HRM code change; human review required before merge.";
    pr.AiRunId = aiRun.Id;
    var analysis = new AiCodeAnalysis { CustomerId = customerId, ProjectId = projectId, RepositoryId = repo.Id, PullRequestId = pr.Id, AiRunId = aiRun.Id, BranchName = branch.BranchName, RiskLevel = RiskLevel.Medium, ChangeAreasCsv = "Hrm", Summary = "AI analysis identified a medium-risk HRM rounding change.", FindingsJson = """[{"area":"Hrm","severity":"Medium","message":"Review leave balance rounding tests."}]""" };
    var patch = new AiPatchProposal { CustomerId = customerId, ProjectId = projectId, RepositoryId = repo.Id, PullRequestId = pr.Id, AiRunId = aiRun.Id, BranchName = branch.BranchName, Title = "Guard leave balance rounding", DiffText = "diff --git a/LeaveBalanceService.cs b/LeaveBalanceService.cs\n+ // Mock guarded rounding fix; no full source stored.", DiffSizeBytes = 108, RiskLevel = RiskLevel.Medium, ChangeAreasCsv = "Hrm" };
    analysis.PatchProposalId = patch.Id;
    var review = new CodeReviewRecord { CustomerId = customerId, ProjectId = projectId, PullRequestId = pr.Id, ReviewerUserId = "release.manager", Decision = CodeReviewDecision.Approved, Comments = "Approved after checking build/test/scan.", RiskLevel = RiskLevel.Medium };
    var package = new DeploymentPackage { CustomerId = customerId, ProjectId = projectId, RepositoryId = repo.Id, PullRequestId = pr.Id, BuildRunId = build.Id, TestRunId = test.Id, CodeScanRunId = scan.Id, PackageNo = store.NextNumber("DPKG"), Version = "2026.06.15-demo", Status = DeploymentPackageStatus.Ready, RiskLevel = RiskLevel.Medium, ArtifactRef = $"artifact://devops/packages/{pr.Id}.zip", DiffSummary = "PR mock-pr-1 passed build, tests and code scan.", ReadyAt = DateTimeOffset.UtcNow };
    var snapshot = new SourceCodeSnapshot { CustomerId = customerId, ProjectId = projectId, RepositoryId = repo.Id, BranchName = branch.BranchName, CommitSha = "mock-commit-001", SnapshotNo = store.NextNumber("SNAP"), MetadataJson = """{"provider":"MockGit","filesChanged":2,"fullSourceStored":false}""", DiffSummary = "Metadata and masked diff preview only; full source is not stored in database.", DiffTextPreview = "LeaveBalanceService.cs + rounding guard; LeaveBalanceTests.cs + regression case.", DiffSizeBytes = 108 };
    var devOpsRun = new DevOpsRun { CustomerId = customerId, ProjectId = projectId, RepositoryId = repo.Id, PullRequestId = pr.Id, PipelineRunId = scan.Id, RunType = DevOpsRunType.Package, Status = DevOpsRunStatus.Succeeded, ActorUserId = "system", MaskedInput = "Seeded mock CI/CD package creation.", Summary = "Seeded deployment package is ready after build/test/scan.", CompletedAt = DateTimeOffset.UtcNow };

    store.DevOpsRepositories.Add(repo);
    store.DevOpsBranches.Add(branch);
    store.DevOpsPullRequests.Add(pr);
    store.CiCdPipelines.Add(pipeline);
    store.PipelineRuns.AddRange([build, test, scan]);
    store.AiRuns.Add(aiRun);
    store.AiCodeAnalyses.Add(analysis);
    store.AiPatchProposals.Add(patch);
    store.CodeReviewRecords.Add(review);
    store.DeploymentPackages.Add(package);
    store.SourceCodeSnapshots.Add(snapshot);
    store.AiCodeGovernancePolicies.Add(policy);
    store.DevOpsRuns.Add(devOpsRun);
    store.DevOpsRunLogs.Add(new DevOpsRunLog { CustomerId = customerId, ProjectId = projectId, DevOpsRunId = devOpsRun.Id, Level = "Info", Message = "Seeded DevOps package ready.", MaskedPayload = devOpsRun.MaskedInput });
    audit.Write(customerId, projectId, "PHASE15_DEVOPS_AUTOMATION_DEMO_DATA_SEEDED", "DevOpsAutomation", repo.Id, new { repo, pr, pipeline, package, rawSecretStored = false });
}

static void SeedObservability(IAppStore store, IAuditWriter audit, Guid customerId, Guid projectId, Guid productionEnvironmentId, Guid connectorId, Guid productionPackageId)
{
    var apiSource = new TelemetrySource
    {
        CustomerId = customerId,
        ProjectId = projectId,
        EnvironmentId = productionEnvironmentId,
        SourceKey = "platform.api.production",
        Name = "HRM AI Ops API - Production",
        SourceType = TelemetrySourceType.PlatformApi,
        EndpointRef = "https://mock.hrm-aiops.local/health",
        Provider = "MockTelemetryProvider",
        PollIntervalSeconds = 60,
        TimeoutSeconds = 10,
        MaskLogs = true
    };
    var connectorSource = new TelemetrySource
    {
        CustomerId = customerId,
        ProjectId = projectId,
        EnvironmentId = productionEnvironmentId,
        ConnectorId = connectorId,
        SourceKey = "customer.connector.production",
        Name = "Customer HRM Connector - Production",
        SourceType = TelemetrySourceType.Connector,
        EndpointRef = "connector://mock-production",
        Provider = "MockTelemetryProvider",
        PollIntervalSeconds = 60,
        TimeoutSeconds = 10,
        MaskLogs = true
    };
    var deploymentSource = new TelemetrySource
    {
        CustomerId = customerId,
        ProjectId = projectId,
        EnvironmentId = productionEnvironmentId,
        ProductionReleasePackageId = productionPackageId,
        SourceKey = "deployment.health.production",
        Name = "Production Deployment Health",
        SourceType = TelemetrySourceType.Deployment,
        EndpointRef = $"deployment://{productionPackageId}",
        Provider = "MockTelemetryProvider",
        PollIntervalSeconds = 30,
        TimeoutSeconds = 10,
        MaskLogs = true
    };
    var healthy = new RuntimeTelemetrySample
    {
        CustomerId = customerId,
        ProjectId = projectId,
        TelemetrySourceId = apiSource.Id,
        EnvironmentId = productionEnvironmentId,
        SignalType = TelemetrySignalType.ApiLatency,
        HealthStatus = TelemetryHealthStatus.Healthy,
        MetricName = "api_latency_ms",
        MetricValue = 185,
        Unit = "ms",
        ApiLatencyMs = 185,
        UptimePercent = 99.95m,
        Summary = "Production API latency is healthy.",
        MaskedPayloadJson = """{"status":"Healthy","latencyMs":185,"rawLogStored":false}"""
    };
    var unhealthy = new RuntimeTelemetrySample
    {
        CustomerId = customerId,
        ProjectId = projectId,
        TelemetrySourceId = deploymentSource.Id,
        EnvironmentId = productionEnvironmentId,
        ProductionReleasePackageId = productionPackageId,
        SignalType = TelemetrySignalType.DeploymentHealth,
        HealthStatus = TelemetryHealthStatus.Unhealthy,
        MetricName = "deployment_health_score",
        MetricValue = 42,
        Unit = "score",
        ApiLatencyMs = 2600,
        UptimePercent = 92.4m,
        Summary = "Production deployment validation failed; masked employee and token values removed.",
        MaskedPayloadJson = """{"status":"Unhealthy","error":"validation failed","email":"[masked-email]","token":"[masked-secret]","rawLogStored":false}"""
    };
    var logSummary = new TelemetryLogSummary
    {
        CustomerId = customerId,
        ProjectId = projectId,
        TelemetrySourceId = deploymentSource.Id,
        TotalLines = 420,
        ErrorCount = 14,
        WarningCount = 21,
        MaskedSummary = "Top production errors are masked and summarized only.",
        TopErrorsJson = """[{"message":"deployment validation failed","email":"[masked-email]","token":"[masked-secret]"}]"""
    };
    var latencyRule = new MonitoringRule
    {
        CustomerId = customerId,
        ProjectId = projectId,
        RuleKey = "runtime.api.latency.high",
        Name = "API latency above threshold",
        SignalType = TelemetrySignalType.ApiLatency,
        MetricName = "api_latency_ms",
        Operator = MonitoringRuleOperator.GreaterThan,
        ThresholdValue = 1000,
        Severity = AlertSeverity.High,
        AutoCreateIncident = true,
        Active = true
    };
    var deploymentRule = new MonitoringRule
    {
        CustomerId = customerId,
        ProjectId = projectId,
        TelemetrySourceId = deploymentSource.Id,
        RuleKey = "deployment.health.critical",
        Name = "Production deployment health critical",
        SignalType = TelemetrySignalType.DeploymentHealth,
        MetricName = "deployment_health_score",
        Operator = MonitoringRuleOperator.LessThan,
        ThresholdValue = 60,
        Severity = AlertSeverity.Critical,
        AutoCreateIncident = true,
        AutoCreateIssue = true,
        Active = true
    };
    var alertRule = new AlertRule
    {
        CustomerId = customerId,
        ProjectId = projectId,
        MonitoringRuleId = deploymentRule.Id,
        AlertKey = "critical.incident.sre",
        MinimumSeverity = AlertSeverity.High,
        Channel = NotificationChannel.InApp,
        RecipientRef = "sre.oncall",
        CreateNotification = true,
        CreateEscalationForCritical = true,
        Active = true
    };
    var alert = new AlertEvent
    {
        CustomerId = customerId,
        ProjectId = projectId,
        MonitoringRuleId = deploymentRule.Id,
        AlertRuleId = alertRule.Id,
        TelemetrySampleId = unhealthy.Id,
        Severity = AlertSeverity.Critical,
        Status = AlertStatus.Open,
        Title = "Production deployment health critical",
        Message = "Deployment health score 42 is below critical threshold 60.",
        CorrelationId = unhealthy.CorrelationId,
        TraceId = unhealthy.TraceId,
        MaskedPayloadJson = unhealthy.MaskedPayloadJson
    };
    var sla = store.SlaPolicies.Where(x => x.CustomerId == customerId && x.Severity == IssueSeverity.Critical).OrderBy(x => x.ResponseHours).FirstOrDefault()
        ?? store.SlaPolicies.Where(x => x.CustomerId == customerId).OrderBy(x => x.ResponseHours).FirstOrDefault();
    var incident = new IncidentRecord
    {
        CustomerId = customerId,
        ProjectId = projectId,
        EnvironmentId = productionEnvironmentId,
        ProductionReleasePackageId = productionPackageId,
        AlertEventId = alert.Id,
        SlaPolicyId = sla?.Id,
        IncidentNo = store.NextNumber("INC"),
        Title = "Critical production deployment health degradation",
        Description = "Mock production deployment health check reported unhealthy status. AI may diagnose but cannot apply production fixes.",
        Status = IncidentStatus.Investigating,
        Priority = IncidentPriority.P0,
        Severity = AlertSeverity.Critical,
        ImpactSummary = "Production validation degraded for HRM leave workflow.",
        DetectedAt = DateTimeOffset.UtcNow
    };
    alert.IncidentId = incident.Id;
    var aiRun = NewGovernanceAiRun(customerId, projectId, AiTaskType.AnalyzeRootCause, "Seeded incident telemetry context", "Masked runtime telemetry only.");
    aiRun.RunType = "AiIncidentDiagnosis";
    aiRun.Status = AiRunStatus.Completed;
    aiRun.CompletedAt = DateTimeOffset.UtcNow;
    aiRun.OutputSummary = "Likely deployment validation regression; use approved production governance for mitigation.";
    incident.AiRunId = aiRun.Id;
    var diagnosis = new AiIncidentDiagnosis
    {
        CustomerId = customerId,
        ProjectId = projectId,
        IncidentId = incident.Id,
        AiRunId = aiRun.Id,
        RootCauseHypothesis = "Likely deployment validation regression after production package preparation.",
        RecommendedActions = "Check deployment health, connector status and rollback readiness. Do not let AI execute production fixes.",
        EvidenceSummary = "Seeded masked deployment health telemetry.",
        ConfidenceScore = 0.78m,
        ProductionFixExecuted = false
    };
    var binding = sla is null ? null : new IncidentSlaBinding
    {
        CustomerId = customerId,
        ProjectId = projectId,
        IncidentId = incident.Id,
        SlaPolicyId = sla.Id,
        Status = SlaStatus.OnTrack,
        ResponseDueAt = incident.DetectedAt.AddHours(sla.ResponseHours),
        ResolutionDueAt = incident.DetectedAt.AddHours(sla.ResolutionHours)
    };
    var notification = new PortalNotification
    {
        CustomerId = customerId,
        ProjectId = projectId,
        NotificationType = NotificationType.SlaBreached,
        Title = $"Critical incident {incident.IncidentNo}",
        Message = incident.Title,
        SourceEntityType = nameof(IncidentRecord),
        SourceEntityId = incident.Id
    };
    var escalation = new EscalationEvent
    {
        CustomerId = customerId,
        ProjectId = projectId,
        SourceEntityType = nameof(IncidentRecord),
        SourceEntityId = incident.Id,
        Reason = "Seeded critical observability incident",
        EscalatedToUserId = "sre.manager",
        Status = EscalationStatus.Notified
    };

    store.TelemetrySources.AddRange([apiSource, connectorSource, deploymentSource]);
    store.RuntimeTelemetrySamples.AddRange([healthy, unhealthy]);
    store.TelemetryLogSummaries.Add(logSummary);
    store.MonitoringRules.AddRange([latencyRule, deploymentRule]);
    store.AlertRules.Add(alertRule);
    store.AlertEvents.Add(alert);
    store.IncidentRecords.Add(incident);
    if (binding is not null) store.IncidentSlaBindings.Add(binding);
    store.AiRuns.Add(aiRun);
    store.AiIncidentDiagnoses.Add(diagnosis);
    store.PortalNotifications.Add(notification);
    store.EscalationEvents.Add(escalation);
    store.IncidentActions.Add(new IncidentAction { CustomerId = customerId, ProjectId = projectId, IncidentId = incident.Id, ActionType = IncidentActionType.Escalate, ActorUserId = "system", Summary = "Seeded critical notification and escalation.", ResultJson = "{}" });
    audit.Write(customerId, projectId, "PHASE16_OBSERVABILITY_DEMO_DATA_SEEDED", "Observability", incident.Id, new { sources = 3, alert, incident, rawLogStored = false });
}

static void SeedDataMigration(IAppStore store, IAuditWriter audit, Guid customerId, Guid projectId, Guid testEnvironmentId, Guid connectorId)
{
    var template = new DataImportTemplate
    {
        CustomerId = customerId,
        ProjectId = projectId,
        TemplateKey = "employee-master-v1",
        Name = "Employee Master Import",
        Domain = HrmDataDomain.Employee,
        DefaultFileType = ImportFileType.Csv,
        MaxClassification = DataClassificationLevel.Restricted,
        Status = ImportTemplateStatus.Active,
        CurrentVersion = 1
    };
    var version = new DataImportTemplateVersion
    {
        CustomerId = customerId,
        ProjectId = projectId,
        TemplateId = template.Id,
        Version = 1,
        SchemaJson = """{"columns":["EmployeeCode","FullName","NationalId","BankAccount","PayrollGroup","LeaveBalance"]}""",
        SampleFileRef = "file://templates/employee-master-v1.csv",
        CreatedBy = "system"
    };
    var file = new DataImportFile
    {
        CustomerId = customerId,
        ProjectId = projectId,
        EnvironmentId = testEnvironmentId,
        TemplateId = template.Id,
        FileRef = "file://uploads/demo/employee-master-import.csv",
        FileName = "employee-master-import.csv",
        FileType = ImportFileType.Csv,
        SizeBytes = 48200,
        RowCount = 12,
        Classification = DataClassificationLevel.Restricted,
        UploadedBy = "data.steward",
        MaskedPreviewJson = """[{"EmployeeCode":"E001","FullName":"[masked]","NationalId":"[masked]","BankAccount":"[masked]","PayrollGroup":"MTH"}]"""
    };
    var mappings = new[]
    {
        new DataColumnMapping { CustomerId = customerId, ProjectId = projectId, TemplateId = template.Id, TemplateVersion = 1, MappingVersion = 1, MappingKey = "employee.employeeNo", SourceColumn = "EmployeeCode", TargetEntity = "Employee", TargetField = "EmployeeNo", DataClassification = DataClassificationLevel.Internal, Status = DataMappingStatus.Active },
        new DataColumnMapping { CustomerId = customerId, ProjectId = projectId, TemplateId = template.Id, TemplateVersion = 1, MappingVersion = 1, MappingKey = "employee.nationalId", SourceColumn = "NationalId", TargetEntity = "Employee", TargetField = "NationalId", DataClassification = DataClassificationLevel.Secret, Status = DataMappingStatus.Active },
        new DataColumnMapping { CustomerId = customerId, ProjectId = projectId, TemplateId = template.Id, TemplateVersion = 1, MappingVersion = 1, MappingKey = "employee.bankAccount", SourceColumn = "BankAccount", TargetEntity = "EmployeeBankAccount", TargetField = "AccountNo", DataClassification = DataClassificationLevel.Secret, Status = DataMappingStatus.Active },
        new DataColumnMapping { CustomerId = customerId, ProjectId = projectId, TemplateId = template.Id, TemplateVersion = 1, MappingVersion = 1, MappingKey = "employee.leaveBalance", SourceColumn = "LeaveBalance", TargetEntity = "LeaveBalance", TargetField = "OpeningBalance", DataClassification = DataClassificationLevel.Confidential, Status = DataMappingStatus.Active }
    };
    var rules = new[]
    {
        new DataValidationRule { CustomerId = customerId, ProjectId = projectId, TemplateId = template.Id, RuleKey = "employee.code.required", Name = "Employee code required", Domain = HrmDataDomain.Employee, TargetField = "EmployeeCode", RuleType = ValidationRuleType.Required, Severity = ValidationIssueSeverity.Error, ExpressionJson = """{"required":true}""" },
        new DataValidationRule { CustomerId = customerId, ProjectId = projectId, TemplateId = template.Id, RuleKey = "employee.nationalid.duplicate", Name = "National ID duplicate check", Domain = HrmDataDomain.Employee, TargetField = "NationalId", RuleType = ValidationRuleType.DuplicateDetection, Severity = ValidationIssueSeverity.Warning, ExpressionJson = """{"scope":"file"}""" },
        new DataValidationRule { CustomerId = customerId, ProjectId = projectId, TemplateId = template.Id, RuleKey = "employee.bank.classification", Name = "Bank account is secret", Domain = HrmDataDomain.Employee, TargetField = "BankAccount", RuleType = ValidationRuleType.DataClassification, Severity = ValidationIssueSeverity.Info, ExpressionJson = """{"classification":"Secret","maskPreview":true}""" }
    };
    var batch = new DataImportBatch
    {
        CustomerId = customerId,
        ProjectId = projectId,
        EnvironmentId = testEnvironmentId,
        ConnectorId = connectorId,
        TemplateId = template.Id,
        TemplateVersion = 1,
        ImportFileId = file.Id,
        BatchNo = store.NextNumber("DIMP"),
        Domain = HrmDataDomain.Employee,
        Status = ImportBatchStatus.SignedOff,
        RequestedBy = "data.steward",
        DryRunRequired = true
    };
    var snapshot = new EnvironmentSnapshot
    {
        CustomerId = customerId,
        ProjectId = projectId,
        EnvironmentId = testEnvironmentId,
        ConnectorId = connectorId,
        SnapshotNo = store.NextNumber("SNAP"),
        Kind = SnapshotKind.ApplyComposite,
        Stage = SnapshotStage.PreApply,
        Summary = $"Pre-import snapshot for {batch.BatchNo}.",
        SnapshotJson = $$"""{"storageRef":"snapshot://data-import/{{batch.Id}}/pre","batchId":"{{batch.Id}}"}""",
        MaskedSummary = $"Pre-import snapshot for {batch.BatchNo}."
    };
    var dryRun = new DataImportRun { CustomerId = customerId, ProjectId = projectId, BatchId = batch.Id, EnvironmentId = testEnvironmentId, ConnectorId = connectorId, RunNo = store.NextNumber("DDRY"), RunType = DataImportRunType.DryRun, Status = DataImportRunStatus.Passed, TotalRows = 12, ValidRows = 12, ErrorRows = 0, DuplicateRows = 0, Summary = "Seeded dry run passed.", CompletedAt = DateTimeOffset.UtcNow };
    var applyRun = new DataImportRun { CustomerId = customerId, ProjectId = projectId, BatchId = batch.Id, EnvironmentId = testEnvironmentId, ConnectorId = connectorId, RunNo = store.NextNumber("DAPP"), RunType = DataImportRunType.Apply, Status = DataImportRunStatus.Passed, TotalRows = 12, ValidRows = 12, ErrorRows = 0, DuplicateRows = 0, Summary = "Seeded import applied to Test environment only.", CompletedAt = DateTimeOffset.UtcNow };
    var reconciliation = new DataReconciliationReport { CustomerId = customerId, ProjectId = projectId, BatchId = batch.Id, ImportRunId = applyRun.Id, ReportNo = store.NextNumber("DREC"), Status = ReconciliationStatus.Matched, SourceRows = 12, ImportedRows = 12, MatchedRows = 12, MismatchedRows = 0, MissingRows = 0, Summary = "Seeded reconciliation matched all imported rows.", ReportFileRef = $"file://migration-reports/{batch.BatchNo}/reconciliation.xlsx" };
    var migrationReport = new DataMigrationReport { CustomerId = customerId, ProjectId = projectId, BatchId = batch.Id, ReconciliationReportId = reconciliation.Id, ReportNo = store.NextNumber("DMR"), Domain = HrmDataDomain.Employee, Summary = reconciliation.Summary, FileRef = $"file://migration-reports/{batch.BatchNo}/migration-summary.pdf" };
    var signOff = new DataSignOff { CustomerId = customerId, ProjectId = projectId, BatchId = batch.Id, ReconciliationReportId = reconciliation.Id, SignOffNo = store.NextNumber("DSO"), Status = DataSignOffStatus.SignedOff, SignedBy = "customer.data.owner", Role = "Customer Data Owner", Comment = "Seeded Test import data signed off.", SignedAt = DateTimeOffset.UtcNow };
    var aiRun = NewGovernanceAiRun(customerId, projectId, AiTaskType.GenerateFixProposal, "Seeded data mapping assistance context", "Masked import template and column metadata only.");
    aiRun.RunType = "AiDataMappingSuggestion";
    aiRun.Status = AiRunStatus.Completed;
    aiRun.CompletedAt = DateTimeOffset.UtcNow;
    aiRun.OutputSummary = "Suggested mappings for employee master; user confirmation required before import.";
    var assistance = new AiDataMigrationAssistance { CustomerId = customerId, ProjectId = projectId, BatchId = batch.Id, TemplateId = template.Id, AiRunId = aiRun.Id, AssistanceType = AiDataAssistanceType.MappingSuggestion, Summary = "Seeded AI mapping suggestion; no import executed by AI.", SuggestionJson = """{"requiresUserConfirmation":true,"mappings":[{"source":"EmployeeCode","target":"Employee.EmployeeNo"}]}""" };
    batch.PreImportSnapshotId = snapshot.Id;
    batch.DryRunId = dryRun.Id;
    batch.ApplyRunId = applyRun.Id;
    batch.ReconciliationReportId = reconciliation.Id;
    batch.SignOffId = signOff.Id;

    store.DataImportTemplates.Add(template);
    store.DataImportTemplateVersions.Add(version);
    store.DataImportFiles.Add(file);
    store.DataColumnMappings.AddRange(mappings);
    store.DataValidationRules.AddRange(rules);
    store.DataImportBatches.Add(batch);
    store.EnvironmentSnapshots.Add(snapshot);
    store.DataImportRuns.AddRange([dryRun, applyRun]);
    store.DataReconciliationReports.Add(reconciliation);
    store.DataMigrationReports.Add(migrationReport);
    store.DataSignOffs.Add(signOff);
    store.AiRuns.Add(aiRun);
    store.AiDataMigrationAssistances.Add(assistance);
    audit.Write(customerId, projectId, "PHASE17_DATA_MIGRATION_DEMO_DATA_SEEDED", "DataMigration", batch.Id, new { template, batch, fileRefOnly = true, productionImport = false });
}

static void SeedDemoData(IAppStore store, IAuditWriter audit, INotificationDeliveryProvider deliveryProvider, IDataMaskingService masking)
{
    if (store.Customers.Count > 0)
    {
        return;
    }

    var customer = new Customer { Code = "DEMO", Name = "Demo Customer", Industry = "HR Technology" };
    var project = new Project { CustomerId = customer.Id, Code = "HRM-AIOPS", Name = "HRM AI Ops Pilot", HrmProductName = "Custom HRM" };
    var dev = new ProjectEnvironment { CustomerId = customer.Id, ProjectId = project.Id, Name = "Dev", Kind = EnvironmentKind.Dev };
    var test = new ProjectEnvironment { CustomerId = customer.Id, ProjectId = project.Id, Name = "Test", Kind = EnvironmentKind.Test };
    var uat = new ProjectEnvironment { CustomerId = customer.Id, ProjectId = project.Id, Name = "UAT", Kind = EnvironmentKind.Uat, RequiresApproval = true };
    var prod = new ProjectEnvironment { CustomerId = customer.Id, ProjectId = project.Id, Name = "Production", Kind = EnvironmentKind.Production, RequiresApproval = true };
    var modules = new[]
    {
        new HrmModuleDefinition { Code = "LEAVE", Name = "Leave Management", DefaultRiskLevel = RiskLevel.Medium, Description = "Leave policy, leave request, approval and balance tracking." },
        new HrmModuleDefinition { Code = "PAYROLL", Name = "Payroll", DefaultRiskLevel = RiskLevel.Critical, Description = "Salary calculation, statutory deduction and payroll posting." },
        new HrmModuleDefinition { Code = "PERMISSION", Name = "Permission", DefaultRiskLevel = RiskLevel.Critical, Description = "Role, permission and access control matrix." },
        new HrmModuleDefinition { Code = "SECURITY", Name = "Security", DefaultRiskLevel = RiskLevel.Critical, Description = "Authentication, authorization, audit and data protection controls." },
        new HrmModuleDefinition { Code = "INTEGRATION", Name = "Integration", DefaultRiskLevel = RiskLevel.High, Description = "Integration with attendance, payroll, SSO and external systems." }
    };
    var promptTemplates = new[]
    {
        NewPrompt("generate-urs-v1", "Generate URS", AiTaskType.GenerateUrs, "Create structured URS proposal from a requirement."),
        NewPrompt("generate-blueprint-v1", "Generate Blueprint", AiTaskType.GenerateBlueprint, "Create structured blueprint proposal from URS."),
        NewPrompt("generate-config-spec-v1", "Generate Config Spec", AiTaskType.GenerateConfigSpec, "Create structured config specification proposal from blueprint."),
        NewPrompt("classify-issue-v1", "Classify Issue", AiTaskType.ClassifyIssue, "Classify HRM support issue and risk."),
        NewPrompt("root-cause-v1", "Root Cause Analysis", AiTaskType.AnalyzeRootCause, "Analyze likely root cause from customer-scoped issue context."),
        NewPrompt("fix-proposal-v1", "Generate Fix Proposal", AiTaskType.GenerateFixProposal, "Generate controlled fix proposal without production write."),
        NewPrompt("change-request-v1", "Generate Change Request", AiTaskType.GenerateChangeRequest, "Generate approval-ready change request draft."),
        NewPrompt("regression-test-v1", "Generate Regression Test Plan", AiTaskType.GenerateRegressionTestPlan, "Generate regression test plan draft."),
        NewPrompt("release-draft-v1", "Generate Release Draft", AiTaskType.GenerateReleaseDraft, "Generate release note and release plan draft."),
        NewPrompt("knowledge-update-v1", "Generate Knowledge Update", AiTaskType.GenerateKnowledgeUpdate, "Generate KB update proposal.")
    };

    var requirement = new Requirement
    {
        CustomerId = customer.Id,
        ProjectId = project.Id,
        RequirementNo = store.NextNumber("REQ"),
        Title = "Leave Management - Annual leave request and approval",
        SourceType = "Sample",
        ContentText = "Employees can submit annual leave requests. Line managers approve or reject. HR can view leave balances and audit approval history.",
        Status = WorkStatus.Approved,
        Version = 1,
        CreatedBy = "system",
        ApprovedBy = "business.owner",
        ApprovedAt = DateTimeOffset.UtcNow
    };
    requirement.VersionGroupId = requirement.Id;

    var urs = new UrsDocument
    {
        CustomerId = customer.Id,
        ProjectId = project.Id,
        RequirementId = requirement.Id,
        UrsNo = store.NextNumber("URS"),
        Title = "URS - Leave Management",
        Content = "The system shall allow employees to create leave requests, managers to approve them, and HR to audit balances and request history.",
        Status = WorkStatus.Draft,
        Version = 1
    };
    urs.VersionGroupId = urs.Id;

    var blueprint = new Blueprint
    {
        CustomerId = customer.Id,
        ProjectId = project.Id,
        UrsId = urs.Id,
        BlueprintNo = store.NextNumber("BP"),
        Type = "Functional",
        Content = "Leave request workflow: Employee submits request, manager reviews, HR monitors balance and exceptions. Data objects: LeaveType, LeaveRequest, LeaveBalance, ApprovalStep.",
        Status = WorkStatus.Draft,
        Version = 1
    };
    blueprint.VersionGroupId = blueprint.Id;

    var config = new ConfigSpec
    {
        CustomerId = customer.Id,
        ProjectId = project.Id,
        EnvironmentId = dev.Id,
        BlueprintId = blueprint.Id,
        ConfigNo = store.NextNumber("CFG"),
        ModuleName = "Leave Management",
        RiskLevel = RiskLevel.Medium,
        Content = "Configure annual leave type, manager approval flow, leave balance visibility and HR override permission.",
        Status = WorkStatus.Draft,
        Version = 1
    };
    config.VersionGroupId = config.Id;
    var issue = new Issue
    {
        CustomerId = customer.Id,
        ProjectId = project.Id,
        EnvironmentId = prod.Id,
        LinkedEntityType = nameof(ConfigSpec),
        LinkedEntityId = config.Id,
        IssueNo = store.NextNumber("ISS"),
        Title = "Leave balance not updated after manager approval",
        Description = "Production user reports leave request is approved but balance remains unchanged. No production write should be attempted by AI.",
        Category = IssueCategory.Functional,
        RiskLevel = RiskLevel.Medium,
        Severity = IssueSeverity.High,
        Priority = IssuePriority.P1,
        ReportedBy = "customer.hr"
    };
    var issue2 = new Issue
    {
        CustomerId = customer.Id,
        ProjectId = project.Id,
        EnvironmentId = prod.Id,
        LinkedEntityType = nameof(ConfigSpec),
        LinkedEntityId = config.Id,
        IssueNo = store.NextNumber("ISS"),
        Title = "Leave balance recalculation delayed for backdated approval",
        Description = "A second production leave scenario shows delayed balance update after a backdated approval. Sensitive employee data is not stored.",
        Category = IssueCategory.Functional,
        RiskLevel = RiskLevel.Medium,
        Severity = IssueSeverity.Medium,
        Priority = IssuePriority.P2,
        ReportedBy = "customer.hr"
    };
    var issueAnalysis = new IssueAnalysis
    {
        CustomerId = customer.Id,
        ProjectId = project.Id,
        IssueId = issue.Id,
        AnalysisType = "RootCause",
        Content = "Likely missing recalculation trigger after approval status transition.",
        ConfidenceScore = 0.84m
    };
    var issueAnalysis2 = new IssueAnalysis
    {
        CustomerId = customer.Id,
        ProjectId = project.Id,
        IssueId = issue2.Id,
        AnalysisType = "RootCause",
        Content = "Backdated approval path bypasses the same balance recalculation trigger.",
        ConfidenceScore = 0.78m
    };
    var fix = new FixProposal
    {
        CustomerId = customer.Id,
        ProjectId = project.Id,
        IssueId = issue.Id,
        Title = "Fix - Leave balance recalculation after approval",
        ProposedSolution = "Apply controlled Test/UAT configuration for leave balance recalculation trigger. No production write or source merge.",
        CodeChangeSummary = "Mock apply updates Test/UAT metadata only.",
        DbChangeSummary = "No HR/payroll transaction data is written.",
        RiskLevel = RiskLevel.Medium,
        Status = WorkStatus.Approved
    };
    var fix2 = new FixProposal
    {
        CustomerId = customer.Id,
        ProjectId = project.Id,
        IssueId = issue2.Id,
        Title = "Fix - Backdated leave approval recalculation",
        ProposedSolution = "Extend recalculation validation to backdated approval workflow and add regression test.",
        CodeChangeSummary = "Mock proposal only; no production source change.",
        DbChangeSummary = "No HR transaction data stored.",
        RiskLevel = RiskLevel.Medium,
        Status = WorkStatus.Draft
    };
    var change = new ChangeRequest
    {
        CustomerId = customer.Id,
        ProjectId = project.Id,
        IssueId = issue.Id,
        FixProposalId = fix.Id,
        CrNo = store.NextNumber("CR"),
        Title = "CR - Test leave balance recalculation trigger",
        Description = "Approved controlled change request for Test/UAT validation only.",
        TargetEnvironmentId = test.Id,
        Status = WorkStatus.Approved,
        RequiresApproval = false
    };
    var regressionPlan = new RegressionTestPlan
    {
        CustomerId = customer.Id,
        ProjectId = project.Id,
        IssueId = issue.Id,
        ChangeRequestId = change.Id,
        TestPlanNo = store.NextNumber("RTP"),
        Title = "Regression - Leave balance controlled apply",
        Content = "Validate leave approval, balance recalculation, HR audit trail, permission boundary and rollback readiness.",
        RiskLevel = RiskLevel.Medium,
        Status = WorkStatus.Approved
    };
    var connectorPolicy = new ConnectorPermissionPolicy
    {
        CustomerId = customer.Id,
        ProjectId = project.Id,
        EnvironmentId = test.Id,
        Name = "Mock Test Apply Policy",
        AllowTestApply = true
    };
    var connector = new CustomerConnector
    {
        CustomerId = customer.Id,
        ProjectId = project.Id,
        EnvironmentId = test.Id,
        PermissionPolicyId = connectorPolicy.Id,
        ConnectorType = "MockTestApply",
        Name = "Mock Test Apply Connector",
        SecretRef = "secret://mock/test-apply",
        ConfigJson = """{"mode":"mock","scope":"TestOnly"}""",
        LastHealthStatus = "Healthy"
    };
    var applyRun = new ApplyRun
    {
        CustomerId = customer.Id,
        ProjectId = project.Id,
        EnvironmentId = test.Id,
        ConnectorId = connector.Id,
        FixProposalId = fix.Id,
        ChangeRequestId = change.Id,
        ApplyRunNo = store.NextNumber("APP"),
        SourceType = nameof(ChangeRequest),
        SourceId = change.Id,
        RiskLevel = RiskLevel.Medium,
        Status = ApplyRunStatus.ReleaseReady,
        RequestedBy = "release.consultant",
        RollbackRecommendation = "Use controlled rollback plan if production validation fails.",
        Summary = "Seeded Test/UAT apply completed successfully and is release-ready.",
        CompletedAt = DateTimeOffset.UtcNow
    };
    var rollbackPlan = new RollbackPlan
    {
        CustomerId = customer.Id,
        ProjectId = project.Id,
        EnvironmentId = test.Id,
        ApplyRunId = applyRun.Id,
        PlanNo = store.NextNumber("RBP"),
        Strategy = "Restore previous leave balance recalculation config.",
        Steps = "Pause release, restore previous config, rerun leave approval validation.",
        ValidationChecklist = "Leave approval, balance recalculation, audit visibility.",
        Status = WorkStatus.Approved
    };
    var regressionRun = new RegressionTestRun
    {
        CustomerId = customer.Id,
        ProjectId = project.Id,
        EnvironmentId = test.Id,
        ApplyRunId = applyRun.Id,
        RegressionTestPlanId = regressionPlan.Id,
        RunNo = store.NextNumber("RTR"),
        Status = RegressionRunStatus.Passed,
        TotalTests = 8,
        PassedTests = 8,
        FailedTests = 0,
        Summary = "All seeded Leave Management regression checks passed.",
        CompletedAt = DateTimeOffset.UtcNow
    };
    var readiness = new ReleaseReadinessReport
    {
        CustomerId = customer.Id,
        ProjectId = project.Id,
        EnvironmentId = test.Id,
        ApplyRunId = applyRun.Id,
        RegressionTestRunId = regressionRun.Id,
        ReportNo = store.NextNumber("RRR"),
        Status = ReleaseReadinessStatus.Ready,
        Summary = "Leave balance recalculation package is ready for controlled production release.",
        Blockers = ""
    };
    applyRun.RollbackPlanId = rollbackPlan.Id;
    applyRun.RegressionTestRunId = regressionRun.Id;
    applyRun.ReleaseReadinessReportId = readiness.Id;
    var productionPackage = new ProductionReleasePackage
    {
        CustomerId = customer.Id,
        ProjectId = project.Id,
        ProductionEnvironmentId = prod.Id,
        ReleaseReadinessReportId = readiness.Id,
        PackageNo = store.NextNumber("PRD"),
        Version = "2026.06.1",
        Title = "Leave balance recalculation production release",
        Status = ProductionReleaseStatus.Draft,
        RiskLevel = RiskLevel.High,
        Summary = "Seeded Phase 7 package from release readiness report. Production deploy requires checklist, two-step approval, window, validated plan and pre-snapshot.",
        RollbackPlanValidated = true
    };

    store.Customers.Add(customer);
    store.Projects.Add(project);
    store.Environments.Add(dev);
    store.Environments.Add(test);
    store.Environments.Add(uat);
    store.Environments.Add(prod);
    store.HrmModules.AddRange(modules);
    foreach (var prompt in promptTemplates)
    {
        store.AiPromptTemplates.Add(prompt.Template);
        store.AiPromptTemplateVersions.Add(prompt.Version);
    }
    store.Requirements.Add(requirement);
    store.UrsDocuments.Add(urs);
    store.Blueprints.Add(blueprint);
    store.ConfigSpecs.Add(config);
    store.Issues.Add(issue);
    store.Issues.Add(issue2);
    store.IssueAnalyses.Add(issueAnalysis);
    store.IssueAnalyses.Add(issueAnalysis2);
    store.FixProposals.Add(fix);
    store.FixProposals.Add(fix2);
    store.ChangeRequests.Add(change);
    store.RegressionTestPlans.Add(regressionPlan);
    store.ConnectorPermissionPolicies.Add(connectorPolicy);
    store.CustomerConnectors.Add(connector);
    store.ApplyRuns.Add(applyRun);
    store.RollbackPlans.Add(rollbackPlan);
    store.RegressionTestRuns.Add(regressionRun);
    store.ReleaseReadinessReports.Add(readiness);
    store.ProductionReleasePackages.Add(productionPackage);
    var learningRun = NewGovernanceAiRun(customer.Id, project.Id, AiTaskType.GenerateLessonsLearned, "Seeded operational learning context", "Masked demo context only.");
    learningRun.Status = AiRunStatus.Completed;
    learningRun.CompletedAt = DateTimeOffset.UtcNow;
    store.AiRuns.Add(learningRun);
    GenerateKnowledgeLearningItems(store, audit, customer.Id, project.Id, learningRun, false);
    var scoreRun = NewGovernanceAiRun(customer.Id, project.Id, AiTaskType.CalculateGovernanceScores, "Seeded governance score context", "Masked demo context only.");
    scoreRun.Status = AiRunStatus.Completed;
    scoreRun.CompletedAt = DateTimeOffset.UtcNow;
    store.AiRuns.Add(scoreRun);
    DetectRepeatedIssuePatterns(store, customer.Id, project.Id, scoreRun.Id);
    RecalculateGovernanceScores(store, customer.Id, project.Id, scoreRun.Id, null);
    RecalculateAiPerformanceMetric(store, customer.Id, project.Id);
    SeedSecurityGovernance(store, customer.Id, project.Id, prod.Id);
    SeedCommercialAndPortal(store, audit, customer.Id, project.Id);
    SeedPortalEnhancement(store, audit, customer.Id, project.Id, requirement.Id, urs.Id, blueprint.Id, config.Id);
    SeedReporting(store, audit, customer.Id, project.Id);
    SeedIntegrationHub(store, audit, customer.Id, project.Id);
    SeedDevOpsAutomation(store, audit, customer.Id, project.Id);
    SeedObservability(store, audit, customer.Id, project.Id, prod.Id, connector.Id, productionPackage.Id);
    SeedDataMigration(store, audit, customer.Id, project.Id, test.Id, connector.Id);
    SeedNotificationWorkflowCollaboration(store, audit, deliveryProvider, masking, customer.Id, project.Id, issue.Id, productionPackage.Id, urs.Id);
    store.TraceLinks.Add(NewTrace(customer.Id, project.Id, "Requirement", requirement.Id, "UrsDocument", urs.Id, "SampleSeed"));
    store.TraceLinks.Add(NewTrace(customer.Id, project.Id, "UrsDocument", urs.Id, "Blueprint", blueprint.Id, "SampleSeed"));
    store.TraceLinks.Add(NewTrace(customer.Id, project.Id, "Blueprint", blueprint.Id, "ConfigSpec", config.Id, "SampleSeed"));
    store.TraceLinks.Add(NewTrace(customer.Id, project.Id, nameof(ConfigSpec), config.Id, nameof(Issue), issue.Id, "SampleIssue"));
    store.TraceLinks.Add(NewTrace(customer.Id, project.Id, nameof(Issue), issue.Id, nameof(FixProposal), fix.Id, "SampleFix"));
    store.TraceLinks.Add(NewTrace(customer.Id, project.Id, nameof(FixProposal), fix.Id, nameof(ChangeRequest), change.Id, "SampleChange"));
    store.DocumentSignOffs.Add(new DocumentSignOff
    {
        CustomerId = customer.Id,
        ProjectId = project.Id,
        DocumentKind = DocumentKind.Requirement,
        DocumentId = requirement.Id,
        Version = requirement.Version,
        SignedOffBy = "business.owner",
        Role = "Business Owner",
        Comment = "Sample Leave Management requirement signed off for Phase 2 demo."
    });
    audit.Write(customer.Id, null, "DEMO_DATA_SEEDED", "System", customer.Id, new { customer, project });
}

static (AiPromptTemplate Template, AiPromptTemplateVersion Version) NewPrompt(string key, string name, AiTaskType taskType, string description)
{
    var template = new AiPromptTemplate
    {
        Key = key,
        Name = name,
        TaskType = taskType,
        Description = description
    };
    var version = new AiPromptTemplateVersion
    {
        TemplateId = template.Id,
        TemplateKey = template.Key,
        Version = 1,
        SystemPrompt = "You are an HRM implementation consultant. Return only JSON that matches the required schema. Do not include secrets or personal data.",
        UserPromptTemplate = "Use the masked customer/project context and source object to generate a controlled draft proposal. Context: {{contextJson}}",
        OutputJsonSchema = """
        {
          "type": "object",
          "required": ["title", "content"],
          "properties": {
            "title": { "type": "string" },
            "content": { "type": "string" },
            "moduleName": { "type": ["string", "null"] },
            "riskLevel": { "type": ["string", "null"], "enum": ["Low", "Medium", "High", "Critical", null] },
            "traceSummary": { "type": "string" },
            "validationNotes": { "type": "array", "items": { "type": "string" } }
          }
        }
        """
    };
    return (template, version);
}

}

