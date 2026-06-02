using HrmAiOps.Application.Abstractions;
using HrmAiOps.Domain.Core;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace HrmAiOps.Infrastructure.Ai;

public sealed class LocalAiProvider : IAiProvider
{
    public Task<string> GenerateAsync(string runType, string input, CancellationToken cancellationToken)
    {
        var output = runType switch
        {
            "generate_urs" => $"# User Requirement Specification\n\n## Scope\n{input}\n\n## Acceptance Criteria\n- Requirement is traceable.\n- Customer data remains isolated.\n- Production changes require approval.",
            "generate_blueprint" => $"# Blueprint\n\n## Functional Design\n{input}\n\n## Technical Notes\n- Modular monolith boundary.\n- API-first workflow.\n- Audit and trace links are mandatory.",
            "generate_config_spec" => $"# Config Specification\n\n## HRM Module\n{input}\n\n## Controls\n- Store only secret_ref.\n- Environment-specific settings are versioned.",
            "diagnosis" => $"# Diagnosis\n\nLikely root cause derived from the issue context:\n{input}\n\nRecommended next step: compare environment config, recent releases, and database profile metadata.",
            "fix_proposal" => $"# Fix Proposal\n\n## Proposed Fix\nAddress the diagnosed failure path for:\n{input}\n\n## Risk\nMedium. Requires review before UAT/Production release.",
            _ => $"Generated draft for {runType}:\n{input}"
        };

        return Task.FromResult(output);
    }
}

public sealed class AiRunRecorder(IAppStore store) : IAiRunRecorder
{
    public AiRun Start(Guid customerId, Guid? projectId, string runType, string? inputSummary)
    {
        var run = new AiRun
        {
            CustomerId = customerId,
            ProjectId = projectId,
            RunType = runType,
            InputSummary = inputSummary,
            Status = AiRunStatus.Running,
            StartedAt = DateTimeOffset.UtcNow
        };
        store.AiRuns.Add(run);
        return run;
    }

    public AiRun Start(Guid customerId, Guid? projectId, string runType, string? inputSummary, string? inputRef, AiPromptTemplateVersion? promptVersion)
    {
        var run = Start(customerId, projectId, runType, inputSummary);
        run.InputRef = inputRef;
        if (promptVersion is not null)
        {
            run.PromptTemplateId = promptVersion.TemplateId.ToString();
            run.PromptTemplateKey = promptVersion.TemplateKey;
            run.PromptVersion = promptVersion.Version;
        }
        return run;
    }

    public void Complete(AiRun run, string outputSummary, string? outputRef = null)
    {
        run.Status = AiRunStatus.Completed;
        run.OutputSummary = outputSummary.Length > 600 ? outputSummary[..600] : outputSummary;
        run.OutputRef = outputRef;
        run.CompletedAt = DateTimeOffset.UtcNow;
        run.TokenInput = run.InputSummary?.Length / 4 ?? 0;
        run.TokenOutput = outputSummary.Length / 4;
    }

    public void Complete(AiRun run, string outputSummary, string? outputRef, string rawOutputJson, IReadOnlyList<string> validationErrors)
    {
        Complete(run, outputSummary, outputRef);
        run.RawOutputJson = rawOutputJson;
        run.ValidationErrorsJson = JsonSerializer.Serialize(validationErrors);
    }

    public void Fail(AiRun run, string errorMessage)
    {
        run.Status = AiRunStatus.Failed;
        run.ErrorMessage = errorMessage;
        run.CompletedAt = DateTimeOffset.UtcNow;
    }
}

