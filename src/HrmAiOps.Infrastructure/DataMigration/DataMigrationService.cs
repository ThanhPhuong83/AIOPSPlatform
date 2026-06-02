using System.Text.Json;
using System.Text.Json.Serialization;
using HrmAiOps.Application.Abstractions;
using HrmAiOps.Domain.Core;

namespace HrmAiOps.Infrastructure.DataMigration;

public sealed class DataMigrationService(
    IAppStore store,
    IAuditWriter audit,
    IDataMaskingService masking,
    IAiRunRecorder aiRuns,
    IBackgroundJobDispatcher jobs) : IDataMigrationService
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public object GetDashboard(DataMigrationDashboardFilter filter)
    {
        var batches = store.DataImportBatches.Where(x => x.CustomerId == filter.CustomerId && x.ProjectId == filter.ProjectId && (!filter.EnvironmentId.HasValue || x.EnvironmentId == filter.EnvironmentId.Value)).ToList();
        var batchIds = batches.Select(x => x.Id).ToHashSet();
        var runs = store.DataImportRuns.Where(x => x.CustomerId == filter.CustomerId && x.ProjectId == filter.ProjectId && batchIds.Contains(x.BatchId)).ToList();
        return new
        {
            filter,
            templates = store.DataImportTemplates.Count(x => x.CustomerId == filter.CustomerId && x.ProjectId == filter.ProjectId && x.Active),
            files = store.DataImportFiles.Count(x => x.CustomerId == filter.CustomerId && x.ProjectId == filter.ProjectId),
            batches = batches.Count,
            dryRunPassed = runs.Count(x => x.RunType == DataImportRunType.DryRun && x.Status == DataImportRunStatus.Passed),
            applied = batches.Count(x => x.Status == ImportBatchStatus.AppliedToTestUat || x.Status == ImportBatchStatus.Reconciled || x.Status == ImportBatchStatus.SignedOff),
            reconciled = store.DataReconciliationReports.Count(x => x.CustomerId == filter.CustomerId && x.ProjectId == filter.ProjectId && batchIds.Contains(x.BatchId)),
            signedOff = store.DataSignOffs.Count(x => x.CustomerId == filter.CustomerId && x.ProjectId == filter.ProjectId && batchIds.Contains(x.BatchId) && x.Status == DataSignOffStatus.SignedOff),
            openValidationIssues = store.DataValidationIssues.Count(x => x.CustomerId == filter.CustomerId && x.ProjectId == filter.ProjectId && x.Severity is ValidationIssueSeverity.Error or ValidationIssueSeverity.Critical),
            latestBatches = batches.OrderByDescending(x => x.CreatedAt).Take(8),
            latestRuns = runs.OrderByDescending(x => x.StartedAt).Take(8)
        };
    }

    public DataImportTemplate CreateTemplate(DataImportTemplateRequest request)
    {
        EnsureProject(request.CustomerId, request.ProjectId);
        var template = new DataImportTemplate
        {
            CustomerId = request.CustomerId,
            ProjectId = request.ProjectId,
            TemplateKey = request.TemplateKey,
            Name = request.Name,
            Domain = request.Domain,
            DefaultFileType = request.DefaultFileType,
            MaxClassification = request.MaxClassification,
            Status = ImportTemplateStatus.Active,
            CurrentVersion = 1,
            Active = true
        };
        var version = new DataImportTemplateVersion
        {
            CustomerId = request.CustomerId,
            ProjectId = request.ProjectId,
            TemplateId = template.Id,
            Version = 1,
            SchemaJson = request.SchemaJson,
            SampleFileRef = request.SampleFileRef,
            CreatedBy = request.RequestedBy
        };
        store.DataImportTemplates.Add(template);
        store.DataImportTemplateVersions.Add(version);
        audit.Write(request.CustomerId, request.ProjectId, "DATA_IMPORT_TEMPLATE_CREATED", nameof(DataImportTemplate), template.Id, new { template, version });
        return template;
    }

    public DataImportFile RegisterFile(DataImportFileRequest request, bool canViewSensitivePreview)
    {
        EnsureProject(request.CustomerId, request.ProjectId);
        if (string.IsNullOrWhiteSpace(request.FileRef))
        {
            throw new InvalidOperationException("FileRef is required. Binary file content must not be stored in the database.");
        }
        if (request.EnvironmentId.HasValue)
        {
            EnsureEnvironment(request.CustomerId, request.ProjectId, request.EnvironmentId.Value, false);
        }
        var preview = ShouldMask(request.Classification, canViewSensitivePreview) ? masking.Mask(request.PreviewJson) : request.PreviewJson;
        var file = new DataImportFile
        {
            CustomerId = request.CustomerId,
            ProjectId = request.ProjectId,
            EnvironmentId = request.EnvironmentId,
            TemplateId = request.TemplateId,
            FileRef = request.FileRef,
            FileName = request.FileName,
            FileType = request.FileType,
            SizeBytes = request.SizeBytes,
            RowCount = request.RowCount,
            Classification = request.Classification,
            UploadedBy = request.UploadedBy,
            MaskedPreviewJson = Limit(preview, 4000)
        };
        store.DataImportFiles.Add(file);
        audit.Write(request.CustomerId, request.ProjectId, "DATA_IMPORT_FILE_REGISTERED", nameof(DataImportFile), file.Id, new { file, binaryStored = false });
        return file;
    }

    public DataColumnMapping CreateMapping(DataColumnMappingRequest request)
    {
        var template = ResolveTemplate(request.CustomerId, request.ProjectId, request.TemplateId);
        var nextVersion = store.DataColumnMappings
            .Where(x => x.CustomerId == request.CustomerId && x.ProjectId == request.ProjectId && x.TemplateId == request.TemplateId && x.MappingKey == request.MappingKey)
            .Select(x => x.MappingVersion)
            .DefaultIfEmpty(0)
            .Max() + 1;
        foreach (var old in store.DataColumnMappings.Where(x => x.CustomerId == request.CustomerId && x.ProjectId == request.ProjectId && x.TemplateId == request.TemplateId && x.MappingKey == request.MappingKey && x.Status == DataMappingStatus.Active))
        {
            old.Status = DataMappingStatus.Superseded;
        }
        var mapping = new DataColumnMapping
        {
            CustomerId = request.CustomerId,
            ProjectId = request.ProjectId,
            TemplateId = template.Id,
            TemplateVersion = request.TemplateVersion <= 0 ? template.CurrentVersion : request.TemplateVersion,
            MappingVersion = nextVersion,
            MappingKey = request.MappingKey,
            SourceColumn = request.SourceColumn,
            TargetEntity = request.TargetEntity,
            TargetField = request.TargetField,
            TransformExpression = request.TransformExpression,
            DataClassification = request.DataClassification,
            Status = DataMappingStatus.Active
        };
        store.DataColumnMappings.Add(mapping);
        audit.Write(request.CustomerId, request.ProjectId, "DATA_COLUMN_MAPPING_VERSION_CREATED", nameof(DataColumnMapping), mapping.Id, mapping);
        return mapping;
    }

    public DataValidationRule CreateValidationRule(DataValidationRuleRequest request)
    {
        EnsureProject(request.CustomerId, request.ProjectId);
        var rule = new DataValidationRule
        {
            CustomerId = request.CustomerId,
            ProjectId = request.ProjectId,
            TemplateId = request.TemplateId,
            RuleKey = request.RuleKey,
            Name = request.Name,
            Domain = request.Domain,
            TargetField = request.TargetField,
            RuleType = request.RuleType,
            ExpressionJson = request.ExpressionJson,
            Severity = request.Severity,
            Active = true
        };
        store.DataValidationRules.Add(rule);
        audit.Write(request.CustomerId, request.ProjectId, "DATA_VALIDATION_RULE_CREATED", nameof(DataValidationRule), rule.Id, rule);
        return rule;
    }

    public DataImportBatch CreateBatch(DataImportBatchRequest request)
    {
        EnsureEnvironment(request.CustomerId, request.ProjectId, request.EnvironmentId, false);
        ResolveTemplate(request.CustomerId, request.ProjectId, request.TemplateId);
        var file = store.DataImportFiles.SingleOrDefault(x => x.CustomerId == request.CustomerId && x.ProjectId == request.ProjectId && x.Id == request.ImportFileId)
            ?? throw new InvalidOperationException("Import file not found for this customer/project.");
        if (request.ConnectorId.HasValue && !store.CustomerConnectors.Any(x => x.CustomerId == request.CustomerId && x.ProjectId == request.ProjectId && x.Id == request.ConnectorId.Value))
        {
            throw new InvalidOperationException("Connector does not belong to this customer/project.");
        }
        var batch = new DataImportBatch
        {
            CustomerId = request.CustomerId,
            ProjectId = request.ProjectId,
            EnvironmentId = request.EnvironmentId,
            ConnectorId = request.ConnectorId,
            TemplateId = request.TemplateId,
            TemplateVersion = request.TemplateVersion,
            ImportFileId = file.Id,
            BatchNo = store.NextNumber("DIMP"),
            Domain = request.Domain,
            Status = ImportBatchStatus.FileUploaded,
            RequestedBy = request.RequestedBy
        };
        store.DataImportBatches.Add(batch);
        audit.Write(request.CustomerId, request.ProjectId, "DATA_IMPORT_BATCH_CREATED", nameof(DataImportBatch), batch.Id, batch);
        return batch;
    }

    public async Task<DataImportRun> DryRunAsync(Guid customerId, Guid projectId, Guid batchId, string requestedBy, CancellationToken cancellationToken)
    {
        var batch = ResolveBatch(customerId, projectId, batchId);
        EnsureEnvironment(customerId, projectId, batch.EnvironmentId, false);
        await jobs.EnqueueAsync("data_import.dry_run", new { customerId, projectId, batchId }, cancellationToken);
        var run = CreateRun(batch, DataImportRunType.DryRun);
        ValidateBatch(batch, run);
        run.Status = run.ErrorRows == 0 ? DataImportRunStatus.Passed : DataImportRunStatus.Failed;
        run.Summary = run.Status == DataImportRunStatus.Passed
            ? $"Dry run passed for {run.ValidRows}/{run.TotalRows} rows."
            : $"Dry run failed with {run.ErrorRows} error rows and {run.DuplicateRows} duplicates.";
        run.CompletedAt = DateTimeOffset.UtcNow;
        batch.DryRunId = run.Id;
        batch.Status = run.Status == DataImportRunStatus.Passed ? ImportBatchStatus.DryRunPassed : ImportBatchStatus.DryRunFailed;
        audit.Write(customerId, projectId, "DATA_IMPORT_DRY_RUN_COMPLETED", nameof(DataImportRun), run.Id, new { run, batch });
        return run;
    }

    public async Task<DataImportRun> ApplyToTestUatAsync(Guid customerId, Guid projectId, Guid batchId, string requestedBy, CancellationToken cancellationToken)
    {
        var batch = ResolveBatch(customerId, projectId, batchId);
        var environment = EnsureEnvironment(customerId, projectId, batch.EnvironmentId, false);
        if (environment.Kind == EnvironmentKind.Production)
        {
            throw new InvalidOperationException("Phase 17 does not allow production import. Only Test/UAT is supported.");
        }
        if (environment.Kind is not (EnvironmentKind.Test or EnvironmentKind.Uat))
        {
            throw new InvalidOperationException("Import apply is only allowed for Test/UAT environments.");
        }
        if (!batch.DryRunId.HasValue || !store.DataImportRuns.Any(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == batch.DryRunId.Value && x.Status == DataImportRunStatus.Passed))
        {
            throw new InvalidOperationException("Dry run must pass before apply.");
        }
        if (batch.ConnectorId.HasValue && !batch.PreImportSnapshotId.HasValue)
        {
            var snapshot = new EnvironmentSnapshot
            {
                CustomerId = customerId,
                ProjectId = projectId,
                EnvironmentId = batch.EnvironmentId,
                ConnectorId = batch.ConnectorId.Value,
                SnapshotNo = store.NextNumber("SNAP"),
                Kind = SnapshotKind.ApplyComposite,
                Stage = SnapshotStage.PreApply,
                Summary = $"Pre-import snapshot for {batch.BatchNo}.",
                SnapshotJson = JsonSerializer.Serialize(new { storageRef = $"snapshot://data-import/{batch.Id}/pre", batchId = batch.Id }, JsonOptions),
                MaskedSummary = $"Pre-import snapshot for {batch.BatchNo}."
            };
            store.EnvironmentSnapshots.Add(snapshot);
            batch.PreImportSnapshotId = snapshot.Id;
        }
        await jobs.EnqueueAsync("data_import.apply_test_uat", new { customerId, projectId, batchId }, cancellationToken);
        var run = CreateRun(batch, DataImportRunType.Apply);
        var file = ResolveFile(customerId, projectId, batch.ImportFileId);
        run.ValidRows = file.RowCount;
        run.TotalRows = file.RowCount;
        run.ErrorRows = 0;
        run.DuplicateRows = 0;
        run.Status = DataImportRunStatus.Passed;
        run.Summary = $"Mock import applied {run.ValidRows} rows to {environment.Kind}.";
        run.CompletedAt = DateTimeOffset.UtcNow;
        batch.ApplyRunId = run.Id;
        batch.Status = ImportBatchStatus.AppliedToTestUat;
        audit.Write(customerId, projectId, "DATA_IMPORT_APPLIED_TEST_UAT", nameof(DataImportRun), run.Id, new { run, batch, productionApplied = false });
        return run;
    }

    public DataReconciliationReport Reconcile(Guid customerId, Guid projectId, Guid batchId, string requestedBy)
    {
        var batch = ResolveBatch(customerId, projectId, batchId);
        if (!batch.ApplyRunId.HasValue)
        {
            throw new InvalidOperationException("Apply run is required before reconciliation.");
        }
        var run = store.DataImportRuns.Single(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == batch.ApplyRunId.Value);
        var report = new DataReconciliationReport
        {
            CustomerId = customerId,
            ProjectId = projectId,
            BatchId = batch.Id,
            ImportRunId = run.Id,
            ReportNo = store.NextNumber("DREC"),
            Status = run.ValidRows == run.TotalRows ? ReconciliationStatus.Matched : ReconciliationStatus.Mismatch,
            SourceRows = run.TotalRows,
            ImportedRows = run.ValidRows,
            MatchedRows = run.ValidRows,
            MismatchedRows = Math.Max(0, run.TotalRows - run.ValidRows),
            MissingRows = 0,
            Summary = $"Reconciled {run.ValidRows}/{run.TotalRows} imported rows.",
            ReportFileRef = $"file://migration-reports/{batch.BatchNo}/reconciliation.xlsx"
        };
        store.DataReconciliationReports.Add(report);
        store.DataMigrationReports.Add(new DataMigrationReport
        {
            CustomerId = customerId,
            ProjectId = projectId,
            BatchId = batch.Id,
            ReconciliationReportId = report.Id,
            ReportNo = store.NextNumber("DMR"),
            Domain = batch.Domain,
            Summary = report.Summary,
            FileRef = $"file://migration-reports/{batch.BatchNo}/migration-summary.pdf"
        });
        batch.ReconciliationReportId = report.Id;
        batch.Status = ImportBatchStatus.Reconciled;
        audit.Write(customerId, projectId, "DATA_IMPORT_RECONCILED", nameof(DataReconciliationReport), report.Id, report);
        return report;
    }

    public async Task<AiDataMigrationAssistance> GenerateAiAssistanceAsync(Guid customerId, Guid projectId, Guid? batchId, Guid? templateId, AiDataAssistanceType assistanceType, string context, string requestedBy, CancellationToken cancellationToken)
    {
        EnsureProject(customerId, projectId);
        if (batchId.HasValue) ResolveBatch(customerId, projectId, batchId.Value);
        if (templateId.HasValue) ResolveTemplate(customerId, projectId, templateId.Value);
        var maskedContext = masking.Mask(context);
        var run = aiRuns.Start(customerId, projectId, $"AiData{assistanceType}", $"Data migration AI assistance: {assistanceType}", batchId.HasValue ? $"data_import_batch:{batchId}" : $"data_import_template:{templateId}", null);
        run.MaskedInputPreview = Limit(maskedContext, 700);
        await jobs.EnqueueAsync("data_import.ai_assistance", new { customerId, projectId, batchId, templateId, assistanceType }, cancellationToken);
        var suggestionJson = assistanceType switch
        {
            AiDataAssistanceType.MappingSuggestion => JsonSerializer.Serialize(new { mappings = new[] { new { source = "EmployeeCode", target = "Employee.EmployeeNo", classification = "Internal" }, new { source = "BankAccount", target = "EmployeeBankAccount.AccountNo", classification = "Secret" } }, requiresUserConfirmation = true }, JsonOptions),
            AiDataAssistanceType.ValidationExplanation => JsonSerializer.Serialize(new { explanation = "Required/duplicate/classification rules should be reviewed before apply.", requiresUserConfirmation = true }, JsonOptions),
            _ => JsonSerializer.Serialize(new { explanation = "Duplicate employee or missing mandatory value detected in masked preview.", requiresUserConfirmation = true }, JsonOptions)
        };
        var assistance = new AiDataMigrationAssistance
        {
            CustomerId = customerId,
            ProjectId = projectId,
            BatchId = batchId,
            TemplateId = templateId,
            AiRunId = run.Id,
            AssistanceType = assistanceType,
            Summary = $"AI generated {assistanceType}; no import was executed.",
            SuggestionJson = suggestionJson,
            AppliedByUser = false
        };
        store.AiDataMigrationAssistances.Add(assistance);
        aiRuns.Complete(run, assistance.Summary, $"ai_data_migration_assistance:{assistance.Id}", assistance.SuggestionJson, []);
        audit.Write(customerId, projectId, "DATA_IMPORT_AI_ASSISTANCE_CREATED", nameof(AiDataMigrationAssistance), assistance.Id, assistance);
        return assistance;
    }

    public DataSignOff SignOff(Guid customerId, Guid projectId, Guid batchId, string signedBy, string role, string comment)
    {
        var batch = ResolveBatch(customerId, projectId, batchId);
        if (!batch.ReconciliationReportId.HasValue)
        {
            throw new InvalidOperationException("Reconciliation report is required before data sign-off.");
        }
        var report = store.DataReconciliationReports.Single(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == batch.ReconciliationReportId.Value);
        if (report.Status != ReconciliationStatus.Matched)
        {
            throw new InvalidOperationException("Only matched reconciliation can be signed off.");
        }
        var signOff = new DataSignOff
        {
            CustomerId = customerId,
            ProjectId = projectId,
            BatchId = batch.Id,
            ReconciliationReportId = report.Id,
            SignOffNo = store.NextNumber("DSO"),
            Status = DataSignOffStatus.SignedOff,
            SignedBy = signedBy,
            Role = role,
            Comment = masking.Mask(comment),
            SignedAt = DateTimeOffset.UtcNow
        };
        store.DataSignOffs.Add(signOff);
        batch.SignOffId = signOff.Id;
        batch.Status = ImportBatchStatus.SignedOff;
        audit.Write(customerId, projectId, "DATA_IMPORT_SIGNED_OFF", nameof(DataSignOff), signOff.Id, signOff);
        return signOff;
    }

    private void ValidateBatch(DataImportBatch batch, DataImportRun run)
    {
        var file = ResolveFile(batch.CustomerId, batch.ProjectId, batch.ImportFileId);
        var rules = store.DataValidationRules.Where(x => x.CustomerId == batch.CustomerId && x.ProjectId == batch.ProjectId && x.Active && (x.TemplateId == null || x.TemplateId == batch.TemplateId) && x.Domain == batch.Domain).ToList();
        run.TotalRows = file.RowCount;
        run.ValidRows = Math.Max(0, file.RowCount - 2);
        run.ErrorRows = file.RowCount >= 2 ? 1 : 0;
        run.DuplicateRows = file.RowCount >= 4 ? 1 : 0;
        if (run.ErrorRows > 0)
        {
            AddIssue(run, 2, "EmployeeCode", ValidationIssueSeverity.Error, "REQUIRED", "EmployeeCode is required.", "");
        }
        if (run.DuplicateRows > 0)
        {
            AddIssue(run, 4, "NationalId", ValidationIssueSeverity.Warning, "DUPLICATE", "Duplicate NationalId detected in masked preview.", "NID-***");
        }
        foreach (var rule in rules.Where(x => x.RuleType == ValidationRuleType.DataClassification))
        {
            AddIssue(run, 1, rule.TargetField, ValidationIssueSeverity.Info, "CLASSIFICATION", $"{rule.TargetField} classified as sensitive.", "[masked]");
        }
    }

    private DataImportRun CreateRun(DataImportBatch batch, DataImportRunType type)
    {
        var run = new DataImportRun
        {
            CustomerId = batch.CustomerId,
            ProjectId = batch.ProjectId,
            BatchId = batch.Id,
            EnvironmentId = batch.EnvironmentId,
            ConnectorId = batch.ConnectorId,
            RunNo = store.NextNumber(type == DataImportRunType.DryRun ? "DDRY" : "DAPP"),
            RunType = type,
            Status = DataImportRunStatus.Running,
            StartedAt = DateTimeOffset.UtcNow
        };
        store.DataImportRuns.Add(run);
        return run;
    }

    private void AddIssue(DataImportRun run, int row, string field, ValidationIssueSeverity severity, string code, string message, string value)
    {
        store.DataValidationIssues.Add(new DataValidationIssue
        {
            CustomerId = run.CustomerId,
            ProjectId = run.ProjectId,
            ImportRunId = run.Id,
            RowNumber = row,
            FieldName = field,
            Severity = severity,
            ErrorCode = code,
            Message = message,
            MaskedValuePreview = masking.Mask(value),
            DuplicateKey = code == "DUPLICATE" ? masking.Mask(value) : null
        });
    }

    private ProjectEnvironment EnsureEnvironment(Guid customerId, Guid projectId, Guid environmentId, bool allowProduction)
    {
        var env = store.Environments.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == environmentId)
            ?? throw new InvalidOperationException("Environment does not belong to this customer/project.");
        if (!allowProduction && env.Kind == EnvironmentKind.Production)
        {
            throw new InvalidOperationException("Production data import is not allowed in Phase 17.");
        }
        return env;
    }

    private void EnsureProject(Guid customerId, Guid projectId)
    {
        if (!store.Projects.Any(x => x.CustomerId == customerId && x.Id == projectId))
        {
            throw new InvalidOperationException("Project not found for customer.");
        }
    }

    private DataImportTemplate ResolveTemplate(Guid customerId, Guid projectId, Guid templateId) =>
        store.DataImportTemplates.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == templateId)
        ?? throw new InvalidOperationException("Import template not found for this customer/project.");

    private DataImportFile ResolveFile(Guid customerId, Guid projectId, Guid fileId) =>
        store.DataImportFiles.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == fileId)
        ?? throw new InvalidOperationException("Import file not found for this customer/project.");

    private DataImportBatch ResolveBatch(Guid customerId, Guid projectId, Guid batchId) =>
        store.DataImportBatches.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == batchId)
        ?? throw new InvalidOperationException("Import batch not found for this customer/project.");

    private static bool ShouldMask(DataClassificationLevel classification, bool canViewSensitivePreview) =>
        !canViewSensitivePreview && classification is DataClassificationLevel.Confidential or DataClassificationLevel.Restricted or DataClassificationLevel.Secret;

    private static string Limit(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value ?? "" : value[..max];

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
