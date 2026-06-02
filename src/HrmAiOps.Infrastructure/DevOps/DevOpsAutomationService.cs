using System.Text;
using System.Text.Json;
using HrmAiOps.Application.Abstractions;
using HrmAiOps.Domain.Core;

namespace HrmAiOps.Infrastructure.DevOps;

public sealed class DevOpsAutomationService(
    IAppStore store,
    IAuditWriter audit,
    IAiRunRecorder aiRuns,
    IDataMaskingService masking,
    IBackgroundJobDispatcher jobs) : IDevOpsAutomationService
{
    private const int DefaultMaxDiffBytes = 12000;
    private static readonly string[] ProtectedBranches = ["main", "master"];
    private static readonly string[] SpecialAreas = ["Payroll", "Permission", "Security", "Integration", "ProductionDeployment"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public object GetDashboard(DevOpsDashboardFilter filter)
    {
        var repos = store.DevOpsRepositories.Where(x => x.CustomerId == filter.CustomerId && x.ProjectId == filter.ProjectId).ToList();
        var repoIds = repos.Select(x => x.Id).ToHashSet();
        var prs = store.DevOpsPullRequests.Where(x => x.CustomerId == filter.CustomerId && x.ProjectId == filter.ProjectId && repoIds.Contains(x.RepositoryId)).ToList();
        var pipelineRuns = store.PipelineRuns.Where(x => x.CustomerId == filter.CustomerId && x.ProjectId == filter.ProjectId && repoIds.Contains(x.RepositoryId)).ToList();
        var packages = store.DeploymentPackages.Where(x => x.CustomerId == filter.CustomerId && x.ProjectId == filter.ProjectId && repoIds.Contains(x.RepositoryId)).ToList();
        var runs = store.DevOpsRuns.Where(x => x.CustomerId == filter.CustomerId && x.ProjectId == filter.ProjectId).OrderByDescending(x => x.StartedAt).ToList();
        return new
        {
            filter,
            repositories = repos.Count,
            openPullRequests = prs.Count(x => x.Status is PullRequestStatus.Open or PullRequestStatus.ReviewRequired or PullRequestStatus.Approved),
            blockedPullRequests = prs.Count(x => x.Status == PullRequestStatus.Blocked),
            pipelines = store.CiCdPipelines.Count(x => x.CustomerId == filter.CustomerId && x.ProjectId == filter.ProjectId && repoIds.Contains(x.RepositoryId) && x.Active),
            successfulPipelineRuns = pipelineRuns.Count(x => x.Status == PipelineRunStatus.Succeeded),
            failedPipelineRuns = pipelineRuns.Count(x => x.Status is PipelineRunStatus.Failed or PipelineRunStatus.Blocked),
            readyPackages = packages.Count(x => x.Status == DeploymentPackageStatus.Ready),
            blockedPackages = packages.Count(x => x.Status is DeploymentPackageStatus.Blocked or DeploymentPackageStatus.ScanBlocked),
            aiCodeAnalyses = store.AiCodeAnalyses.Count(x => x.CustomerId == filter.CustomerId && x.ProjectId == filter.ProjectId),
            latestPullRequests = prs.OrderByDescending(x => x.CreatedAt).Take(8),
            latestPipelineRuns = pipelineRuns.OrderByDescending(x => x.StartedAt).Take(8),
            latestRuns = runs.Take(10)
        };
    }

    public DevOpsRepository RegisterRepository(DevOpsRepositoryRegistrationRequest request)
    {
        ValidateSecretRef(request.SecretRef);
        var repo = new DevOpsRepository
        {
            CustomerId = request.CustomerId,
            ProjectId = request.ProjectId,
            Provider = request.Provider,
            Name = request.Name.Trim(),
            ProviderRepositoryId = request.ProviderRepositoryId.Trim(),
            RepoUrl = request.RepoUrl.Trim(),
            DefaultBranch = string.IsNullOrWhiteSpace(request.DefaultBranch) ? "main" : request.DefaultBranch.Trim(),
            SecretRef = request.SecretRef.Trim()
        };
        store.DevOpsRepositories.Add(repo);
        var run = StartRun(request.CustomerId, request.ProjectId, repo.Id, null, null, DevOpsRunType.RepositorySync, request.RequestedBy, $"Register {repo.Provider}/{repo.Name}");
        CompleteRun(run, DevOpsRunStatus.Succeeded, "Repository registered with secret_ref-only credential metadata.");
        audit.Write(request.CustomerId, request.ProjectId, "DEVOPS_REPOSITORY_REGISTERED", nameof(DevOpsRepository), repo.Id, repo);
        return repo;
    }

    public DevOpsBranch CreateBranch(DevOpsBranchRequest request)
    {
        var repo = ResolveRepository(request.CustomerId, request.ProjectId, request.RepositoryId);
        if (request.CreatedByAi && IsProtectedBranch(request.BranchName))
        {
            throw new InvalidOperationException("AI cannot create or write directly to protected main/master branches.");
        }

        var branch = new DevOpsBranch
        {
            CustomerId = request.CustomerId,
            ProjectId = request.ProjectId,
            RepositoryId = repo.Id,
            BranchName = request.BranchName.Trim(),
            SourceBranch = string.IsNullOrWhiteSpace(request.SourceBranch) ? repo.DefaultBranch : request.SourceBranch.Trim(),
            CreatedByAi = request.CreatedByAi,
            CreatedBy = request.RequestedBy
        };
        store.DevOpsBranches.Add(branch);
        var run = StartRun(request.CustomerId, request.ProjectId, repo.Id, null, null, DevOpsRunType.BranchCreate, request.RequestedBy, $"{branch.SourceBranch} -> {branch.BranchName}");
        CompleteRun(run, DevOpsRunStatus.Succeeded, "Mock Git branch created.");
        audit.Write(request.CustomerId, request.ProjectId, "DEVOPS_BRANCH_CREATED", nameof(DevOpsBranch), branch.Id, branch);
        return branch;
    }

    public DevOpsPullRequest CreatePullRequest(DevOpsPullRequestCreateRequest request)
    {
        var repo = ResolveRepository(request.CustomerId, request.ProjectId, request.RepositoryId);
        if (request.CreatedByAi && IsProtectedBranch(request.SourceBranch))
        {
            throw new InvalidOperationException("AI cannot create a pull request from a protected branch.");
        }

        var requiresReview = repo.RequirePullRequestReview || request.RiskLevel is RiskLevel.High or RiskLevel.Critical || HasSpecialArea(request.ChangeAreasCsv);
        var pr = new DevOpsPullRequest
        {
            CustomerId = request.CustomerId,
            ProjectId = request.ProjectId,
            RepositoryId = repo.Id,
            ExternalPrRef = $"mock-pr-{store.DevOpsPullRequests.Count(x => x.RepositoryId == repo.Id) + 1}",
            SourceBranch = request.SourceBranch.Trim(),
            TargetBranch = string.IsNullOrWhiteSpace(request.TargetBranch) ? repo.DefaultBranch : request.TargetBranch.Trim(),
            Title = request.Title.Trim(),
            Description = request.Description,
            RiskLevel = request.RiskLevel,
            ChangeAreasCsv = NormalizeAreas(request.ChangeAreasCsv),
            Status = requiresReview ? PullRequestStatus.ReviewRequired : PullRequestStatus.Open,
            CreatedBy = request.RequestedBy,
            CreatedByAi = request.CreatedByAi
        };
        store.DevOpsPullRequests.Add(pr);
        store.SourceCodeSnapshots.Add(NewSnapshot(request.CustomerId, request.ProjectId, repo.Id, pr.SourceBranch, "PR metadata captured; full source is not stored.", request.Description));
        var run = StartRun(request.CustomerId, request.ProjectId, repo.Id, pr.Id, null, DevOpsRunType.PullRequestCreate, request.RequestedBy, $"{pr.SourceBranch} -> {pr.TargetBranch}");
        CompleteRun(run, DevOpsRunStatus.Succeeded, "Mock pull request created.");
        audit.Write(request.CustomerId, request.ProjectId, "DEVOPS_PULL_REQUEST_CREATED", nameof(DevOpsPullRequest), pr.Id, pr);
        return pr;
    }

    public async Task<AiCodeAnalysis> AnalyzeCodeAsync(AiCodeAnalysisRequest request, CancellationToken cancellationToken)
    {
        var repo = ResolveRepository(request.CustomerId, request.ProjectId, request.RepositoryId);
        var pr = request.PullRequestId.HasValue ? ResolvePullRequest(request.CustomerId, request.ProjectId, request.PullRequestId.Value) : null;
        EnsureRepoMatch(repo, pr);
        var maskedDiff = Limit(masking.Mask(request.DiffText), ResolvePolicy(request.CustomerId, request.ProjectId, repo.Id).MaxDiffBytes);
        var aiRun = aiRuns.Start(request.CustomerId, request.ProjectId, "AiCodeAnalysis", $"Analyze repository {repo.Name} branch {request.BranchName}", $"devops_repository:{repo.Id}", null);
        aiRun.MaskedInputPreview = Limit(maskedDiff, 700);
        await jobs.EnqueueAsync("devops.ai_code_analysis", new { request.CustomerId, request.ProjectId, repo.Id, request.PullRequestId }, cancellationToken);

        var areas = DetectAreas(maskedDiff);
        var risk = DetectRisk(maskedDiff, areas);
        var findings = areas.Select(area => new { area, severity = risk.ToString(), message = $"{area} code path requires governance review." }).ToArray();
        var analysis = new AiCodeAnalysis
        {
            CustomerId = request.CustomerId,
            ProjectId = request.ProjectId,
            RepositoryId = repo.Id,
            PullRequestId = pr?.Id,
            AiRunId = aiRun.Id,
            BranchName = request.BranchName,
            RiskLevel = risk,
            ChangeAreasCsv = string.Join(",", areas),
            Summary = $"AI code analysis completed. Risk={risk}; Areas={string.Join(",", areas)}.",
            FindingsJson = JsonSerializer.Serialize(findings, JsonOptions)
        };
        store.AiCodeAnalyses.Add(analysis);
        if (pr is not null)
        {
            pr.AiRunId = aiRun.Id;
            pr.RiskLevel = MaxRisk(pr.RiskLevel, risk);
            pr.ChangeAreasCsv = MergeAreas(pr.ChangeAreasCsv, analysis.ChangeAreasCsv);
            if (risk is RiskLevel.High or RiskLevel.Critical)
            {
                pr.Status = PullRequestStatus.ReviewRequired;
            }
        }
        aiRuns.Complete(aiRun, analysis.Summary, $"ai_code_analysis:{analysis.Id}", analysis.FindingsJson, []);
        var run = StartRun(request.CustomerId, request.ProjectId, repo.Id, pr?.Id, null, DevOpsRunType.AiCodeAnalysis, request.RequestedBy, maskedDiff);
        CompleteRun(run, DevOpsRunStatus.Succeeded, analysis.Summary);
        audit.Write(request.CustomerId, request.ProjectId, "DEVOPS_AI_CODE_ANALYSIS_CREATED", nameof(AiCodeAnalysis), analysis.Id, analysis);
        return analysis;
    }

    public async Task<AiPatchProposal> ProposePatchAsync(AiPatchProposalRequest request, CancellationToken cancellationToken)
    {
        var repo = ResolveRepository(request.CustomerId, request.ProjectId, request.RepositoryId);
        var pr = request.PullRequestId.HasValue ? ResolvePullRequest(request.CustomerId, request.ProjectId, request.PullRequestId.Value) : null;
        EnsureRepoMatch(repo, pr);
        if (request.BranchName.Equals(repo.DefaultBranch, StringComparison.OrdinalIgnoreCase) || IsProtectedBranch(request.BranchName))
        {
            throw new InvalidOperationException("AI patch proposal must target a feature branch, not main/master.");
        }

        var policy = ResolvePolicy(request.CustomerId, request.ProjectId, repo.Id);
        var maskedIntent = masking.Mask(request.Intent);
        var areas = DetectAreas(maskedIntent);
        var risk = DetectRisk(maskedIntent, areas);
        var aiRun = aiRuns.Start(request.CustomerId, request.ProjectId, "AiPatchProposal", $"Propose patch for {repo.Name}/{request.BranchName}", $"devops_repository:{repo.Id}", null);
        aiRun.MaskedInputPreview = Limit(maskedIntent, 700);
        await jobs.EnqueueAsync("devops.ai_patch_proposal", new { request.CustomerId, request.ProjectId, repo.Id, request.PullRequestId }, cancellationToken);

        var diff = BuildMockDiff(request.BranchName, maskedIntent, areas);
        var limitedDiff = Limit(masking.Mask(diff), policy.MaxDiffBytes);
        var patch = new AiPatchProposal
        {
            CustomerId = request.CustomerId,
            ProjectId = request.ProjectId,
            RepositoryId = repo.Id,
            PullRequestId = pr?.Id,
            AiRunId = aiRun.Id,
            BranchName = request.BranchName,
            Title = $"AI patch proposal for {repo.Name}",
            DiffText = limitedDiff,
            DiffSizeBytes = Encoding.UTF8.GetByteCount(limitedDiff),
            RiskLevel = risk,
            ChangeAreasCsv = string.Join(",", areas)
        };
        store.AiPatchProposals.Add(patch);
        if (pr is not null)
        {
            pr.AiRunId = aiRun.Id;
            pr.RiskLevel = MaxRisk(pr.RiskLevel, risk);
            pr.ChangeAreasCsv = MergeAreas(pr.ChangeAreasCsv, patch.ChangeAreasCsv);
        }
        aiRuns.Complete(aiRun, $"Patch proposed. Risk={risk}; diffBytes={patch.DiffSizeBytes}.", $"ai_patch_proposal:{patch.Id}");
        var run = StartRun(request.CustomerId, request.ProjectId, repo.Id, pr?.Id, null, DevOpsRunType.AiPatchProposal, request.RequestedBy, limitedDiff);
        CompleteRun(run, DevOpsRunStatus.Succeeded, "AI patch proposal created. Diff is size-limited and masked.");
        audit.Write(request.CustomerId, request.ProjectId, "DEVOPS_AI_PATCH_PROPOSED", nameof(AiPatchProposal), patch.Id, patch);
        return patch;
    }

    public CodeReviewRecord AddReview(Guid customerId, Guid projectId, Guid pullRequestId, string reviewerUserId, CodeReviewDecision decision, string comments)
    {
        var pr = ResolvePullRequest(customerId, projectId, pullRequestId);
        var review = new CodeReviewRecord
        {
            CustomerId = customerId,
            ProjectId = projectId,
            PullRequestId = pr.Id,
            ReviewerUserId = reviewerUserId,
            Decision = decision,
            Comments = masking.Mask(comments),
            RiskLevel = pr.RiskLevel,
            RequiresSpecialApproval = pr.RiskLevel is RiskLevel.High or RiskLevel.Critical || HasSpecialArea(pr.ChangeAreasCsv)
        };
        store.CodeReviewRecords.Add(review);
        pr.Status = decision switch
        {
            CodeReviewDecision.Approved => PullRequestStatus.Approved,
            CodeReviewDecision.ChangesRequested => PullRequestStatus.ChangesRequested,
            CodeReviewDecision.Rejected => PullRequestStatus.Rejected,
            _ => PullRequestStatus.ReviewRequired
        };
        var run = StartRun(customerId, projectId, pr.RepositoryId, pr.Id, null, DevOpsRunType.CodeReview, reviewerUserId, comments);
        CompleteRun(run, DevOpsRunStatus.Succeeded, $"Review decision: {decision}.");
        audit.Write(customerId, projectId, "DEVOPS_CODE_REVIEW_RECORDED", nameof(CodeReviewRecord), review.Id, review);
        return review;
    }

    public ApprovalRequest SubmitApproval(Guid customerId, Guid projectId, Guid pullRequestId, string requestedBy, string approverUserId)
    {
        var pr = ResolvePullRequest(customerId, projectId, pullRequestId);
        var approval = new ApprovalRequest
        {
            CustomerId = customerId,
            ProjectId = projectId,
            EntityType = nameof(DevOpsPullRequest),
            EntityId = pr.Id,
            RequestedBy = requestedBy,
            Status = ApprovalStatus.Pending
        };
        store.ApprovalRequests.Add(approval);
        store.ApprovalSteps.Add(new ApprovalStep
        {
            CustomerId = customerId,
            ApprovalRequestId = approval.Id,
            StepOrder = 1,
            ApproverUserId = approverUserId,
            Status = ApprovalStatus.Pending
        });
        pr.ApprovalRequestId = approval.Id;
        var run = StartRun(customerId, projectId, pr.RepositoryId, pr.Id, null, DevOpsRunType.CodeReview, requestedBy, "Special code approval submitted.");
        CompleteRun(run, DevOpsRunStatus.RequiresApproval, "High/special-risk PR requires approval before merge/package.");
        audit.Write(customerId, projectId, "DEVOPS_APPROVAL_SUBMITTED", nameof(ApprovalRequest), approval.Id, approval);
        return approval;
    }

    public async Task<PipelineRun> RunPipelineAsync(PipelineRunRequest request, CancellationToken cancellationToken)
    {
        var repo = ResolveRepository(request.CustomerId, request.ProjectId, request.RepositoryId);
        var pipeline = store.CiCdPipelines.SingleOrDefault(x => x.CustomerId == request.CustomerId && x.ProjectId == request.ProjectId && x.RepositoryId == repo.Id && x.Id == request.PipelineId && x.Active)
            ?? throw new InvalidOperationException("Pipeline not found.");
        if (pipeline.TimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("CI/CD pipeline timeout must be greater than zero.");
        }

        var pr = request.PullRequestId.HasValue ? ResolvePullRequest(request.CustomerId, request.ProjectId, request.PullRequestId.Value) : null;
        EnsureRepoMatch(repo, pr);
        var run = new PipelineRun
        {
            CustomerId = request.CustomerId,
            ProjectId = request.ProjectId,
            RepositoryId = repo.Id,
            PipelineId = pipeline.Id,
            PullRequestId = pr?.Id,
            RunType = request.RunType,
            Status = PipelineRunStatus.Running,
            StartedAt = DateTimeOffset.UtcNow
        };
        store.PipelineRuns.Add(run);
        await jobs.EnqueueAsync($"devops.pipeline.{request.RunType.ToString().ToLowerInvariant()}", new { run.Id, request.CustomerId, request.ProjectId }, cancellationToken);

        var maskedInput = masking.Mask(request.InputJson);
        var fail = ContainsAny(maskedInput, "forceFailure", "fail:true", "simulateFail");
        var highRiskFinding = request.RunType == PipelineRunType.CodeScan && (ContainsAny(maskedInput, "highRiskFinding", "criticalFinding") || pr?.RiskLevel == RiskLevel.Critical);
        if (fail || highRiskFinding)
        {
            run.Status = request.RunType == PipelineRunType.CodeScan && highRiskFinding ? PipelineRunStatus.Blocked : PipelineRunStatus.Failed;
            run.ErrorMessage = highRiskFinding ? "Mock code scan found high-risk code findings." : "Mock CI provider failed this run.";
            run.Summary = run.ErrorMessage;
        }
        else
        {
            run.Status = PipelineRunStatus.Succeeded;
            run.Summary = $"Mock {request.RunType} completed successfully.";
            run.ArtifactRef = request.RunType == PipelineRunType.Build ? $"artifact://devops/{repo.Id}/{run.Id}.zip" : null;
            run.LogsRef = $"log://devops/{run.Id}";
        }
        run.CompletedAt = DateTimeOffset.UtcNow;

        if (pr is not null)
        {
            if (request.RunType == PipelineRunType.Build) pr.BuildRunId = run.Id;
            if (request.RunType == PipelineRunType.Test) pr.TestRunId = run.Id;
            if (request.RunType == PipelineRunType.CodeScan) pr.CodeScanRunId = run.Id;
            if (run.Status == PipelineRunStatus.Blocked)
            {
                pr.Status = PullRequestStatus.Blocked;
            }
        }

        var devOpsRun = StartRun(request.CustomerId, request.ProjectId, repo.Id, pr?.Id, run.Id, request.RunType switch
        {
            PipelineRunType.Build => DevOpsRunType.Build,
            PipelineRunType.Test => DevOpsRunType.Test,
            _ => DevOpsRunType.CodeScan
        }, request.RequestedBy, maskedInput);
        CompleteRun(devOpsRun, run.Status == PipelineRunStatus.Succeeded ? DevOpsRunStatus.Succeeded : DevOpsRunStatus.Blocked, run.Summary);
        audit.Write(request.CustomerId, request.ProjectId, "DEVOPS_PIPELINE_RUN_COMPLETED", nameof(PipelineRun), run.Id, run);
        return run;
    }

    public DeploymentPackage CreateOrUpdatePackage(DeploymentPackageRequest request)
    {
        var pr = ResolvePullRequest(request.CustomerId, request.ProjectId, request.PullRequestId);
        var repo = ResolveRepository(request.CustomerId, request.ProjectId, pr.RepositoryId);
        var package = store.DeploymentPackages.FirstOrDefault(x => x.CustomerId == request.CustomerId && x.ProjectId == request.ProjectId && x.PullRequestId == pr.Id)
            ?? new DeploymentPackage
            {
                CustomerId = request.CustomerId,
                ProjectId = request.ProjectId,
                RepositoryId = repo.Id,
                PullRequestId = pr.Id,
                PackageNo = store.NextNumber("DPKG")
            };
        if (!store.DeploymentPackages.Contains(package))
        {
            store.DeploymentPackages.Add(package);
        }

        package.Version = string.IsNullOrWhiteSpace(request.Version) ? $"0.1.{store.DeploymentPackages.Count}" : request.Version;
        package.RiskLevel = pr.RiskLevel;
        package.BuildRunId = pr.BuildRunId;
        package.TestRunId = pr.TestRunId;
        package.CodeScanRunId = pr.CodeScanRunId;
        package.DiffSummary = $"PR {pr.ExternalPrRef}: {pr.Title}; Areas={pr.ChangeAreasCsv}; Risk={pr.RiskLevel}.";

        var status = EvaluatePackageStatus(request.CustomerId, request.ProjectId, pr);
        package.Status = status;
        package.ArtifactRef = status == DeploymentPackageStatus.Ready ? $"artifact://devops/packages/{package.Id}.zip" : "";
        package.ReadyAt = status == DeploymentPackageStatus.Ready ? DateTimeOffset.UtcNow : null;
        if (status == DeploymentPackageStatus.ApprovalRequired)
        {
            package.ApprovalRequestId = pr.ApprovalRequestId;
        }

        var run = StartRun(request.CustomerId, request.ProjectId, repo.Id, pr.Id, null, DevOpsRunType.Package, request.RequestedBy, package.DiffSummary);
        CompleteRun(run, status == DeploymentPackageStatus.Ready ? DevOpsRunStatus.Succeeded : DevOpsRunStatus.Blocked, $"Deployment package status={status}.");
        audit.Write(request.CustomerId, request.ProjectId, "DEVOPS_DEPLOYMENT_PACKAGE_EVALUATED", nameof(DeploymentPackage), package.Id, package);
        return package;
    }

    public DevOpsPullRequest MergePullRequest(MergePullRequestRequest request)
    {
        var pr = ResolvePullRequest(request.CustomerId, request.ProjectId, request.PullRequestId);
        var repo = ResolveRepository(request.CustomerId, request.ProjectId, pr.RepositoryId);
        if (request.RequestedByAi && IsProtectedBranch(pr.TargetBranch))
        {
            BlockPr(pr, "AI cannot merge into main/master directly.", request.RequestedBy);
            throw new InvalidOperationException("AI cannot merge into main/master directly.");
        }
        if (pr.Status != PullRequestStatus.Approved)
        {
            BlockPr(pr, "Pull request must be approved by review before merge.", request.RequestedBy);
            throw new InvalidOperationException("Pull request must be approved before merge.");
        }
        if (!HasSucceededRun(pr.BuildRunId) || !HasSucceededRun(pr.TestRunId))
        {
            BlockPr(pr, "Build and test must pass before merge.", request.RequestedBy);
            throw new InvalidOperationException("Build and test must pass before merge.");
        }
        var scan = pr.CodeScanRunId.HasValue ? store.PipelineRuns.SingleOrDefault(x => x.Id == pr.CodeScanRunId.Value) : null;
        if (scan is null || scan.Status != PipelineRunStatus.Succeeded)
        {
            BlockPr(pr, "Code scan must pass before merge; high-risk findings block merge.", request.RequestedBy);
            throw new InvalidOperationException("Code scan must pass before merge.");
        }
        if (RequiresApproval(pr) && !HasApprovedApproval(pr.ApprovalRequestId))
        {
            BlockPr(pr, "High/Critical or special-area code changes require approval.", request.RequestedBy);
            throw new InvalidOperationException("High/Critical or special-area code changes require approval.");
        }

        pr.Status = PullRequestStatus.Merged;
        pr.MergeCommitRef = $"mock-merge-{Guid.NewGuid():N}"[..22];
        pr.MergedAt = DateTimeOffset.UtcNow;
        var run = StartRun(request.CustomerId, request.ProjectId, repo.Id, pr.Id, null, DevOpsRunType.Merge, request.RequestedBy, $"{pr.ExternalPrRef} -> {pr.TargetBranch}");
        CompleteRun(run, DevOpsRunStatus.Succeeded, $"PR merged with commit {pr.MergeCommitRef}.");
        audit.Write(request.CustomerId, request.ProjectId, "DEVOPS_PULL_REQUEST_MERGED", nameof(DevOpsPullRequest), pr.Id, pr);
        return pr;
    }

    private DeploymentPackageStatus EvaluatePackageStatus(Guid customerId, Guid projectId, DevOpsPullRequest pr)
    {
        if (!HasSucceededRun(pr.BuildRunId)) return DeploymentPackageStatus.BuildRequired;
        if (!HasSucceededRun(pr.TestRunId)) return DeploymentPackageStatus.TestRequired;
        var scan = pr.CodeScanRunId.HasValue ? store.PipelineRuns.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == pr.CodeScanRunId.Value) : null;
        if (scan is null || scan.Status == PipelineRunStatus.Blocked) return DeploymentPackageStatus.ScanBlocked;
        if (scan.Status != PipelineRunStatus.Succeeded) return DeploymentPackageStatus.Blocked;
        if (RequiresApproval(pr) && !HasApprovedApproval(pr.ApprovalRequestId)) return DeploymentPackageStatus.ApprovalRequired;
        return DeploymentPackageStatus.Ready;
    }

    private DevOpsRepository ResolveRepository(Guid customerId, Guid projectId, Guid repositoryId) =>
        store.DevOpsRepositories.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == repositoryId && x.Status == "Active")
        ?? throw new InvalidOperationException("Repository not found for this customer/project.");

    private DevOpsPullRequest ResolvePullRequest(Guid customerId, Guid projectId, Guid pullRequestId) =>
        store.DevOpsPullRequests.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == pullRequestId)
        ?? throw new InvalidOperationException("Pull request not found for this customer/project.");

    private void EnsureRepoMatch(DevOpsRepository repo, DevOpsPullRequest? pr)
    {
        if (pr is not null && pr.RepositoryId != repo.Id)
        {
            throw new InvalidOperationException("Pull request belongs to another repository.");
        }
    }

    private AiCodeGovernancePolicy ResolvePolicy(Guid customerId, Guid projectId, Guid repositoryId) =>
        store.AiCodeGovernancePolicies
            .Where(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Active && (x.RepositoryId == repositoryId || x.RepositoryId == null))
            .OrderByDescending(x => x.RepositoryId.HasValue)
            .FirstOrDefault()
        ?? new AiCodeGovernancePolicy { CustomerId = customerId, ProjectId = projectId, RepositoryId = repositoryId, PolicyKey = "default.devops.ai-code", MaxDiffBytes = DefaultMaxDiffBytes };

    private DevOpsRun StartRun(Guid customerId, Guid projectId, Guid? repositoryId, Guid? pullRequestId, Guid? pipelineRunId, DevOpsRunType type, string actor, string input)
    {
        var run = new DevOpsRun
        {
            CustomerId = customerId,
            ProjectId = projectId,
            RepositoryId = repositoryId,
            PullRequestId = pullRequestId,
            PipelineRunId = pipelineRunId,
            RunType = type,
            Status = DevOpsRunStatus.Running,
            ActorUserId = actor,
            MaskedInput = Limit(masking.Mask(input), 2000),
            StartedAt = DateTimeOffset.UtcNow
        };
        store.DevOpsRuns.Add(run);
        AddLog(run, "Info", $"{type} started.", run.MaskedInput);
        return run;
    }

    private void CompleteRun(DevOpsRun run, DevOpsRunStatus status, string summary)
    {
        run.Status = status;
        run.Summary = Limit(summary, 700);
        run.CompletedAt = DateTimeOffset.UtcNow;
        AddLog(run, status == DevOpsRunStatus.Succeeded ? "Info" : "Warning", summary, run.MaskedInput);
    }

    private void BlockPr(DevOpsPullRequest pr, string reason, string actor)
    {
        pr.Status = PullRequestStatus.Blocked;
        var run = StartRun(pr.CustomerId, pr.ProjectId, pr.RepositoryId, pr.Id, null, DevOpsRunType.Merge, actor, reason);
        CompleteRun(run, DevOpsRunStatus.Blocked, reason);
        audit.Write(pr.CustomerId, pr.ProjectId, "DEVOPS_PULL_REQUEST_BLOCKED", nameof(DevOpsPullRequest), pr.Id, new { pr, reason });
    }

    private void AddLog(DevOpsRun run, string level, string message, string payload)
    {
        store.DevOpsRunLogs.Add(new DevOpsRunLog
        {
            CustomerId = run.CustomerId,
            ProjectId = run.ProjectId,
            DevOpsRunId = run.Id,
            Level = level,
            Message = Limit(message, 700),
            MaskedPayload = Limit(payload, 2000)
        });
    }

    private SourceCodeSnapshot NewSnapshot(Guid customerId, Guid projectId, Guid repoId, string branch, string summary, string diffText) =>
        new()
        {
            CustomerId = customerId,
            ProjectId = projectId,
            RepositoryId = repoId,
            BranchName = branch,
            CommitSha = $"mock-{Guid.NewGuid():N}"[..17],
            SnapshotNo = store.NextNumber("SNAP"),
            MetadataJson = JsonSerializer.Serialize(new { provider = "MockGit", captured = DateTimeOffset.UtcNow }, JsonOptions),
            DiffSummary = summary,
            DiffTextPreview = Limit(masking.Mask(diffText), 2000),
            DiffSizeBytes = Encoding.UTF8.GetByteCount(diffText)
        };

    private bool HasSucceededRun(Guid? runId) =>
        runId.HasValue && store.PipelineRuns.Any(x => x.Id == runId.Value && x.Status == PipelineRunStatus.Succeeded);

    private bool HasApprovedApproval(Guid? approvalRequestId) =>
        approvalRequestId.HasValue && store.ApprovalRequests.Any(x => x.Id == approvalRequestId.Value && x.Status == ApprovalStatus.Approved);

    private static bool RequiresApproval(DevOpsPullRequest pr) =>
        pr.RiskLevel is RiskLevel.High or RiskLevel.Critical || HasSpecialArea(pr.ChangeAreasCsv);

    private static bool HasSpecialArea(string areasCsv) =>
        SplitCsv(areasCsv).Any(area => SpecialAreas.Contains(area, StringComparer.OrdinalIgnoreCase));

    private static string NormalizeAreas(string areasCsv)
    {
        var areas = SplitCsv(areasCsv).DefaultIfEmpty("Other").Distinct(StringComparer.OrdinalIgnoreCase);
        return string.Join(",", areas);
    }

    private static string MergeAreas(string left, string right) =>
        string.Join(",", SplitCsv(left).Concat(SplitCsv(right)).DefaultIfEmpty("Other").Distinct(StringComparer.OrdinalIgnoreCase));

    private static IReadOnlyList<string> DetectAreas(string text)
    {
        var areas = new List<string>();
        if (ContainsAny(text, "payroll", "salary", "tax")) areas.Add(nameof(CodeChangeArea.Payroll));
        if (ContainsAny(text, "permission", "role", "rbac")) areas.Add(nameof(CodeChangeArea.Permission));
        if (ContainsAny(text, "security", "auth", "jwt", "password", "token", "secret")) areas.Add(nameof(CodeChangeArea.Security));
        if (ContainsAny(text, "integration", "webhook", "gateway", "provider")) areas.Add(nameof(CodeChangeArea.Integration));
        if (ContainsAny(text, "production", "deploy", "release")) areas.Add(nameof(CodeChangeArea.ProductionDeployment));
        if (areas.Count == 0) areas.Add(nameof(CodeChangeArea.Hrm));
        return areas.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static RiskLevel DetectRisk(string text, IReadOnlyList<string> areas)
    {
        if (ContainsAny(text, "critical", "production database", "direct deploy")) return RiskLevel.Critical;
        if (areas.Any(x => SpecialAreas.Contains(x, StringComparer.OrdinalIgnoreCase)) || ContainsAny(text, "high risk", "highrisk")) return RiskLevel.High;
        return ContainsAny(text, "config", "migration") ? RiskLevel.Medium : RiskLevel.Low;
    }

    private static RiskLevel MaxRisk(RiskLevel left, RiskLevel right) => (RiskLevel)Math.Max((int)left, (int)right);

    private static bool IsProtectedBranch(string branch) =>
        ProtectedBranches.Contains(branch.Trim(), StringComparer.OrdinalIgnoreCase);

    private static string BuildMockDiff(string branchName, string intent, IReadOnlyList<string> areas) =>
        $"""
        diff --git a/src/hrm-aiops/{branchName}.cs b/src/hrm-aiops/{branchName}.cs
        + // AI patch proposal only. Human review and PR approval are required before merge.
        + // Intent: {intent}
        + // Governance areas: {string.Join(",", areas)}
        """;

    private static string Limit(string value, int max)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Length <= max ? value : value[..max];
    }

    private static void ValidateSecretRef(string secretRef)
    {
        if (string.IsNullOrWhiteSpace(secretRef) || !secretRef.StartsWith("secret://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Git credentials must store only secret_ref values.");
        }
    }

    private static bool ContainsAny(string value, params string[] tokens) =>
        tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<string> SplitCsv(string csv) =>
        (csv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