public sealed class RegexDataMaskingService : IDataMaskingService
{
    private static readonly Regex EmailRegex = new(@"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new(@"(?<![\w-])(?:\+?\d{1,3}[\s.])?(?:\(?\d{2,4}\)?[\s.]?){2,4}\d{2,4}(?![\w-])", RegexOptions.Compiled);
    private static readonly Regex SecretRegex = new(@"(?i)([""']?(?:password|token|api[_-]?key|secret)[""']?\s*[:=]\s*)[""']?[^,\s""'}]+", RegexOptions.Compiled);

    public string Mask(string input)
    {
        var masked = EmailRegex.Replace(input, "[masked-email]");
        masked = PhoneRegex.Replace(masked, "[masked-phone]");
        masked = SecretRegex.Replace(masked, "$1\"[masked-secret]\"");
        return masked;
    }
}

public sealed class AiContextBuilder(IAppStore store, IDataMaskingService masking) : IAiContextBuilder
{
    public Task<AiContext> BuildAsync(Guid customerId, Guid projectId, string sourceEntityType, Guid sourceEntityId, CancellationToken cancellationToken)
    {
        var project = store.Projects.SingleOrDefault(x => x.CustomerId == customerId && x.Id == projectId)
            ?? throw new InvalidOperationException("Project not found or does not belong to customer.");

        object source = sourceEntityType switch
        {
            "Requirement" => store.Requirements.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == sourceEntityId)
                ?? throw new InvalidOperationException("Requirement not found."),
            "UrsDocument" => store.UrsDocuments.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == sourceEntityId)
                ?? throw new InvalidOperationException("URS not found."),
            "Blueprint" => store.Blueprints.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == sourceEntityId)
                ?? throw new InvalidOperationException("Blueprint not found."),
            "Issue" => store.Issues.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == sourceEntityId)
                ?? throw new InvalidOperationException("Issue not found."),
            "IssueAnalysis" => store.IssueAnalyses.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == sourceEntityId)
                ?? throw new InvalidOperationException("Issue analysis not found."),
            "FixProposal" => store.FixProposals.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == sourceEntityId)
                ?? throw new InvalidOperationException("Fix proposal not found."),
            "ChangeRequest" => store.ChangeRequests.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == sourceEntityId)
                ?? throw new InvalidOperationException("Change request not found."),
            _ => throw new InvalidOperationException("Unsupported source entity.")
        };

        var context = new
        {
            project = new { project.Code, project.Name, project.HrmProductName },
            sourceEntityType,
            source,
            relatedModules = store.HrmModules.Select(x => new { x.Code, x.Name, x.DefaultRiskLevel }).ToArray()
        };
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        var json = JsonSerializer.Serialize(context, options);
        var masked = masking.Mask(json);
        return Task.FromResult(new AiContext(customerId, projectId, sourceEntityType, sourceEntityId, masked, masked.Length > 700 ? masked[..700] : masked));
    }
}

public sealed class LocalStructuredAiProvider : IStructuredAiProvider
{
    public Task<AiProviderResponse> ExecuteAsync(AiProviderRequest request, CancellationToken cancellationToken)
    {
        var title = request.TaskType switch
        {
            nameof(AiTaskType.GenerateUrs) => "AI Draft URS",
            nameof(AiTaskType.GenerateBlueprint) => "AI Draft Blueprint",
            nameof(AiTaskType.GenerateConfigSpec) => "AI Draft Config Specification",
            nameof(AiTaskType.ClassifyIssue) => "AI Issue Classification",
            nameof(AiTaskType.AnalyzeRootCause) => "AI Root Cause Analysis",
            nameof(AiTaskType.GenerateFixProposal) => "AI Fix Proposal",
            nameof(AiTaskType.GenerateChangeRequest) => "AI Change Request",
            nameof(AiTaskType.GenerateRegressionTestPlan) => "AI Regression Test Plan",
            nameof(AiTaskType.GenerateReleaseDraft) => "AI Release Draft",
            nameof(AiTaskType.GenerateKnowledgeUpdate) => "AI Knowledge Update",
            _ => "AI Draft"
        };
        var content = request.TaskType switch
        {
            nameof(AiTaskType.GenerateUrs) => "## Scope\nGenerated URS proposal from masked requirement context.\n\n## Acceptance Criteria\n- Customer-scoped traceability is preserved.\n- Approved source documents are not modified.\n- Consultant review is required before acceptance.",
            nameof(AiTaskType.GenerateBlueprint) => "## Functional Blueprint\nGenerated blueprint proposal from approved or draft URS context.\n\n## Process\nSubmit, review, approve, audit.\n\n## Data Objects\nEmployee, Request, ApprovalStep, Balance.",
            nameof(AiTaskType.GenerateConfigSpec) => "## Configuration Specification\nGenerated config proposal from blueprint context.\n\n## Parameters\nLeave type, approval matrix, balance visibility, HR override permission.",
            nameof(AiTaskType.ClassifyIssue) => "## Classification\nIssue categorized from masked support context.\n\n## Recommendation\nProceed with root cause analysis before any change.",
            nameof(AiTaskType.AnalyzeRootCause) => "## Root Cause\nLikely caused by configuration, data, integration, or permission mismatch.\n\n## Evidence\nReview linked documents, environment metadata, and recent accepted configuration specs.",
            nameof(AiTaskType.GenerateFixProposal) => "## Fix Proposal\nApply targeted configuration or code review in a non-production environment.\n\n## Constraints\nNo production write and no source PR is created by AI.",
            nameof(AiTaskType.GenerateChangeRequest) => "## Change Request\nPrepare controlled change with implementation summary, risk, rollback idea, and approval need.",
            nameof(AiTaskType.GenerateRegressionTestPlan) => "## Regression Test Plan\nValidate happy path, permission boundaries, integration side effects, and payroll/security impact where relevant.",
            nameof(AiTaskType.GenerateReleaseDraft) => "## Release Draft\nDraft release notes and deployment checklist. This is not an executable production release.",
            nameof(AiTaskType.GenerateKnowledgeUpdate) => "## Knowledge Update\nDocument symptom, root cause, workaround, resolution, and prevention notes.",
            _ => "Generated structured proposal."
        };
        var category = request.ContextJson.Contains("payroll", StringComparison.OrdinalIgnoreCase)
            ? "Payroll"
            : request.ContextJson.Contains("permission", StringComparison.OrdinalIgnoreCase)
                ? "Permission"
                : request.ContextJson.Contains("security", StringComparison.OrdinalIgnoreCase)
                    ? "Security"
                    : request.ContextJson.Contains("production database", StringComparison.OrdinalIgnoreCase) || request.ContextJson.Contains("ProductionDatabase", StringComparison.OrdinalIgnoreCase)
                        ? "ProductionDatabase"
                        : request.ContextJson.Contains("integration", StringComparison.OrdinalIgnoreCase)
                            ? "Integration"
                            : request.ContextJson.Contains("config", StringComparison.OrdinalIgnoreCase)
                                ? "Configuration"
                                : request.ContextJson.Contains("data", StringComparison.OrdinalIgnoreCase)
                                    ? "Data"
                                    : "Other";
        var riskLevel = category is "Payroll" or "Permission" or "Security" or "ProductionDatabase"
            ? "Critical"
            : category is "Integration" ? "High" : "Medium";

        var output = JsonSerializer.Serialize(new
        {
            title,
            content,
            moduleName = request.TaskType == nameof(AiTaskType.GenerateConfigSpec) ? "Leave Management" : null,
            riskLevel,
            category = request.TaskType == nameof(AiTaskType.ClassifyIssue) ? category : null,
            traceSummary = "Generated from masked project-scoped context.",
            validationNotes = new[] { "JSON shape is provider-controlled.", "Requires consultant accept/reject." }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return Task.FromResult(new AiProviderResponse("LocalStructured", "structured-json-v1", output));
    }
}

public sealed class StructuredOutputValidator : IStructuredOutputValidator
{
    public StructuredOutputValidationResult Validate(AiTaskType taskType, string outputJson)
    {
        var errors = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(outputJson);
            var root = doc.RootElement;
            var title = ReadString(root, "title");
            var content = ReadString(root, "content");
            if (string.IsNullOrWhiteSpace(title))
            {
                errors.Add("title is required.");
            }
            if (string.IsNullOrWhiteSpace(content))
            {
                errors.Add("content is required.");
            }

            RiskLevel? riskLevel = null;
            if (taskType is AiTaskType.GenerateConfigSpec or AiTaskType.ClassifyIssue or AiTaskType.GenerateFixProposal or AiTaskType.GenerateChangeRequest or AiTaskType.GenerateRegressionTestPlan or AiTaskType.GenerateReleaseDraft)
            {
                var risk = ReadString(root, "riskLevel");
                if (string.IsNullOrWhiteSpace(risk) || !Enum.TryParse<RiskLevel>(risk, true, out var parsedRisk))
                {
                    errors.Add("riskLevel is required for config specification and must be Low, Medium, High or Critical.");
                }
                else
                {
                    riskLevel = parsedRisk;
                }
            }

            return new StructuredOutputValidationResult(errors.Count == 0, title, content, riskLevel, errors);
        }
        catch (JsonException ex)
        {
            errors.Add($"Invalid JSON: {ex.Message}");
            return new StructuredOutputValidationResult(false, "", "", null, errors);
        }
    }

    private static string ReadString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
}

public sealed class AiTaskExecutor(
    IAppStore store,
    IAiContextBuilder contextBuilder,
    IStructuredAiProvider provider,
    IStructuredOutputValidator validator,
    IAiRunRecorder runs,
    IAuditWriter audit) : IAiTaskExecutor
{
    public async Task<AiProposal> ExecuteAsync(AiTaskExecutionRequest request, CancellationToken cancellationToken)
    {
        var prompt = ResolvePrompt(request.TaskType);
        var context = await contextBuilder.BuildAsync(request.CustomerId, request.ProjectId, request.SourceEntityType, request.SourceEntityId, cancellationToken);
        var run = runs.Start(request.CustomerId, request.ProjectId, request.TaskType.ToString(), context.InputSummary, $"{request.SourceEntityType}:{request.SourceEntityId}", prompt);
        run.MaskedInputPreview = context.InputSummary;

        try
        {
            var aiResponse = await provider.ExecuteAsync(new AiProviderRequest(request.TaskType.ToString(), prompt.SystemPrompt, prompt.UserPromptTemplate, prompt.OutputJsonSchema, context.ContextJson), cancellationToken);
            run.Provider = aiResponse.Provider;
            run.Model = aiResponse.Model;
            var validation = validator.Validate(request.TaskType, aiResponse.OutputJson);
            var proposal = new AiProposal
            {
                CustomerId = request.CustomerId,
                ProjectId = request.ProjectId,
                AiRunId = run.Id,
                TaskType = request.TaskType,
                SourceEntityType = request.SourceEntityType,
                SourceEntityId = request.SourceEntityId,
                TargetEntityType = TargetEntityType(request.TaskType),
                Status = validation.IsValid ? AiProposalStatus.PendingReview : AiProposalStatus.FailedValidation,
                Title = validation.Title,
                ProposedContent = validation.Content,
                StructuredOutputJson = aiResponse.OutputJson,
                ValidationErrorsJson = JsonSerializer.Serialize(validation.Errors)
            };
            store.AiProposals.Add(proposal);
            runs.Complete(run, validation.Content, $"ai_proposal:{proposal.Id}", aiResponse.OutputJson, validation.Errors);
            audit.Write(request.CustomerId, request.ProjectId, "AI_PROPOSAL_CREATED", nameof(AiProposal), proposal.Id, proposal);
            return proposal;
        }
        catch (Exception ex)
        {
            runs.Fail(run, ex.Message);
            throw;
        }
    }

    private AiPromptTemplateVersion ResolvePrompt(AiTaskType taskType)
    {
        var template = store.AiPromptTemplates.FirstOrDefault(x => x.TaskType == taskType && x.IsActive)
            ?? throw new InvalidOperationException($"No active prompt template found for {taskType}.");
        return store.AiPromptTemplateVersions
            .Where(x => x.TemplateId == template.Id && x.IsActive)
            .OrderByDescending(x => x.Version)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"No active prompt version found for {template.Key}.");
    }

    private static string TargetEntityType(AiTaskType taskType) => taskType switch
    {
        AiTaskType.GenerateUrs => nameof(UrsDocument),
        AiTaskType.GenerateBlueprint => nameof(Blueprint),
        AiTaskType.GenerateConfigSpec => nameof(ConfigSpec),
        AiTaskType.ClassifyIssue => nameof(Issue),
        AiTaskType.AnalyzeRootCause => nameof(IssueAnalysis),
        AiTaskType.GenerateFixProposal => nameof(FixProposal),
        AiTaskType.GenerateChangeRequest => nameof(ChangeRequest),
        AiTaskType.GenerateRegressionTestPlan => nameof(RegressionTestPlan),
        AiTaskType.GenerateReleaseDraft => nameof(ReleaseDraft),
        AiTaskType.GenerateKnowledgeUpdate => nameof(KnowledgeArticle),
        _ => "Document"
    };
}
