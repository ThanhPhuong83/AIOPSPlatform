using System.Text.Json;
using System.Text.Json.Serialization;
using HrmAiOps.Application.Abstractions;
using HrmAiOps.Domain.Core;
using HrmAiOps.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// CORS — restrict to configured origins; empty list = allow any (dev default)
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        else
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddInfrastructure();

var app = builder.Build();

// Global error handler — catches unhandled exceptions before they reach the client
app.UseExceptionHandler(exHandler =>
{
    exHandler.Run(async context =>
    {
        var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = ex is InvalidOperationException
            ? StatusCodes.Status422UnprocessableEntity
            : StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new
        {
            title = ex is InvalidOperationException ? "Business rule violation" : "An unexpected error occurred.",
            detail = ex?.Message,
            status = context.Response.StatusCode
        });
    });
});

app.UseCors();
app.MapOpenApi();

// Tenant isolation middleware
app.Use(async (context, next) =>
{
    if (TryReadCustomerIdFromPath(context.Request.Path.Value, out var routeCustomerId))
    {
        var store = context.RequestServices.GetRequiredService<IAppStore>();

        // Reject mismatched X-Customer-Id header
        var headerCustomer = context.Request.Headers["X-Customer-Id"].FirstOrDefault();
        if (Guid.TryParse(headerCustomer, out var headerCustomerId) && headerCustomerId != routeCustomerId)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "Tenant header does not match route customer." });
            return;
        }

        var actor = context.Request.Headers["X-User-Id"].FirstOrDefault();

        // Only block users who are explicitly Revoked or Suspended — new engineers have no grant yet and must be allowed
        if (!string.IsNullOrWhiteSpace(actor))
        {
            var grant = store.TenantAccessGrants.FirstOrDefault(x =>
                x.CustomerId == routeCustomerId &&
                string.Equals(x.UserId, actor, StringComparison.OrdinalIgnoreCase));
            if (grant is { Status: TenantAccessStatus.Revoked or TenantAccessStatus.Suspended })
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "User access has been revoked for this tenant." });
                return;
            }
        }
    }

    await next();
});

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "HRM AI Ops Platform" }));

app.MapGet("/api/customers", (IAppStore store, int page = 1, int pageSize = 50) =>
    Results.Ok(store.Customers.OrderBy(x => x.Name).ToPagedResult(page, pageSize)));

app.MapPost("/api/customers", (CreateCustomerRequest request, IAppStore store, IAuditWriter audit) =>
{
    if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new { error = "Customer code and name are required." });
    }

    if (store.Customers.Any(x => string.Equals(x.Code, request.Code, StringComparison.OrdinalIgnoreCase)))
    {
        return Results.Conflict(new { error = "Customer code already exists." });
    }

    var customer = new Customer
    {
        Code = request.Code.Trim(),
        Name = request.Name.Trim(),
        Industry = request.Industry,
        Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? "Asia/Bangkok" : request.Timezone
    };
    store.Customers.Add(customer);
    audit.Write(customer.Id, null, "CUSTOMER_CREATED", nameof(Customer), customer.Id, customer);
    return Results.Created($"/api/customers/{customer.Id}", customer);
});

app.MapGet("/api/customers/{customerId:guid}", (Guid customerId, IAppStore store) =>
    FindCustomer(store, customerId) is { } customer ? Results.Ok(customer) : Results.NotFound());

app.MapGet("/api/customers/{customerId:guid}/projects", (Guid customerId, IAppStore store, int page = 1, int pageSize = 50) =>
    FindCustomer(store, customerId) is null
        ? Results.NotFound()
        : Results.Ok(store.Projects.Where(x => x.CustomerId == customerId).OrderBy(x => x.Name).ToPagedResult(page, pageSize)));

app.MapPost("/api/customers/{customerId:guid}/projects", (Guid customerId, CreateProjectRequest request, IAppStore store, IAuditWriter audit) =>
{
    if (FindCustomer(store, customerId) is null)
    {
        return Results.NotFound(new { error = "Customer not found." });
    }

    if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new { error = "Project code and name are required." });
    }

    var project = new Project
    {
        CustomerId = customerId,
        Code = request.Code.Trim(),
        Name = request.Name.Trim(),
        Description = request.Description,
        HrmProductName = request.HrmProductName
    };
    store.Projects.Add(project);
    audit.Write(customerId, project.Id, "PROJECT_CREATED", nameof(Project), project.Id, project);
    return Results.Created($"/api/customers/{customerId}/projects/{project.Id}", project);
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}", (Guid customerId, Guid projectId, IAppStore store) =>
    FindProject(store, customerId, projectId) is { } project ? Results.Ok(project) : Results.NotFound());

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/environments", (Guid customerId, Guid projectId, IAppStore store) =>
    FindProject(store, customerId, projectId) is null
        ? Results.NotFound()
        : Results.Ok(store.Environments.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderBy(x => x.Kind)));

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/environments", (Guid customerId, Guid projectId, UpsertEnvironmentRequest request, IAppStore store, IAuditWriter audit) =>
{
    if (FindProject(store, customerId, projectId) is null)
    {
        return Results.NotFound(new { error = "Project not found." });
    }

    var environment = new ProjectEnvironment
    {
        CustomerId = customerId,
        ProjectId = projectId,
        Name = string.IsNullOrWhiteSpace(request.Name) ? request.Kind.ToString() : request.Name,
        Kind = request.Kind,
        BaseUrl = request.BaseUrl,
        RequiresApproval = request.RequiresApproval || request.Kind == EnvironmentKind.Production
    };
    store.Environments.Add(environment);
    audit.Write(customerId, projectId, "ENVIRONMENT_CREATED", nameof(ProjectEnvironment), environment.Id, environment);
    return Results.Created($"/api/customers/{customerId}/projects/{projectId}/environments/{environment.Id}", environment);
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/repositories", (Guid customerId, Guid projectId, IAppStore store) =>
    FindProject(store, customerId, projectId) is null
        ? Results.NotFound()
        : Results.Ok(store.SourceRepositories.Where(x => x.CustomerId == customerId && x.ProjectId == projectId)));

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/repositories", (Guid customerId, Guid projectId, RepositoryRequest request, IAppStore store, IAuditWriter audit) =>
{
    if (FindProject(store, customerId, projectId) is null)
    {
        return Results.NotFound(new { error = "Project not found." });
    }

    if (string.IsNullOrWhiteSpace(request.SecretRef))
    {
        return Results.BadRequest(new { error = "secret_ref is required. Do not store tokens in the database." });
    }

    var repo = new SourceRepository
    {
        CustomerId = customerId,
        ProjectId = projectId,
        EnvironmentId = request.EnvironmentId,
        Provider = request.Provider,
        RepoUrl = request.RepoUrl,
        DefaultBranch = request.DefaultBranch,
        SecretRef = request.SecretRef
    };
    store.SourceRepositories.Add(repo);
    audit.Write(customerId, projectId, "SOURCE_REPOSITORY_REGISTERED", nameof(SourceRepository), repo.Id, repo);
    return Results.Created($"/api/customers/{customerId}/projects/{projectId}/repositories/{repo.Id}", repo);
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/database-profiles", (Guid customerId, Guid projectId, IAppStore store) =>
    FindProject(store, customerId, projectId) is null
        ? Results.NotFound()
        : Results.Ok(store.DatabaseProfiles.Where(x => x.CustomerId == customerId && x.ProjectId == projectId)));

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/database-profiles", (Guid customerId, Guid projectId, DatabaseProfileRequest request, IAppStore store, IAuditWriter audit) =>
{
    if (FindEnvironment(store, customerId, projectId, request.EnvironmentId) is null)
    {
        return Results.BadRequest(new { error = "Environment does not belong to this customer/project." });
    }

    if (string.IsNullOrWhiteSpace(request.SecretRef))
    {
        return Results.BadRequest(new { error = "secret_ref is required. Do not store database passwords in the database." });
    }

    var profile = new DatabaseProfile
    {
        CustomerId = customerId,
        ProjectId = projectId,
        EnvironmentId = request.EnvironmentId,
        Engine = request.Engine,
        Host = request.Host,
        Port = request.Port,
        DatabaseName = request.DatabaseName,
        UsernameRef = request.UsernameRef,
        SecretRef = request.SecretRef,
        ReadOnly = request.ReadOnly
    };
    store.DatabaseProfiles.Add(profile);
    audit.Write(customerId, projectId, "DATABASE_PROFILE_REGISTERED", nameof(DatabaseProfile), profile.Id, profile);
    return Results.Created($"/api/customers/{customerId}/projects/{projectId}/database-profiles/{profile.Id}", profile);
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/requirements", (Guid customerId, Guid projectId, IAppStore store, int page = 1, int pageSize = 50) =>
    FindProject(store, customerId, projectId) is null
        ? Results.NotFound()
        : Results.Ok(store.Requirements.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt).ToPagedResult(page, pageSize)));

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/requirements/manual", (Guid customerId, Guid projectId, RequirementRequest request, IAppStore store, IAuditWriter audit) =>
{
    if (FindProject(store, customerId, projectId) is null)
    {
        return Results.NotFound(new { error = "Project not found." });
    }

    var requirement = new Requirement
    {
        CustomerId = customerId,
        ProjectId = projectId,
        RequirementNo = store.NextNumber("REQ"),
        Title = request.Title,
        ContentText = request.ContentText,
        SourceType = "Manual",
        CreatedBy = request.CreatedBy
    };
    requirement.VersionGroupId = requirement.Id;
    store.Requirements.Add(requirement);
    audit.Write(customerId, projectId, "REQUIREMENT_CREATED", nameof(Requirement), requirement.Id, requirement);
    return Results.Created($"/api/customers/{customerId}/projects/{projectId}/requirements/{requirement.Id}", requirement);
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/requirements/upload", (Guid customerId, Guid projectId, RequirementUploadRequest request, IAppStore store, IAuditWriter audit) =>
{
    if (FindProject(store, customerId, projectId) is null)
    {
        return Results.NotFound(new { error = "Project not found." });
    }

    var requirement = new Requirement
    {
        CustomerId = customerId,
        ProjectId = projectId,
        RequirementNo = store.NextNumber("REQ"),
        Title = request.Title,
        ContentText = request.ExtractedText,
        SourceType = "Upload",
        SourceFileRef = request.SourceFileRef,
        CreatedBy = request.CreatedBy
    };
    requirement.VersionGroupId = requirement.Id;
    store.Requirements.Add(requirement);
    audit.Write(customerId, projectId, "REQUIREMENT_UPLOADED", nameof(Requirement), requirement.Id, requirement);
    return Results.Created($"/api/customers/{customerId}/projects/{projectId}/requirements/{requirement.Id}", requirement);
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/requirements/{requirementId:guid}/generate-urs", async (Guid customerId, Guid projectId, Guid requirementId, IAppStore store, IAiTaskExecutor executor, CancellationToken cancellationToken) =>
{
    var requirement = store.Requirements.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == requirementId);
    if (requirement is null)
    {
        return Results.NotFound(new { error = "Requirement not found." });
    }

    var proposal = await executor.ExecuteAsync(new AiTaskExecutionRequest(customerId, projectId, AiTaskType.GenerateUrs, nameof(Requirement), requirementId), cancellationToken);
    return Results.Ok(proposal);
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/urs", (Guid customerId, Guid projectId, IAppStore store) =>
    Results.Ok(store.UrsDocuments.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt)));

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/urs/{ursId:guid}/generate-blueprint", async (Guid customerId, Guid projectId, Guid ursId, IAppStore store, IAiTaskExecutor executor, CancellationToken cancellationToken) =>
{
    var urs = store.UrsDocuments.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == ursId);
    if (urs is null)
    {
        return Results.NotFound(new { error = "URS not found." });
    }

    var proposal = await executor.ExecuteAsync(new AiTaskExecutionRequest(customerId, projectId, AiTaskType.GenerateBlueprint, nameof(UrsDocument), ursId), cancellationToken);
    return Results.Ok(proposal);
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/blueprints", (Guid customerId, Guid projectId, IAppStore store) =>
    Results.Ok(store.Blueprints.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt)));

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/blueprints/{blueprintId:guid}/generate-config-spec", async (Guid customerId, Guid projectId, Guid blueprintId, ConfigSpecGenerateRequest request, IAppStore store, IAiTaskExecutor executor, CancellationToken cancellationToken) =>
{
    var blueprint = store.Blueprints.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == blueprintId);
    if (blueprint is null)
    {
        return Results.NotFound(new { error = "Blueprint not found." });
    }

    var proposal = await executor.ExecuteAsync(new AiTaskExecutionRequest(customerId, projectId, AiTaskType.GenerateConfigSpec, nameof(Blueprint), blueprintId), cancellationToken);
    return Results.Ok(proposal);
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/config-specs", (Guid customerId, Guid projectId, IAppStore store) =>
    Results.Ok(store.ConfigSpecs.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt)));

app.MapGet("/api/hrm-modules", (IAppStore store) =>
    Results.Ok(store.HrmModules.OrderByDescending(x => x.DefaultRiskLevel).ThenBy(x => x.Name)));

app.MapPut("/api/customers/{customerId:guid}/projects/{projectId:guid}/requirements/{requirementId:guid}", (Guid customerId, Guid projectId, Guid requirementId, RequirementUpdateRequest request, IAppStore store, IAuditWriter audit) =>
{
    var requirement = store.Requirements.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == requirementId);
    if (requirement is null)
    {
        return Results.NotFound(new { error = "Requirement not found." });
    }

    if (requirement.Status == WorkStatus.Approved)
    {
        return Results.Conflict(new { error = "Approved documents are locked. Create a new version instead." });
    }

    requirement.Title = request.Title;
    requirement.ContentText = request.ContentText;
    requirement.Status = request.Status;
    requirement.UpdatedAt = DateTimeOffset.UtcNow;
    audit.Write(customerId, projectId, "REQUIREMENT_UPDATED", nameof(Requirement), requirement.Id, requirement);
    return Results.Ok(requirement);
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/requirements/{requirementId:guid}/versions", (Guid customerId, Guid projectId, Guid requirementId, RequirementUpdateRequest request, IAppStore store, IAuditWriter audit) =>
{
    var current = store.Requirements.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == requirementId);
    if (current is null)
    {
        return Results.NotFound(new { error = "Requirement not found." });
    }

    current.IsLatest = false;
    current.UpdatedAt = DateTimeOffset.UtcNow;
    var next = new Requirement
    {
        CustomerId = customerId,
        ProjectId = projectId,
        VersionGroupId = current.VersionGroupId == Guid.Empty ? current.Id : current.VersionGroupId,
        SupersedesDocumentId = current.Id,
        RequirementNo = current.RequirementNo,
        Title = request.Title,
        SourceType = current.SourceType,
        SourceFileRef = current.SourceFileRef,
        ContentText = request.ContentText,
        Status = WorkStatus.Draft,
        Version = current.Version + 1,
        CreatedBy = request.UpdatedBy
    };
    store.Requirements.Add(next);
    audit.Write(customerId, projectId, "REQUIREMENT_VERSION_CREATED", nameof(Requirement), next.Id, next);
    return Results.Created($"/api/customers/{customerId}/projects/{projectId}/requirements/{next.Id}", next);
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/requirements/{requirementId:guid}/sign-off", (Guid customerId, Guid projectId, Guid requirementId, SignOffRequest request, IAppStore store, IAuditWriter audit) =>
{
    var requirement = store.Requirements.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == requirementId);
    if (requirement is null)
    {
        return Results.NotFound(new { error = "Requirement not found." });
    }

    requirement.Status = WorkStatus.Approved;
    requirement.ApprovedBy = request.SignedOffBy;
    requirement.ApprovedAt = DateTimeOffset.UtcNow;
    requirement.UpdatedAt = DateTimeOffset.UtcNow;
    var signOff = NewSignOff(customerId, projectId, DocumentKind.Requirement, requirement.Id, requirement.Version, request);
    store.DocumentSignOffs.Add(signOff);
    audit.Write(customerId, projectId, "REQUIREMENT_SIGNED_OFF", nameof(Requirement), requirement.Id, requirement);
    return Results.Ok(new { document = requirement, signOff });
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/requirements/{requirementId:guid}/versions", (Guid customerId, Guid projectId, Guid requirementId, IAppStore store) =>
{
    var current = store.Requirements.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == requirementId);
    if (current is null)
    {
        return Results.NotFound(new { error = "Requirement not found." });
    }

    var groupId = current.VersionGroupId == Guid.Empty ? current.Id : current.VersionGroupId;
    return Results.Ok(store.Requirements.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && (x.VersionGroupId == groupId || x.Id == groupId)).OrderByDescending(x => x.Version));
});

app.MapDelete("/api/customers/{customerId:guid}/projects/{projectId:guid}/requirements/{requirementId:guid}", (Guid customerId, Guid projectId, Guid requirementId, IAppStore store, IAuditWriter audit) =>
{
    var document = store.Requirements.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == requirementId);
    if (document is null)
    {
        return Results.NotFound(new { error = "Requirement not found." });
    }

    if (document.Status == WorkStatus.Approved)
    {
        return Results.Conflict(new { error = "Approved documents are locked and cannot be archived directly." });
    }

    document.Status = WorkStatus.Archived;
    document.IsLatest = false;
    document.UpdatedAt = DateTimeOffset.UtcNow;
    audit.Write(customerId, projectId, "REQUIREMENT_ARCHIVED", nameof(Requirement), document.Id, document);
    return Results.Ok(document);
});

app.MapPut("/api/customers/{customerId:guid}/projects/{projectId:guid}/urs/{ursId:guid}", (Guid customerId, Guid projectId, Guid ursId, DocumentUpdateRequest request, IAppStore store, IAuditWriter audit) =>
{
    var document = store.UrsDocuments.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == ursId);
    if (document is null)
    {
        return Results.NotFound(new { error = "URS not found." });
    }

    if (document.Status == WorkStatus.Approved)
    {
        return Results.Conflict(new { error = "Approved documents are locked. Create a new version instead." });
    }

    document.Title = request.Title;
    document.Content = request.Content;
    document.Status = request.Status;
    document.UpdatedAt = DateTimeOffset.UtcNow;
    audit.Write(customerId, projectId, "URS_UPDATED", nameof(UrsDocument), document.Id, document);
    return Results.Ok(document);
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/urs/{ursId:guid}/versions", (Guid customerId, Guid projectId, Guid ursId, DocumentUpdateRequest request, IAppStore store, IAuditWriter audit) =>
{
    var current = store.UrsDocuments.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == ursId);
    if (current is null)
    {
        return Results.NotFound(new { error = "URS not found." });
    }

    current.IsLatest = false;
    var next = new UrsDocument
    {
        CustomerId = customerId,
        ProjectId = projectId,
        VersionGroupId = current.VersionGroupId == Guid.Empty ? current.Id : current.VersionGroupId,
        SupersedesDocumentId = current.Id,
        RequirementId = current.RequirementId,
        UrsNo = current.UrsNo,
        Title = request.Title,
        Content = request.Content,
        Status = WorkStatus.Draft,
        Version = current.Version + 1
    };
    store.UrsDocuments.Add(next);
    store.TraceLinks.Add(NewTrace(customerId, projectId, "Requirement", next.RequirementId, "UrsDocument", next.Id, "VersionedFromRequirement"));
    audit.Write(customerId, projectId, "URS_VERSION_CREATED", nameof(UrsDocument), next.Id, next);
    return Results.Created($"/api/customers/{customerId}/projects/{projectId}/urs/{next.Id}", next);
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/urs/{ursId:guid}/sign-off", (Guid customerId, Guid projectId, Guid ursId, SignOffRequest request, IAppStore store, IAuditWriter audit) =>
{
    var document = store.UrsDocuments.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == ursId);
    if (document is null)
    {
        return Results.NotFound(new { error = "URS not found." });
    }

    document.Status = WorkStatus.Approved;
    document.ApprovedBy = request.SignedOffBy;
    document.ApprovedAt = DateTimeOffset.UtcNow;
    var signOff = NewSignOff(customerId, projectId, DocumentKind.Urs, document.Id, document.Version, request);
    store.DocumentSignOffs.Add(signOff);
    audit.Write(customerId, projectId, "URS_SIGNED_OFF", nameof(UrsDocument), document.Id, document);
    return Results.Ok(new { document, signOff });
});

app.MapDelete("/api/customers/{customerId:guid}/projects/{projectId:guid}/urs/{ursId:guid}", (Guid customerId, Guid projectId, Guid ursId, IAppStore store, IAuditWriter audit) =>
{
    var document = store.UrsDocuments.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == ursId);
    if (document is null)
    {
        return Results.NotFound(new { error = "URS not found." });
    }

    if (document.Status == WorkStatus.Approved)
    {
        return Results.Conflict(new { error = "Approved documents are locked and cannot be archived directly." });
    }

    document.Status = WorkStatus.Archived;
    document.IsLatest = false;
    document.UpdatedAt = DateTimeOffset.UtcNow;
    audit.Write(customerId, projectId, "URS_ARCHIVED", nameof(UrsDocument), document.Id, document);
    return Results.Ok(document);
});

app.MapPut("/api/customers/{customerId:guid}/projects/{projectId:guid}/blueprints/{blueprintId:guid}", (Guid customerId, Guid projectId, Guid blueprintId, DocumentUpdateRequest request, IAppStore store, IAuditWriter audit) =>
{
    var document = store.Blueprints.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == blueprintId);
    if (document is null)
    {
        return Results.NotFound(new { error = "Blueprint not found." });
    }

    if (document.Status == WorkStatus.Approved)
    {
        return Results.Conflict(new { error = "Approved documents are locked. Create a new version instead." });
    }

    document.Type = request.Title;
    document.Content = request.Content;
    document.Status = request.Status;
    document.UpdatedAt = DateTimeOffset.UtcNow;
    audit.Write(customerId, projectId, "BLUEPRINT_UPDATED", nameof(Blueprint), document.Id, document);
    return Results.Ok(document);
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/blueprints/{blueprintId:guid}/versions", (Guid customerId, Guid projectId, Guid blueprintId, DocumentUpdateRequest request, IAppStore store, IAuditWriter audit) =>
{
    var current = store.Blueprints.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == blueprintId);
    if (current is null)
    {
        return Results.NotFound(new { error = "Blueprint not found." });
    }

    current.IsLatest = false;
    var next = new Blueprint
    {
        CustomerId = customerId,
        ProjectId = projectId,
        VersionGroupId = current.VersionGroupId == Guid.Empty ? current.Id : current.VersionGroupId,
        SupersedesDocumentId = current.Id,
        UrsId = current.UrsId,
        BlueprintNo = current.BlueprintNo,
        Type = request.Title,
        Content = request.Content,
        Status = WorkStatus.Draft,
        Version = current.Version + 1
    };
    store.Blueprints.Add(next);
    store.TraceLinks.Add(NewTrace(customerId, projectId, "UrsDocument", next.UrsId, "Blueprint", next.Id, "VersionedFromUrs"));
    audit.Write(customerId, projectId, "BLUEPRINT_VERSION_CREATED", nameof(Blueprint), next.Id, next);
    return Results.Created($"/api/customers/{customerId}/projects/{projectId}/blueprints/{next.Id}", next);
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/blueprints/{blueprintId:guid}/sign-off", (Guid customerId, Guid projectId, Guid blueprintId, SignOffRequest request, IAppStore store, IAuditWriter audit) =>
{
    var document = store.Blueprints.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == blueprintId);
    if (document is null)
    {
        return Results.NotFound(new { error = "Blueprint not found." });
    }

    document.Status = WorkStatus.Approved;
    document.ApprovedBy = request.SignedOffBy;
    document.ApprovedAt = DateTimeOffset.UtcNow;
    var signOff = NewSignOff(customerId, projectId, DocumentKind.Blueprint, document.Id, document.Version, request);
    store.DocumentSignOffs.Add(signOff);
    audit.Write(customerId, projectId, "BLUEPRINT_SIGNED_OFF", nameof(Blueprint), document.Id, document);
    return Results.Ok(new { document, signOff });
});

app.MapDelete("/api/customers/{customerId:guid}/projects/{projectId:guid}/blueprints/{blueprintId:guid}", (Guid customerId, Guid projectId, Guid blueprintId, IAppStore store, IAuditWriter audit) =>
{
    var document = store.Blueprints.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == blueprintId);
    if (document is null)
    {
        return Results.NotFound(new { error = "Blueprint not found." });
    }

    if (document.Status == WorkStatus.Approved)
    {
        return Results.Conflict(new { error = "Approved documents are locked and cannot be archived directly." });
    }

    document.Status = WorkStatus.Archived;
    document.IsLatest = false;
    document.UpdatedAt = DateTimeOffset.UtcNow;
    audit.Write(customerId, projectId, "BLUEPRINT_ARCHIVED", nameof(Blueprint), document.Id, document);
    return Results.Ok(document);
});

app.MapPut("/api/customers/{customerId:guid}/projects/{projectId:guid}/config-specs/{configSpecId:guid}", (Guid customerId, Guid projectId, Guid configSpecId, ConfigSpecUpdateRequest request, IAppStore store, IAuditWriter audit) =>
{
    var document = store.ConfigSpecs.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == configSpecId);
    if (document is null)
    {
        return Results.NotFound(new { error = "Config spec not found." });
    }

    if (document.Status == WorkStatus.Approved)
    {
        return Results.Conflict(new { error = "Approved documents are locked. Create a new version instead." });
    }

    document.ModuleName = request.ModuleName;
    document.RiskLevel = ResolveRiskLevel(store, request.ModuleName, request.RiskLevel);
    document.Content = request.Content;
    document.Status = request.Status;
    document.UpdatedAt = DateTimeOffset.UtcNow;
    audit.Write(customerId, projectId, "CONFIG_SPEC_UPDATED", nameof(ConfigSpec), document.Id, document);
    return Results.Ok(document);
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/config-specs/{configSpecId:guid}/versions", (Guid customerId, Guid projectId, Guid configSpecId, ConfigSpecUpdateRequest request, IAppStore store, IAuditWriter audit) =>
{
    var current = store.ConfigSpecs.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == configSpecId);
    if (current is null)
    {
        return Results.NotFound(new { error = "Config spec not found." });
    }

    current.IsLatest = false;
    var next = new ConfigSpec
    {
        CustomerId = customerId,
        ProjectId = projectId,
        VersionGroupId = current.VersionGroupId == Guid.Empty ? current.Id : current.VersionGroupId,
        SupersedesDocumentId = current.Id,
        EnvironmentId = current.EnvironmentId,
        BlueprintId = current.BlueprintId,
        ConfigNo = current.ConfigNo,
        ModuleName = request.ModuleName,
        RiskLevel = ResolveRiskLevel(store, request.ModuleName, request.RiskLevel),
        Content = request.Content,
        Status = WorkStatus.Draft,
        Version = current.Version + 1
    };
    store.ConfigSpecs.Add(next);
    store.TraceLinks.Add(NewTrace(customerId, projectId, "Blueprint", next.BlueprintId, "ConfigSpec", next.Id, "VersionedFromBlueprint"));
    audit.Write(customerId, projectId, "CONFIG_SPEC_VERSION_CREATED", nameof(ConfigSpec), next.Id, next);
    return Results.Created($"/api/customers/{customerId}/projects/{projectId}/config-specs/{next.Id}", next);
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/config-specs/{configSpecId:guid}/sign-off", (Guid customerId, Guid projectId, Guid configSpecId, SignOffRequest request, IAppStore store, IAuditWriter audit) =>
{
    var document = store.ConfigSpecs.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == configSpecId);
    if (document is null)
    {
        return Results.NotFound(new { error = "Config spec not found." });
    }

    document.Status = WorkStatus.Approved;
    document.ApprovedBy = request.SignedOffBy;
    document.ApprovedAt = DateTimeOffset.UtcNow;
    var signOff = NewSignOff(customerId, projectId, DocumentKind.ConfigSpec, document.Id, document.Version, request);
    store.DocumentSignOffs.Add(signOff);
    audit.Write(customerId, projectId, "CONFIG_SPEC_SIGNED_OFF", nameof(ConfigSpec), document.Id, document);
    return Results.Ok(new { document, signOff });
});

app.MapDelete("/api/customers/{customerId:guid}/projects/{projectId:guid}/config-specs/{configSpecId:guid}", (Guid customerId, Guid projectId, Guid configSpecId, IAppStore store, IAuditWriter audit) =>
{
    var document = store.ConfigSpecs.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == configSpecId);
    if (document is null)
    {
        return Results.NotFound(new { error = "Config spec not found." });
    }

    if (document.Status == WorkStatus.Approved)
    {
        return Results.Conflict(new { error = "Approved documents are locked and cannot be archived directly." });
    }

    document.Status = WorkStatus.Archived;
    document.IsLatest = false;
    document.UpdatedAt = DateTimeOffset.UtcNow;
    audit.Write(customerId, projectId, "CONFIG_SPEC_ARCHIVED", nameof(ConfigSpec), document.Id, document);
    return Results.Ok(document);
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/document-signoffs", (Guid customerId, Guid projectId, IAppStore store) =>
    Results.Ok(store.DocumentSignOffs.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.SignedOffAt)));

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/issues", (Guid customerId, Guid projectId, IAppStore store, int page = 1, int pageSize = 50) =>
    Results.Ok(store.Issues.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt).ToPagedResult(page, pageSize)));

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/issues", (Guid customerId, Guid projectId, IssueRequest request, IAppStore store, IAuditWriter audit) =>
{
    if (FindProject(store, customerId, projectId) is null)
    {
        return Results.NotFound(new { error = "Project not found." });
    }

    var issue = new Issue
    {
        CustomerId = customerId,
        ProjectId = projectId,
        EnvironmentId = request.EnvironmentId,
        LinkedEntityType = request.LinkedEntityType,
        LinkedEntityId = request.LinkedEntityId,
        IssueNo = store.NextNumber("ISS"),
        Title = request.Title,
        Description = request.Description,
        Category = request.Category ?? InferIssueCategory(request.Title, request.Description),
        RiskLevel = ResolveIssueRisk(request.Title, request.Description, request.Category),
        Severity = request.Severity,
        Priority = request.Priority,
        ReportedBy = request.ReportedBy
    };
    store.Issues.Add(issue);
    if (!string.IsNullOrWhiteSpace(issue.LinkedEntityType) && issue.LinkedEntityId.HasValue)
    {
        store.TraceLinks.Add(NewTrace(customerId, projectId, issue.LinkedEntityType, issue.LinkedEntityId.Value, nameof(Issue), issue.Id, "IssueLinked"));
    }
    audit.Write(customerId, projectId, "ISSUE_CREATED", nameof(Issue), issue.Id, issue);
    return Results.Created($"/api/customers/{customerId}/projects/{projectId}/issues/{issue.Id}", issue);
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/issues/{issueId:guid}/classify", async (Guid customerId, Guid projectId, Guid issueId, IAppStore store, IAiTaskExecutor executor, CancellationToken cancellationToken) =>
    await ExecuteIssueAi(customerId, projectId, issueId, AiTaskType.ClassifyIssue, store, executor, cancellationToken));

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/issues/{issueId:guid}/root-cause", async (Guid customerId, Guid projectId, Guid issueId, IAppStore store, IAiTaskExecutor executor, CancellationToken cancellationToken) =>
    await ExecuteIssueAi(customerId, projectId, issueId, AiTaskType.AnalyzeRootCause, store, executor, cancellationToken));

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/issues/{issueId:guid}/fix-proposal", async (Guid customerId, Guid projectId, Guid issueId, IAppStore store, IAiTaskExecutor executor, CancellationToken cancellationToken) =>
    await ExecuteIssueAi(customerId, projectId, issueId, AiTaskType.GenerateFixProposal, store, executor, cancellationToken));

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/issues/{issueId:guid}/change-request-draft", async (Guid customerId, Guid projectId, Guid issueId, IAppStore store, IAiTaskExecutor executor, CancellationToken cancellationToken) =>
    await ExecuteIssueAi(customerId, projectId, issueId, AiTaskType.GenerateChangeRequest, store, executor, cancellationToken));

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/issues/{issueId:guid}/regression-test-plan", async (Guid customerId, Guid projectId, Guid issueId, IAppStore store, IAiTaskExecutor executor, CancellationToken cancellationToken) =>
    await ExecuteIssueAi(customerId, projectId, issueId, AiTaskType.GenerateRegressionTestPlan, store, executor, cancellationToken));

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/issues/{issueId:guid}/release-draft", async (Guid customerId, Guid projectId, Guid issueId, IAppStore store, IAiTaskExecutor executor, CancellationToken cancellationToken) =>
    await ExecuteIssueAi(customerId, projectId, issueId, AiTaskType.GenerateReleaseDraft, store, executor, cancellationToken));

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/issues/{issueId:guid}/knowledge-update", async (Guid customerId, Guid projectId, Guid issueId, IAppStore store, IAiTaskExecutor executor, CancellationToken cancellationToken) =>
    await ExecuteIssueAi(customerId, projectId, issueId, AiTaskType.GenerateKnowledgeUpdate, store, executor, cancellationToken));

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/issues/{issueId:guid}/close", (Guid customerId, Guid projectId, Guid issueId, CloseIssueRequest request, IAppStore store, IAuditWriter audit) =>
{
    var issue = store.Issues.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == issueId);
    if (issue is null)
    {
        return Results.NotFound(new { error = "Issue not found." });
    }

    issue.Status = IssueStatus.Closed;
    issue.UpdatedAt = DateTimeOffset.UtcNow;
    audit.Write(customerId, projectId, "ISSUE_CLOSED", nameof(Issue), issue.Id, new { issue, request.ResolutionNote, request.ClosedBy });
    return Results.Ok(issue);
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/issues/{issueId:guid}/diagnose", async (Guid customerId, Guid projectId, Guid issueId, IAppStore store, IAiProvider ai, IAiRunRecorder aiRuns, IAuditWriter audit, CancellationToken cancellationToken) =>
{
    var issue = store.Issues.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == issueId);
    if (issue is null)
    {
        return Results.NotFound(new { error = "Issue not found." });
    }

    var context = $"{issue.Title}\n{issue.Description}\nSeverity: {issue.Severity}\nEnvironment: {issue.EnvironmentId}";
    var run = aiRuns.Start(customerId, projectId, "diagnosis", context);
    var content = await ai.GenerateAsync("diagnosis", context, cancellationToken);
    var analysis = new IssueAnalysis
    {
        CustomerId = customerId,
        ProjectId = projectId,
        IssueId = issue.Id,
        AiRunId = run.Id,
        Content = content,
        ConfidenceScore = 0.72m
    };
    store.IssueAnalyses.Add(analysis);
    issue.RootCauseSummary = content.Length > 300 ? content[..300] : content;
    issue.UpdatedAt = DateTimeOffset.UtcNow;
    store.TraceLinks.Add(NewTrace(customerId, projectId, "Issue", issue.Id, "IssueAnalysis", analysis.Id));
    aiRuns.Complete(run, content, $"issue_analysis:{analysis.Id}");
    audit.Write(customerId, projectId, "ISSUE_DIAGNOSED", nameof(IssueAnalysis), analysis.Id, analysis);
    return Results.Ok(analysis);
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/issues/{issueId:guid}/legacy-fix-proposal", async (Guid customerId, Guid projectId, Guid issueId, IAppStore store, IAiProvider ai, IAiRunRecorder aiRuns, IAuditWriter audit, CancellationToken cancellationToken) =>
{
    var issue = store.Issues.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == issueId);
    if (issue is null)
    {
        return Results.NotFound(new { error = "Issue not found." });
    }

    var latestAnalysis = store.IssueAnalyses.LastOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.IssueId == issueId);
    var input = $"{issue.Title}\n{issue.Description}\nRoot cause: {latestAnalysis?.Content ?? issue.RootCauseSummary ?? "Not diagnosed yet."}";
    var run = aiRuns.Start(customerId, projectId, "fix_proposal", input);
    var content = await ai.GenerateAsync("fix_proposal", input, cancellationToken);
    var proposal = new FixProposal
    {
        CustomerId = customerId,
        ProjectId = projectId,
        IssueId = issue.Id,
        AiRunId = run.Id,
        Title = $"Fix - {issue.Title}",
        ProposedSolution = content,
        CodeChangeSummary = "Review impacted service/module and apply targeted patch.",
        DbChangeSummary = "No direct schema change proposed by local stub.",
        RiskLevel = issue.RiskLevel
    };
    store.FixProposals.Add(proposal);
    store.TraceLinks.Add(NewTrace(customerId, projectId, "Issue", issue.Id, "FixProposal", proposal.Id));
    aiRuns.Complete(run, content, $"fix_proposal:{proposal.Id}");
    audit.Write(customerId, projectId, "FIX_PROPOSAL_GENERATED", nameof(FixProposal), proposal.Id, proposal);
    return Results.Ok(proposal);
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/change-requests", (Guid customerId, Guid projectId, ChangeRequestRequest request, IAppStore store, IAuditWriter audit) =>
{
    var environment = FindEnvironment(store, customerId, projectId, request.TargetEnvironmentId);
    if (environment is null)
    {
        return Results.BadRequest(new { error = "Target environment does not belong to this customer/project." });
    }

    var change = new ChangeRequest
    {
        CustomerId = customerId,
        ProjectId = projectId,
        IssueId = request.IssueId,
        FixProposalId = request.FixProposalId,
        CrNo = store.NextNumber("CR"),
        Title = request.Title,
        Description = request.Description,
        TargetEnvironmentId = request.TargetEnvironmentId,
        RequiresApproval = environment.Kind == EnvironmentKind.Production || environment.RequiresApproval
    };
    store.ChangeRequests.Add(change);
    if (request.IssueId.HasValue)
    {
        store.TraceLinks.Add(NewTrace(customerId, projectId, "Issue", request.IssueId.Value, "ChangeRequest", change.Id));
    }
    audit.Write(customerId, projectId, "CHANGE_REQUEST_CREATED", nameof(ChangeRequest), change.Id, change);
    return Results.Created($"/api/customers/{customerId}/projects/{projectId}/change-requests/{change.Id}", change);
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/change-requests/{changeRequestId:guid}/submit-approval", (Guid customerId, Guid projectId, Guid changeRequestId, SubmitApprovalRequest request, IAppStore store, IAuditWriter audit) =>
{
    var change = store.ChangeRequests.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == changeRequestId);
    if (change is null)
    {
        return Results.NotFound(new { error = "Change request not found." });
    }

    var approval = new ApprovalRequest
    {
        CustomerId = customerId,
        ProjectId = projectId,
        EntityType = nameof(ChangeRequest),
        EntityId = change.Id,
        TargetEnvironmentId = change.TargetEnvironmentId,
        RequestedBy = request.RequestedBy
    };
    store.ApprovalRequests.Add(approval);
    store.ApprovalSteps.Add(new ApprovalStep
    {
        CustomerId = customerId,
        ApprovalRequestId = approval.Id,
        StepOrder = 1,
        ApproverUserId = string.IsNullOrWhiteSpace(request.ApproverUserId) ? "platform-admin" : request.ApproverUserId
    });
    change.Status = WorkStatus.InReview;
    store.TraceLinks.Add(NewTrace(customerId, projectId, "ChangeRequest", change.Id, "ApprovalRequest", approval.Id));
    audit.Write(customerId, projectId, "APPROVAL_SUBMITTED", nameof(ApprovalRequest), approval.Id, approval);
    return Results.Ok(approval);
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/approvals", (Guid customerId, Guid projectId, IAppStore store) =>
    Results.Ok(store.ApprovalRequests.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.RequestedAt)));

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/approvals/{approvalId:guid}/approve", (Guid customerId, Guid projectId, Guid approvalId, ApprovalActionRequest request, IAppStore store, IAuditWriter audit) =>
{
    var approval = store.ApprovalRequests.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == approvalId);
    if (approval is null)
    {
        return Results.NotFound(new { error = "Approval not found." });
    }

    approval.Status = ApprovalStatus.Approved;
    approval.CompletedAt = DateTimeOffset.UtcNow;
    foreach (var step in store.ApprovalSteps.Where(x => x.CustomerId == customerId && x.ApprovalRequestId == approvalId))
    {
        step.Status = ApprovalStatus.Approved;
        step.Comment = request.Comment;
        step.ActedAt = DateTimeOffset.UtcNow;
    }
    audit.Write(customerId, projectId, "APPROVAL_APPROVED", nameof(ApprovalRequest), approval.Id, approval);
    return Results.Ok(approval);
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/releases", (Guid customerId, Guid projectId, ReleaseRequest request, IAppStore store, IAuditWriter audit) =>
{
    var environment = FindEnvironment(store, customerId, projectId, request.TargetEnvironmentId);
    if (environment is null)
    {
        return Results.BadRequest(new { error = "Target environment does not belong to this customer/project." });
    }

    if (environment.Kind == EnvironmentKind.Production)
    {
        return Results.Conflict(new { error = "Legacy release creation cannot target Production. Use the Phase 7 controlled ProductionReleasePackage workflow." });
    }

    var release = new Release
    {
        CustomerId = customerId,
        ProjectId = projectId,
        ReleaseNo = store.NextNumber("REL"),
        ChangeRequestId = request.ChangeRequestId,
        TargetEnvironmentId = request.TargetEnvironmentId,
        Version = request.Version,
        ReleaseNotes = request.ReleaseNotes,
        DeploymentPlan = request.DeploymentPlan,
        Status = ReleaseStatus.Planned
    };
    store.Releases.Add(release);
    if (request.ChangeRequestId.HasValue)
    {
        store.TraceLinks.Add(NewTrace(customerId, projectId, "ChangeRequest", request.ChangeRequestId.Value, "Release", release.Id));
    }
    audit.Write(customerId, projectId, "RELEASE_CREATED", nameof(Release), release.Id, release);
    return Results.Created($"/api/customers/{customerId}/projects/{projectId}/releases/{release.Id}", release);
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/releases/{releaseId:guid}/deploy", (Guid customerId, Guid projectId, Guid releaseId, DeployReleaseRequest request, IAppStore store, IAuditWriter audit) =>
{
    var release = store.Releases.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == releaseId);
    if (release is null)
    {
        return Results.NotFound(new { error = "Release not found." });
    }

    var environment = FindEnvironment(store, customerId, projectId, release.TargetEnvironmentId);
    if (environment?.Kind == EnvironmentKind.Production)
    {
        return Results.Conflict(new { error = "Legacy deploy endpoint cannot deploy Production. Use the Phase 7 controlled production deployment workflow." });
    }

    var rollback = new RollbackPoint
    {
        CustomerId = customerId,
        ProjectId = projectId,
        ReleaseId = release.Id,
        EnvironmentId = release.TargetEnvironmentId,
        SourceCommit = request.SourceCommit,
        ArtifactRef = request.ArtifactRef,
        DatabaseBackupRef = request.DatabaseBackupRef,
        ConfigSnapshotRef = request.ConfigSnapshotRef
    };
    store.RollbackPoints.Add(rollback);
    release.Status = ReleaseStatus.Deployed;
    release.DeployedAt = DateTimeOffset.UtcNow;
    audit.Write(customerId, projectId, "RELEASE_DEPLOYED", nameof(Release), release.Id, release);
    return Results.Ok(new { release, rollbackPoint = rollback });
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/releases", (Guid customerId, Guid projectId, IAppStore store) =>
    Results.Ok(store.Releases.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt)));

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/fix-proposals", (Guid customerId, Guid projectId, IAppStore store) =>
    Results.Ok(store.FixProposals.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt)));

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/regression-test-plans", (Guid customerId, Guid projectId, IAppStore store) =>
    Results.Ok(store.RegressionTestPlans.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt)));

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/release-drafts", (Guid customerId, Guid projectId, IAppStore store) =>
    Results.Ok(store.ReleaseDrafts.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt)));

app.MapPost("/api/customers/{customerId:guid}/knowledge-base", (Guid customerId, KnowledgeArticleRequest request, IAppStore store, IAuditWriter audit) =>
{
    if (FindCustomer(store, customerId) is null)
    {
        return Results.NotFound(new { error = "Customer not found." });
    }

    var article = new KnowledgeArticle
    {
        CustomerId = customerId,
        ProjectId = request.ProjectId,
        IssueId = request.IssueId,
        Title = request.Title,
        Category = request.Category,
        Content = request.Content
    };
    store.KnowledgeArticles.Add(article);
    audit.Write(customerId, request.ProjectId, "KNOWLEDGE_ARTICLE_CREATED", nameof(KnowledgeArticle), article.Id, article);
    return Results.Created($"/api/customers/{customerId}/knowledge-base/{article.Id}", article);
});

app.MapGet("/api/customers/{customerId:guid}/knowledge-base", (Guid customerId, IAppStore store, string? q, int page = 1, int pageSize = 50) =>
{
    var articles = store.KnowledgeArticles.Where(x => x.CustomerId == customerId);
    if (!string.IsNullOrWhiteSpace(q))
        articles = articles.Where(x => x.Title.Contains(q, StringComparison.OrdinalIgnoreCase) || x.Content.Contains(q, StringComparison.OrdinalIgnoreCase));
    return Results.Ok(articles.OrderByDescending(x => x.CreatedAt).ToPagedResult(page, pageSize));
});

app.MapGet("/api/customers/{customerId:guid}/ai-runs", (Guid customerId, IAppStore store, int page = 1, int pageSize = 50) =>
    Results.Ok(store.AiRuns.Where(x => x.CustomerId == customerId).OrderByDescending(x => x.StartedAt).ToPagedResult(page, pageSize)));

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/knowledge-learning", (Guid customerId, Guid projectId, IAppStore store, string? module, KnowledgeLifecycleStatus? status) =>
{
    if (FindProject(store, customerId, projectId) is null) return Results.NotFound(new { error = "Project not found." });
    var query = store.KnowledgeLearningItems.Where(x => x.CustomerId == customerId && x.ProjectId == projectId);
    if (!string.IsNullOrWhiteSpace(module)) query = query.Where(x => x.ModuleName.Contains(module, StringComparison.OrdinalIgnoreCase));
    if (status.HasValue) query = query.Where(x => x.Status == status.Value);
    return Results.Ok(query.OrderByDescending(x => x.CreatedAt));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/knowledge-learning/generate-from-operations", (Guid customerId, Guid projectId, GenerateLearningRequest request, IAppStore store, IAuditWriter audit) =>
{
    if (FindProject(store, customerId, projectId) is null) return Results.NotFound(new { error = "Project not found." });

    var aiRun = NewGovernanceAiRun(customerId, projectId, AiTaskType.GenerateLessonsLearned, "Issue/release/validation/rollback/post-release closure sources", "Masked operational summaries only.");
    store.AiRuns.Add(aiRun);
    var created = GenerateKnowledgeLearningItems(store, audit, customerId, projectId, aiRun, request.AllowLowRiskAutoApprove);
    aiRun.OutputSummary = $"Generated {created.Count} knowledge learning item(s).";
    aiRun.OutputRef = "KnowledgeLearningItem";
    aiRun.Status = AiRunStatus.Completed;
    aiRun.CompletedAt = DateTimeOffset.UtcNow;
    audit.Write(customerId, projectId, "KNOWLEDGE_LEARNING_GENERATED", nameof(KnowledgeLearningItem), aiRun.Id, created);
    return Results.Ok(new { aiRun, created });
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/knowledge-learning/{knowledgeId:guid}/approve", (Guid customerId, Guid projectId, Guid knowledgeId, ReviewKnowledgeRequest request, IAppStore store, IAuditWriter audit) =>
{
    var item = store.KnowledgeLearningItems.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == knowledgeId);
    if (item is null) return Results.NotFound(new { error = "Knowledge item not found." });
    item.Status = KnowledgeLifecycleStatus.Approved;
    item.ReviewedBy = request.ReviewedBy;
    item.ReviewedAt = DateTimeOffset.UtcNow;
    item.UpdatedAt = DateTimeOffset.UtcNow;
    audit.Write(customerId, projectId, "KNOWLEDGE_LEARNING_APPROVED", nameof(KnowledgeLearningItem), item.Id, item);
    return Results.Ok(item);
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/knowledge-learning/{knowledgeId:guid}/reject", (Guid customerId, Guid projectId, Guid knowledgeId, ReviewKnowledgeRequest request, IAppStore store, IAuditWriter audit) =>
{
    var item = store.KnowledgeLearningItems.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == knowledgeId);
    if (item is null) return Results.NotFound(new { error = "Knowledge item not found." });
    item.Status = KnowledgeLifecycleStatus.Rejected;
    item.ReviewedBy = request.ReviewedBy;
    item.ReviewedAt = DateTimeOffset.UtcNow;
    item.ExplainabilityJson = JsonSerializer.Serialize(new { item.ExplainabilityJson, rejectComment = request.Comment });
    item.UpdatedAt = DateTimeOffset.UtcNow;
    audit.Write(customerId, projectId, "KNOWLEDGE_LEARNING_REJECTED", nameof(KnowledgeLearningItem), item.Id, item);
    return Results.Ok(item);
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/knowledge-learning/{knowledgeId:guid}/supersede", (Guid customerId, Guid projectId, Guid knowledgeId, SupersedeKnowledgeRequest request, IAppStore store, IAuditWriter audit) =>
{
    var current = store.KnowledgeLearningItems.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == knowledgeId);
    if (current is null) return Results.NotFound(new { error = "Knowledge item not found." });
    current.Status = KnowledgeLifecycleStatus.Superseded;
    current.UpdatedAt = DateTimeOffset.UtcNow;
    var next = new KnowledgeLearningItem
    {
        CustomerId = customerId,
        ProjectId = projectId,
        AiRunId = current.AiRunId,
        SourceType = current.SourceType,
        SourceEntityType = current.SourceEntityType,
        SourceEntityId = current.SourceEntityId,
        SourceSummary = current.SourceSummary,
        KnowledgeNo = current.KnowledgeNo,
        Title = request.Title,
        Category = current.Category,
        ModuleName = current.ModuleName,
        Content = request.Content,
        LessonsLearned = request.LessonsLearned,
        RiskLevel = current.RiskLevel,
        Status = KnowledgeLifecycleStatus.PendingReview,
        VersionGroupId = current.VersionGroupId == Guid.Empty ? current.Id : current.VersionGroupId,
        SupersedesKnowledgeItemId = current.Id,
        Version = current.Version + 1,
        ExplainabilityJson = JsonSerializer.Serialize(new { reason = "Manual supersede", supersededId = current.Id })
    };
    store.KnowledgeLearningItems.Add(next);
    store.TraceLinks.Add(NewTrace(customerId, projectId, nameof(KnowledgeLearningItem), current.Id, nameof(KnowledgeLearningItem), next.Id, "SupersededBy"));
    audit.Write(customerId, projectId, "KNOWLEDGE_LEARNING_SUPERSEDED", nameof(KnowledgeLearningItem), next.Id, next);
    return Results.Created($"/api/customers/{customerId}/projects/{projectId}/knowledge-learning/{next.Id}", next);
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/knowledge-learning/{knowledgeId:guid}/expire", (Guid customerId, Guid projectId, Guid knowledgeId, ReviewKnowledgeRequest request, IAppStore store, IAuditWriter audit) =>
{
    var item = store.KnowledgeLearningItems.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == knowledgeId);
    if (item is null) return Results.NotFound(new { error = "Knowledge item not found." });
    item.Status = KnowledgeLifecycleStatus.Expired;
    item.ReviewedBy = request.ReviewedBy;
    item.ReviewedAt = DateTimeOffset.UtcNow;
    item.ExpiresAt = DateTimeOffset.UtcNow;
    item.UpdatedAt = DateTimeOffset.UtcNow;
    audit.Write(customerId, projectId, "KNOWLEDGE_LEARNING_EXPIRED", nameof(KnowledgeLearningItem), item.Id, item);
    return Results.Ok(item);
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/governance-analytics/recalculate", (Guid customerId, Guid projectId, RecalculateGovernanceRequest request, IAppStore store, IAuditWriter audit) =>
{
    if (FindProject(store, customerId, projectId) is null) return Results.NotFound(new { error = "Project not found." });

    var aiRun = NewGovernanceAiRun(customerId, projectId, AiTaskType.CalculateGovernanceScores, "Project-scoped operational metrics", "No sensitive transaction data included.");
    store.AiRuns.Add(aiRun);
    var patterns = DetectRepeatedIssuePatterns(store, customerId, projectId, aiRun.Id);
    var scores = RecalculateGovernanceScores(store, customerId, projectId, aiRun.Id, request.ModuleName);
    var aiMetric = RecalculateAiPerformanceMetric(store, customerId, projectId);
    aiRun.OutputSummary = $"Calculated {scores.Count} score(s), {patterns.Count} repeated pattern(s), AI quality {aiMetric.QualityScore:0.##}.";
    aiRun.Status = AiRunStatus.Completed;
    aiRun.CompletedAt = DateTimeOffset.UtcNow;
    audit.Write(customerId, projectId, "GOVERNANCE_ANALYTICS_RECALCULATED", nameof(GovernanceScoreSnapshot), aiRun.Id, new { scores, patterns, aiMetric });
    return Results.Ok(new { aiRun, scores, patterns, aiMetric });
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/governance-analytics", (Guid customerId, Guid projectId, IAppStore store, string? module, DateTimeOffset? from, DateTimeOffset? to) =>
{
    if (FindProject(store, customerId, projectId) is null) return Results.NotFound(new { error = "Project not found." });
    var issues = FilterDate(store.Issues.Where(x => x.CustomerId == customerId && x.ProjectId == projectId), from, to).ToList();
    var knowledge = FilterDate(store.KnowledgeLearningItems.Where(x => x.CustomerId == customerId && x.ProjectId == projectId), from, to);
    var scores = FilterDate(store.GovernanceScoreSnapshots.Where(x => x.CustomerId == customerId && x.ProjectId == projectId), from, to);
    var patterns = FilterDate(store.RepeatedIssuePatterns.Where(x => x.CustomerId == customerId && x.ProjectId == projectId), from, to);
    var insights = FilterDate(store.GovernanceInsights.Where(x => x.CustomerId == customerId && x.ProjectId == projectId), from, to);
    if (!string.IsNullOrWhiteSpace(module))
    {
        knowledge = knowledge.Where(x => x.ModuleName.Contains(module, StringComparison.OrdinalIgnoreCase));
        scores = scores.Where(x => x.ModuleName == null || x.ModuleName.Contains(module, StringComparison.OrdinalIgnoreCase));
        patterns = patterns.Where(x => x.ModuleName.Contains(module, StringComparison.OrdinalIgnoreCase));
        insights = insights.Where(x => x.ModuleName.Contains(module, StringComparison.OrdinalIgnoreCase));
        issues = issues.Where(x => ResolveModuleName(store, x).Contains(module, StringComparison.OrdinalIgnoreCase)).ToList();
    }
    return Results.Ok(new
    {
        filters = new { customerId, projectId, module, from, to },
        summary = new
        {
            totalIssues = issues.Count,
            openIssues = issues.Count(x => x.Status != IssueStatus.Closed),
            approvedKnowledge = knowledge.Count(x => x.Status == KnowledgeLifecycleStatus.Approved),
            pendingKnowledge = knowledge.Count(x => x.Status == KnowledgeLifecycleStatus.PendingReview),
            repeatedPatterns = patterns.Count(),
            scoreCount = scores.Count()
        },
        scores = scores.OrderByDescending(x => x.CalculatedAt),
        repeatedIssuePatterns = patterns.OrderByDescending(x => x.IssueCount),
        insights = insights.OrderByDescending(x => x.CreatedAt),
        knowledge = knowledge.OrderByDescending(x => x.CreatedAt),
        aiPerformance = store.AiPerformanceMetrics.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CalculatedAt).FirstOrDefault(),
        formulas = GovernanceFormulaCatalog()
    });
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/governance-analytics/scores", (Guid customerId, Guid projectId, IAppStore store) =>
    Results.Ok(store.GovernanceScoreSnapshots.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CalculatedAt)));

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/repeated-issue-patterns", (Guid customerId, Guid projectId, IAppStore store) =>
    Results.Ok(store.RepeatedIssuePatterns.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.IssueCount)));

app.MapGet("/api/customers/{customerId:guid}/security/dashboard", (Guid customerId, IAppStore store, HttpContext http, Guid? projectId) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "security.view", out var error)) return error;
    var auditLogs = store.AuditLogs.Where(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == projectId)).ToList();
    var secretAccess = store.SecretAccessAudits.Where(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == projectId)).ToList();
    var highRiskRules = store.ApprovalGovernanceRules.Where(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == projectId) && x.Enabled && x.RequiredApprovalSteps >= 2).ToList();
    return Results.Ok(new
    {
        actor,
        tenantAccessGrants = store.TenantAccessGrants.Count(x => x.CustomerId == customerId && x.Status == TenantAccessStatus.Active),
        roles = store.SecurityRoles.Count(x => x.CustomerId == customerId),
        permissions = store.SecurityPermissions.Count,
        dataClassificationRules = store.DataClassificationRules.Count(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == projectId || x.ProjectId == null)),
        aiAccessPolicies = store.AiAccessPolicies.Count(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == projectId)),
        connectorSecurityPolicies = store.ConnectorSecurityPolicies.Count(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == projectId)),
        approvalGovernanceRules = store.ApprovalGovernanceRules.Count(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == projectId)),
        multiStepApprovalRules = highRiskRules.Count,
        complianceEvidence = store.ComplianceEvidence.Count(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == projectId)),
        deniedSecretAccess = secretAccess.Count(x => x.Status == SecretAccessStatus.Denied),
        secretAccessCount = secretAccess.Count,
        immutableAuditLogs = auditLogs.Count,
        recentSecurityEvents = auditLogs.OrderByDescending(x => x.CreatedAt).Take(20),
        controls = new[]
        {
            "TENANT_ISOLATION_HEADER_GUARD",
            "RBAC_POLICY_AUTHORIZATION",
            "SECRET_REF_ONLY",
            "AI_MASKING_REQUIRED",
            "CONNECTOR_PERMISSION_ENFORCED",
            "PRODUCTION_APPROVAL_GOVERNANCE"
        }
    });
});

app.MapGet("/api/customers/{customerId:guid}/security/tenant-access", (Guid customerId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    return RequirePermission(store, customerId, null, actor, "security.view", out var error)
        ? Results.Ok(store.TenantAccessGrants.Where(x => x.CustomerId == customerId).OrderBy(x => x.UserId))
        : error;
});

app.MapPost("/api/customers/{customerId:guid}/security/tenant-access", (Guid customerId, TenantAccessRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, request.ProjectId, actor, "security.manage", out var error)) return error;
    var grant = new TenantAccessGrant { CustomerId = customerId, ProjectId = request.ProjectId, UserId = request.UserId, RoleKey = request.RoleKey, GrantedBy = actor, ExpiresAt = request.ExpiresAt };
    store.TenantAccessGrants.Add(grant);
    if (!store.UserRoleAssignments.Any(x => x.CustomerId == customerId && x.ProjectId == request.ProjectId && x.UserId == request.UserId && x.RoleKey == request.RoleKey))
    {
        store.UserRoleAssignments.Add(new UserRoleAssignment { CustomerId = customerId, ProjectId = request.ProjectId, UserId = request.UserId, RoleKey = request.RoleKey });
    }
    audit.Write(customerId, request.ProjectId, "TENANT_ACCESS_GRANTED", nameof(TenantAccessGrant), grant.Id, grant);
    return Results.Created($"/api/customers/{customerId}/security/tenant-access/{grant.Id}", grant);
});

app.MapGet("/api/customers/{customerId:guid}/security/roles", (Guid customerId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    return RequirePermission(store, customerId, null, actor, "security.view", out var error)
        ? Results.Ok(new
        {
            roles = store.SecurityRoles.Where(x => x.CustomerId == customerId).OrderBy(x => x.RoleKey),
            permissions = store.SecurityPermissions.OrderBy(x => x.PermissionKey),
            rolePermissions = store.SecurityRolePermissions.Where(x => x.CustomerId == customerId).OrderBy(x => x.RoleKey),
            assignments = store.UserRoleAssignments.Where(x => x.CustomerId == customerId).OrderBy(x => x.UserId)
        })
        : error;
});

app.MapPost("/api/customers/{customerId:guid}/security/roles", (Guid customerId, SecurityRoleRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, null, actor, "security.manage", out var error)) return error;
    var role = new SecurityRole { CustomerId = customerId, RoleKey = request.RoleKey, Name = request.Name, Description = request.Description };
    store.SecurityRoles.Add(role);
    foreach (var permission in request.PermissionKeys.Distinct(StringComparer.OrdinalIgnoreCase))
    {
        store.SecurityRolePermissions.Add(new SecurityRolePermission { CustomerId = customerId, RoleKey = role.RoleKey, PermissionKey = permission });
    }
    audit.Write(customerId, null, "SECURITY_ROLE_CREATED", nameof(SecurityRole), role.Id, role);
    return Results.Created($"/api/customers/{customerId}/security/roles/{role.Id}", role);
});

app.MapPost("/api/customers/{customerId:guid}/security/secrets", (Guid customerId, SecretVaultReferenceRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, request.ProjectId, actor, "secret.manage", out var error)) return error;
    if (string.IsNullOrWhiteSpace(request.SecretRef) || !request.SecretRef.StartsWith("secret://", StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new { error = "Only secret_ref values are allowed. Do not submit raw secrets." });
    }
    var secret = new SecretVaultReference { CustomerId = customerId, ProjectId = request.ProjectId, Name = request.Name, SecretRef = request.SecretRef, VaultProvider = request.VaultProvider, Classification = DataClassificationLevel.Secret, RotationDueAt = request.RotationDueAt };
    store.SecretVaultReferences.Add(secret);
    audit.Write(customerId, request.ProjectId, "SECRET_REFERENCE_REGISTERED", nameof(SecretVaultReference), secret.Id, new { secret.Name, secret.SecretRef, secret.VaultProvider, secret.Classification });
    return Results.Created($"/api/customers/{customerId}/security/secrets/{secret.Id}", secret);
});

app.MapPost("/api/customers/{customerId:guid}/security/secrets/access", (Guid customerId, SecretAccessRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    var projectId = request.ProjectId;
    var allowed = RequirePermission(store, customerId, projectId, actor, "secret.read", out _);
    var exists = store.SecretVaultReferences.Any(x => x.CustomerId == customerId && x.SecretRef == request.SecretRef && (!projectId.HasValue || x.ProjectId == projectId || x.ProjectId == null));
    var access = new SecretAccessAudit
    {
        CustomerId = customerId,
        ProjectId = projectId,
        UserId = actor,
        SecretRef = MaskSecretRef(request.SecretRef),
        Purpose = request.Purpose,
        Status = !exists ? SecretAccessStatus.NotFound : allowed ? SecretAccessStatus.Allowed : SecretAccessStatus.Denied,
        Reason = !exists ? "Secret reference not registered for tenant." : allowed ? "Permission granted. Secret value resolved outside database." : "Missing secret.read permission."
    };
    store.SecretAccessAudits.Add(access);
    audit.Write(customerId, projectId, "SECRET_ACCESS_AUDITED", nameof(SecretAccessAudit), access.Id, access);
    return allowed && exists
        ? Results.Ok(new { access.Status, access.Reason, access.CorrelationId, valueReturned = false })
        : Results.Json(new { access.Status, access.Reason, access.CorrelationId, valueReturned = false }, statusCode: StatusCodes.Status403Forbidden);
});

app.MapGet("/api/customers/{customerId:guid}/security/data-classification", (Guid customerId, IAppStore store, HttpContext http, Guid? projectId) =>
{
    var actor = Actor(http);
    return RequirePermission(store, customerId, projectId, actor, "security.view", out var error)
        ? Results.Ok(store.DataClassificationRules.Where(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == projectId || x.ProjectId == null)).OrderBy(x => x.ResourceType))
        : error;
});

app.MapPost("/api/customers/{customerId:guid}/security/data-classification", (Guid customerId, DataClassificationRuleRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, request.ProjectId, actor, "security.manage", out var error)) return error;
    var rule = new DataClassificationRule { CustomerId = customerId, ProjectId = request.ProjectId, ResourceType = request.ResourceType, FieldName = request.FieldName, Classification = request.Classification, MaskingStrategy = request.MaskingStrategy, ApplyToAiPrompt = request.ApplyToAiPrompt };
    store.DataClassificationRules.Add(rule);
    audit.Write(customerId, request.ProjectId, "DATA_CLASSIFICATION_RULE_CREATED", nameof(DataClassificationRule), rule.Id, rule);
    return Results.Created($"/api/customers/{customerId}/security/data-classification/{rule.Id}", rule);
});

app.MapPost("/api/customers/{customerId:guid}/security/mask-preview", (Guid customerId, MaskPreviewRequest request, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, request.ProjectId, actor, "security.view", out var error)) return error;
    return Results.Ok(new { masked = MaskByClassification(store, customerId, request.ProjectId, request.ResourceType, request.Text), classificationApplied = true });
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/security/ai-access-policies", (Guid customerId, Guid projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    return RequirePermission(store, customerId, projectId, actor, "ai.policy.manage", out var error)
        ? Results.Ok(store.AiAccessPolicies.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderBy(x => x.TaskType))
        : error;
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/security/ai-access-policies", (Guid customerId, Guid projectId, AiAccessPolicyRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "ai.policy.manage", out var error)) return error;
    var policy = new AiAccessPolicy { CustomerId = customerId, ProjectId = projectId, TaskType = request.TaskType, AllowedRolesCsv = request.AllowedRolesCsv, MaxInputClassification = request.MaxInputClassification, MaskingRequired = request.MaskingRequired, RequiresApprovalForHighRisk = request.RequiresApprovalForHighRisk };
    store.AiAccessPolicies.Add(policy);
    audit.Write(customerId, projectId, "AI_ACCESS_POLICY_CREATED", nameof(AiAccessPolicy), policy.Id, policy);
    return Results.Created($"/api/customers/{customerId}/projects/{projectId}/security/ai-access-policies/{policy.Id}", policy);
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/security/connector-security-policies", (Guid customerId, Guid projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    return RequirePermission(store, customerId, projectId, actor, "connector.policy.manage", out var error)
        ? Results.Ok(store.ConnectorSecurityPolicies.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderBy(x => x.ConnectorType))
        : error;
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/security/connector-security-policies", (Guid customerId, Guid projectId, ConnectorSecurityPolicyRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "connector.policy.manage", out var error)) return error;
    var policy = new ConnectorSecurityPolicy { CustomerId = customerId, ProjectId = projectId, EnvironmentId = request.EnvironmentId, ConnectorType = request.ConnectorType, AllowedActionsCsv = request.AllowedActionsCsv, RequiredPermission = request.RequiredPermission, MaxDataClassification = request.MaxDataClassification, ReadOnlyRequired = request.ReadOnlyRequired, AllowTestApply = request.AllowTestApply, AllowProductionApplyWithApproval = request.AllowProductionApplyWithApproval };
    store.ConnectorSecurityPolicies.Add(policy);
    audit.Write(customerId, projectId, "CONNECTOR_SECURITY_POLICY_CREATED", nameof(ConnectorSecurityPolicy), policy.Id, policy);
    return Results.Created($"/api/customers/{customerId}/projects/{projectId}/security/connector-security-policies/{policy.Id}", policy);
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/security/approval-governance-rules", (Guid customerId, Guid projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    return RequirePermission(store, customerId, projectId, actor, "approval.policy.manage", out var error)
        ? Results.Ok(store.ApprovalGovernanceRules.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderBy(x => x.ModuleName))
        : error;
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/security/approval-governance-rules", (Guid customerId, Guid projectId, ApprovalGovernanceRuleRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "approval.policy.manage", out var error)) return error;
    var rule = new ApprovalGovernanceRule { CustomerId = customerId, ProjectId = projectId, RuleKey = request.RuleKey, ModuleName = request.ModuleName, MinimumRiskLevel = request.MinimumRiskLevel, AppliesToProduction = request.AppliesToProduction, RequiredApprovalSteps = request.RequiredApprovalSteps, ApproverRolesCsv = request.ApproverRolesCsv, RequiresSecurityApproval = request.RequiresSecurityApproval, Reason = request.Reason };
    store.ApprovalGovernanceRules.Add(rule);
    audit.Write(customerId, projectId, "APPROVAL_GOVERNANCE_RULE_CREATED", nameof(ApprovalGovernanceRule), rule.Id, rule);
    return Results.Created($"/api/customers/{customerId}/projects/{projectId}/security/approval-governance-rules/{rule.Id}", rule);
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/security/compliance-evidence", (Guid customerId, Guid projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    return RequirePermission(store, customerId, projectId, actor, "compliance.view", out var error)
        ? Results.Ok(store.ComplianceEvidence.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt))
        : error;
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/security/compliance-evidence/generate", (Guid customerId, Guid projectId, GenerateComplianceEvidenceRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "compliance.manage", out var error)) return error;
    var auditLog = request.AuditLogId.HasValue ? store.AuditLogs.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == request.AuditLogId.Value) : null;
    var approval = request.ApprovalRequestId.HasValue ? store.ApprovalRequests.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == request.ApprovalRequestId.Value) : null;
    var evidence = new ComplianceEvidence
    {
        CustomerId = customerId,
        ProjectId = projectId,
        AuditLogId = auditLog?.Id,
        ApprovalRequestId = approval?.Id,
        EvidenceNo = store.NextNumber("CEV"),
        ControlId = request.ControlId,
        Title = request.Title,
        Summary = request.Summary,
        EntityType = request.EntityType,
        EntityId = request.EntityId,
        EvidenceRef = request.EvidenceRef ?? $"audit://{auditLog?.CorrelationId ?? Guid.NewGuid().ToString("N")}",
        TraceJson = JsonSerializer.Serialize(new { auditLogId = auditLog?.Id, approvalRequestId = approval?.Id, sourceEntityType = request.EntityType, sourceEntityId = request.EntityId })
    };
    store.ComplianceEvidence.Add(evidence);
    audit.Write(customerId, projectId, "COMPLIANCE_EVIDENCE_GENERATED", nameof(ComplianceEvidence), evidence.Id, evidence);
    return Results.Created($"/api/customers/{customerId}/projects/{projectId}/security/compliance-evidence/{evidence.Id}", evidence);
});

app.MapGet("/api/service-plans", (IAppStore store) =>
    Results.Ok(store.ServicePlans.Where(x => x.Active).OrderBy(x => x.BaseMonthlyPrice)));

app.MapPost("/api/service-plans", (ServicePlanRequest request, IAppStore store) =>
{
    var plan = new ServicePlan
    {
        PlanCode = request.PlanCode,
        Name = request.Name,
        Description = request.Description,
        BaseMonthlyPrice = request.BaseMonthlyPrice,
        Currency = request.Currency,
        MaxProjects = request.MaxProjects,
        MaxConnectors = request.MaxConnectors,
        MaxAiRunsPerMonth = request.MaxAiRunsPerMonth,
        MaxTicketsPerMonth = request.MaxTicketsPerMonth,
        IncludedSupportHours = request.IncludedSupportHours,
        SlaResponseHours = request.SlaResponseHours,
        SlaResolutionHours = request.SlaResolutionHours,
        EnabledModulesCsv = request.EnabledModulesCsv,
        QuotaEnforcementMode = request.QuotaEnforcementMode
    };
    store.ServicePlans.Add(plan);
    return Results.Created($"/api/service-plans/{plan.Id}", plan);
});

app.MapGet("/api/customers/{customerId:guid}/commercial/subscriptions", (Guid customerId, IAppStore store) =>
    Results.Ok(store.Subscriptions.Where(x => x.CustomerId == customerId).OrderByDescending(x => x.CreatedAt)));

app.MapPost("/api/customers/{customerId:guid}/commercial/contracts", (Guid customerId, CustomerContractRequest request, IAppStore store, IAuditWriter audit) =>
{
    if (FindCustomer(store, customerId) is null) return Results.NotFound(new { error = "Customer not found." });
    var contract = new CustomerContract
    {
        CustomerId = customerId,
        ContractNo = store.NextNumber("CTR"),
        Title = request.Title,
        Status = request.Status,
        StartsAt = request.StartsAt,
        EndsAt = request.EndsAt,
        Currency = request.Currency,
        ContractValue = request.ContractValue,
        TermsSummary = request.TermsSummary,
        BillingContactRef = request.BillingContactRef
    };
    store.CustomerContracts.Add(contract);
    audit.Write(customerId, null, "CUSTOMER_CONTRACT_CREATED", nameof(CustomerContract), contract.Id, contract);
    return Results.Created($"/api/customers/{customerId}/commercial/contracts/{contract.Id}", contract);
});

app.MapPost("/api/customers/{customerId:guid}/commercial/subscriptions", (Guid customerId, SubscriptionRequest request, IAppStore store, IAuditWriter audit) =>
{
    if (FindCustomer(store, customerId) is null) return Results.NotFound(new { error = "Customer not found." });
    var plan = store.ServicePlans.SingleOrDefault(x => x.Id == request.ServicePlanId);
    if (plan is null) return Results.BadRequest(new { error = "Service plan not found." });
    var subscription = new Subscription
    {
        CustomerId = customerId,
        ServicePlanId = plan.Id,
        ContractId = request.ContractId,
        SubscriptionNo = store.NextNumber("SUB"),
        Status = request.Status,
        BillingCycle = request.BillingCycle,
        StartsAt = request.StartsAt,
        EndsAt = request.EndsAt,
        CurrentPeriodStart = request.CurrentPeriodStart,
        CurrentPeriodEnd = request.CurrentPeriodEnd,
        UnitPrice = request.UnitPrice ?? plan.BaseMonthlyPrice,
        Currency = plan.Currency
    };
    store.Subscriptions.Add(subscription);
    store.SupportEntitlements.Add(NewEntitlement(customerId, subscription.Id, plan));
    store.SlaPolicies.Add(new SlaPolicy { CustomerId = customerId, SubscriptionId = subscription.Id, PolicyNo = store.NextNumber("SLA"), Name = $"{plan.Name} High Severity SLA", Severity = IssueSeverity.High, ResponseHours = plan.SlaResponseHours, ResolutionHours = plan.SlaResolutionHours, WarningBeforeHours = Math.Max(1, plan.SlaResponseHours / 2) });
    audit.Write(customerId, null, "SUBSCRIPTION_CREATED", nameof(Subscription), subscription.Id, subscription);
    return Results.Created($"/api/customers/{customerId}/commercial/subscriptions/{subscription.Id}", subscription);
});

app.MapGet("/api/customers/{customerId:guid}/commercial/contracts", (Guid customerId, IAppStore store) =>
    Results.Ok(store.CustomerContracts.Where(x => x.CustomerId == customerId).OrderByDescending(x => x.StartsAt)));

app.MapGet("/api/customers/{customerId:guid}/commercial/entitlements", (Guid customerId, IAppStore store) =>
    Results.Ok(store.SupportEntitlements.Where(x => x.CustomerId == customerId).OrderByDescending(x => x.CreatedAt)));

app.MapGet("/api/customers/{customerId:guid}/commercial/sla-policies", (Guid customerId, IAppStore store) =>
    Results.Ok(store.SlaPolicies.Where(x => x.CustomerId == customerId).OrderBy(x => x.Severity)));

app.MapGet("/api/customers/{customerId:guid}/portal/summary", (Guid customerId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    return Results.Ok(CustomerPortalSummary(store, customerId));
});

app.MapGet("/api/customers/{customerId:guid}/portal/tickets", (Guid customerId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    return Results.Ok(store.CustomerPortalTickets.Where(x => x.CustomerId == customerId).OrderByDescending(x => x.SubmittedAt));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/portal/tickets", (Guid customerId, Guid projectId, PortalTicketRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    if (FindProject(store, customerId, projectId) is null) return Results.NotFound(new { error = "Project not found." });
    var quota = EvaluateQuota(store, customerId, UsageMetricType.Ticket, 1);
    if (quota.Blocked) return Results.Conflict(new { error = "Ticket quota exceeded.", quota });
    var sla = ResolveSlaPolicy(store, customerId, request.Severity);
    var ticket = new CustomerPortalTicket
    {
        CustomerId = customerId,
        ProjectId = projectId,
        SlaPolicyId = sla?.Id,
        TicketNo = store.NextNumber("TCK"),
        Title = request.Title,
        Description = request.Description,
        Severity = request.Severity,
        RequestedBy = request.RequestedBy ?? actor
    };
    ApplySla(ticket, sla, DateTimeOffset.UtcNow);
    store.CustomerPortalTickets.Add(ticket);
    var issue = new Issue { CustomerId = customerId, ProjectId = projectId, IssueNo = store.NextNumber("ISS"), Title = request.Title, Description = request.Description, Severity = request.Severity, RiskLevel = request.Severity is IssueSeverity.Critical ? RiskLevel.Critical : request.Severity is IssueSeverity.High ? RiskLevel.High : RiskLevel.Medium, Category = IssueCategory.Other, Priority = request.Severity is IssueSeverity.Critical ? IssuePriority.P1 : IssuePriority.P2, ReportedBy = ticket.RequestedBy };
    store.Issues.Add(issue);
    ticket.IssueId = issue.Id;
    AddUsage(store, customerId, projectId, UsageMetricType.Ticket, nameof(CustomerPortalTicket), ticket.Id, 1, "Portal ticket created.");
    store.TraceLinks.Add(NewTrace(customerId, projectId, nameof(CustomerPortalTicket), ticket.Id, nameof(Issue), issue.Id, "PortalTicketIssue"));
    audit.Write(customerId, projectId, "CUSTOMER_PORTAL_TICKET_CREATED", nameof(CustomerPortalTicket), ticket.Id, new { ticket, quota });
    return Results.Created($"/api/customers/{customerId}/portal/tickets/{ticket.Id}", new { ticket, issue, quota });
});

app.MapPost("/api/customers/{customerId:guid}/portal/tickets/{ticketId:guid}/first-response", (Guid customerId, Guid ticketId, TicketActionRequest request, IAppStore store, IAuditWriter audit) =>
{
    var ticket = store.CustomerPortalTickets.SingleOrDefault(x => x.CustomerId == customerId && x.Id == ticketId);
    if (ticket is null) return Results.NotFound(new { error = "Ticket not found." });
    ticket.FirstResponseAt = request.At ?? DateTimeOffset.UtcNow;
    RecalculateTicketSla(ticket, DateTimeOffset.UtcNow);
    audit.Write(customerId, ticket.ProjectId, "CUSTOMER_PORTAL_TICKET_RESPONDED", nameof(CustomerPortalTicket), ticket.Id, ticket);
    return Results.Ok(ticket);
});

app.MapPost("/api/customers/{customerId:guid}/portal/tickets/{ticketId:guid}/resolve", (Guid customerId, Guid ticketId, TicketActionRequest request, IAppStore store, IAuditWriter audit) =>
{
    var ticket = store.CustomerPortalTickets.SingleOrDefault(x => x.CustomerId == customerId && x.Id == ticketId);
    if (ticket is null) return Results.NotFound(new { error = "Ticket not found." });
    ticket.ResolvedAt = request.At ?? DateTimeOffset.UtcNow;
    ticket.Status = PortalTicketStatus.Resolved;
    RecalculateTicketSla(ticket, DateTimeOffset.UtcNow);
    audit.Write(customerId, ticket.ProjectId, "CUSTOMER_PORTAL_TICKET_RESOLVED", nameof(CustomerPortalTicket), ticket.Id, ticket);
    return Results.Ok(ticket);
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/portal/service-requests", (Guid customerId, Guid projectId, ServiceRequestDto request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var serviceRequest = new ServiceRequest { CustomerId = customerId, ProjectId = projectId, PortalTicketId = request.PortalTicketId, RequestNo = store.NextNumber("SRQ"), RequestType = request.RequestType, Title = request.Title, Description = request.Description, RiskLevel = request.RiskLevel, EstimatedHours = request.EstimatedHours, RequestedBy = request.RequestedBy ?? actor };
    store.ServiceRequests.Add(serviceRequest);
    audit.Write(customerId, projectId, "SERVICE_REQUEST_CREATED", nameof(ServiceRequest), serviceRequest.Id, serviceRequest);
    return Results.Created($"/api/customers/{customerId}/portal/service-requests/{serviceRequest.Id}", serviceRequest);
});

app.MapGet("/api/customers/{customerId:guid}/commercial/usage", (Guid customerId, IAppStore store, DateTimeOffset? from, DateTimeOffset? to) =>
{
    var usage = store.UsageRecords.Where(x => x.CustomerId == customerId);
    if (from.HasValue) usage = usage.Where(x => x.UsageDate >= from.Value);
    if (to.HasValue) usage = usage.Where(x => x.UsageDate <= to.Value);
    return Results.Ok(usage.OrderByDescending(x => x.UsageDate));
});

app.MapPost("/api/customers/{customerId:guid}/commercial/usage/recalculate", (Guid customerId, IAppStore store, IAuditWriter audit) =>
{
    var subscription = ActiveSubscription(store, customerId);
    if (subscription is null) return Results.NotFound(new { error = "Active subscription not found." });
    var snapshots = RecalculateUsageQuotas(store, customerId, subscription);
    audit.Write(customerId, null, "USAGE_QUOTA_RECALCULATED", nameof(UsageQuotaSnapshot), subscription.Id, snapshots);
    return Results.Ok(snapshots);
});

app.MapPost("/api/customers/{customerId:guid}/commercial/billing-drafts/generate", (Guid customerId, GenerateBillingDraftRequest request, IAppStore store, IAuditWriter audit) =>
{
    var subscription = store.Subscriptions.SingleOrDefault(x => x.CustomerId == customerId && x.Id == request.SubscriptionId);
    if (subscription is null) return Results.NotFound(new { error = "Subscription not found." });
    var draft = GenerateBillingDraft(store, customerId, subscription, request.PeriodStart, request.PeriodEnd);
    audit.Write(customerId, null, "BILLING_DRAFT_GENERATED", nameof(BillingDraft), draft.Id, draft);
    return Results.Created($"/api/customers/{customerId}/commercial/billing-drafts/{draft.Id}", new { draft, lines = store.BillingLineItems.Where(x => x.CustomerId == customerId && x.BillingDraftId == draft.Id) });
});

app.MapGet("/api/customers/{customerId:guid}/commercial/billing-drafts", (Guid customerId, IAppStore store) =>
    Results.Ok(store.BillingDrafts.Where(x => x.CustomerId == customerId).OrderByDescending(x => x.CreatedAt)));

app.MapPost("/api/customers/{customerId:guid}/commercial/billing-drafts/{billingDraftId:guid}/invoice-draft", (Guid customerId, Guid billingDraftId, InvoiceDraftRequest request, IAppStore store, IAuditWriter audit) =>
{
    var billing = store.BillingDrafts.SingleOrDefault(x => x.CustomerId == customerId && x.Id == billingDraftId);
    if (billing is null) return Results.NotFound(new { error = "Billing draft not found." });
    var invoice = new InvoiceDraft { CustomerId = customerId, BillingDraftId = billing.Id, SubscriptionId = billing.SubscriptionId, InvoiceNo = store.NextNumber("INV"), IssueDate = request.IssueDate, DueDate = request.DueDate, TotalAmount = billing.TotalAmount, Currency = billing.Currency, TraceJson = billing.TraceJson };
    store.InvoiceDrafts.Add(invoice);
    audit.Write(customerId, null, "INVOICE_DRAFT_CREATED", nameof(InvoiceDraft), invoice.Id, invoice);
    return Results.Created($"/api/customers/{customerId}/commercial/invoice-drafts/{invoice.Id}", invoice);
});

app.MapGet("/api/customers/{customerId:guid}/commercial/invoice-drafts", (Guid customerId, IAppStore store) =>
    Results.Ok(store.InvoiceDrafts.Where(x => x.CustomerId == customerId).OrderByDescending(x => x.IssueDate)));

app.MapPost("/api/customers/{customerId:guid}/commercial/invoice-drafts/{invoiceId:guid}/payments", (Guid customerId, Guid invoiceId, PaymentTrackingRequest request, IAppStore store, IAuditWriter audit) =>
{
    var invoice = store.InvoiceDrafts.SingleOrDefault(x => x.CustomerId == customerId && x.Id == invoiceId);
    if (invoice is null) return Results.NotFound(new { error = "Invoice draft not found." });
    var payment = new PaymentTrackingRecord { CustomerId = customerId, InvoiceDraftId = invoice.Id, PaymentRef = request.PaymentRef, Status = request.Status, Amount = request.Amount, Currency = invoice.Currency, RecordedAt = request.RecordedAt, Notes = request.Notes };
    store.PaymentTrackingRecords.Add(payment);
    if (request.Status == PaymentTrackingStatus.Recorded && request.Amount >= invoice.TotalAmount) invoice.Status = InvoiceDraftStatus.Paid;
    audit.Write(customerId, null, "PAYMENT_TRACKING_RECORDED", nameof(PaymentTrackingRecord), payment.Id, new { payment, sensitivePaymentDataStored = false });
    return Results.Created($"/api/customers/{customerId}/commercial/payments/{payment.Id}", payment);
});

app.MapPost("/api/customers/{customerId:guid}/commercial/service-reports/generate", (Guid customerId, GenerateServiceReportRequest request, IAppStore store, IAuditWriter audit) =>
{
    var report = GenerateServiceReport(store, customerId, request.SubscriptionId, request.PeriodStart, request.PeriodEnd);
    audit.Write(customerId, null, "CUSTOMER_SERVICE_REPORT_GENERATED", nameof(CustomerServiceReport), report.Id, report);
    return Results.Created($"/api/customers/{customerId}/commercial/service-reports/{report.Id}", report);
});

app.MapGet("/api/customers/{customerId:guid}/commercial/service-reports", (Guid customerId, IAppStore store) =>
    Results.Ok(store.CustomerServiceReports.Where(x => x.CustomerId == customerId).OrderByDescending(x => x.PeriodEnd)));

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/reporting/catalog", (Guid customerId, Guid projectId, IAppStore store, IReportingService reporting) =>
    FindProject(store, customerId, projectId) is null
        ? Results.NotFound(new { error = "Project not found." })
        : Results.Ok(reporting.GetCatalog(customerId, projectId)));

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/dashboards/executive", (Guid customerId, Guid projectId, IAppStore store, IReportingService reporting, HttpContext http, DateTimeOffset? dateFrom, DateTimeOffset? dateTo) =>
{
    if (FindProject(store, customerId, projectId) is null) return Results.NotFound(new { error = "Project not found." });
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "dashboard.executive.view", out var error)) return error;
    return Results.Ok(reporting.GetExecutiveDashboard(ReportFilter(customerId, projectId, dateFrom, dateTo)));
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/dashboards/project", (Guid customerId, Guid projectId, IAppStore store, IReportingService reporting, HttpContext http, DateTimeOffset? dateFrom, DateTimeOffset? dateTo) =>
{
    if (FindProject(store, customerId, projectId) is null) return Results.NotFound(new { error = "Project not found." });
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "report.view", out var error)) return error;
    return Results.Ok(reporting.GetProjectDashboard(ReportFilter(customerId, projectId, dateFrom, dateTo)));
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/dashboards/customer-health", (Guid customerId, Guid projectId, IAppStore store, IReportingService reporting, HttpContext http, DateTimeOffset? dateFrom, DateTimeOffset? dateTo) =>
{
    if (FindProject(store, customerId, projectId) is null) return Results.NotFound(new { error = "Project not found." });
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "report.view", out var error)) return error;
    return Results.Ok(reporting.GetCustomerHealthDashboard(ReportFilter(customerId, projectId, dateFrom, dateTo)));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/dashboards/ai-summary", async (Guid customerId, Guid projectId, GenerateDashboardSummaryRequest request, IAppStore store, IReportingService reporting, HttpContext http, CancellationToken cancellationToken) =>
{
    if (FindProject(store, customerId, projectId) is null) return Results.NotFound(new { error = "Project not found." });
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "dashboard.executive.view", out var error)) return error;
    var snapshot = await reporting.GenerateAiSummaryAsync(ReportFilter(customerId, projectId, request.DateFrom, request.DateTo), actor, cancellationToken);
    return Results.Created($"/api/customers/{customerId}/projects/{projectId}/dashboards/ai-summary/{snapshot.Id}", snapshot);
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/reporting/exports", (Guid customerId, Guid projectId, IAppStore store, HttpContext http) =>
{
    if (FindProject(store, customerId, projectId) is null) return Results.NotFound(new { error = "Project not found." });
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "report.view", out var error)) return error;
    return Results.Ok(new
    {
        jobs = store.ReportGenerationJobs.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt),
        files = store.ReportExportFiles.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt)
    });
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/reporting/exports", async (Guid customerId, Guid projectId, GenerateReportExportRequest request, IAppStore store, IReportingService reporting, HttpContext http, CancellationToken cancellationToken) =>
{
    if (FindProject(store, customerId, projectId) is null) return Results.NotFound(new { error = "Project not found." });
    var actor = Actor(http);
    var template = ResolveReportTemplateForRequest(store, customerId, projectId, request.TemplateId, request.ReportType);
    if (template is null) return Results.NotFound(new { error = "Report template not found." });
    if (!CanExportReport(store, customerId, projectId, actor, template, request.Visibility, out var error)) return error;
    var result = await reporting.GenerateAsync(new ReportGenerationRequest(
        customerId,
        projectId,
        request.TemplateId,
        request.ReportType,
        request.OutputFormat,
        request.Visibility,
        request.DateFrom,
        request.DateTo,
        actor,
        request.ExternalExport),
        cancellationToken);
    return Results.Created($"/api/customers/{customerId}/projects/{projectId}/reporting/exports/{result.File.Id}", result);
});

app.MapGet("/api/customers/{customerId:guid}/integrations/dashboard", (Guid customerId, Guid? projectId, IAppStore store, IIntegrationHubService integrations, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "integration.view", out var error)) return error;
    return Results.Ok(integrations.GetDashboard(new IntegrationDashboardFilter(customerId, projectId)));
});

app.MapGet("/api/customers/{customerId:guid}/integrations/providers", (Guid customerId, Guid? projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "integration.view", out var error)) return error;
    return Results.Ok(store.IntegrationProviders.Where(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == null || x.ProjectId == projectId)).OrderBy(x => x.Category).ThenBy(x => x.Name));
});

app.MapPost("/api/customers/{customerId:guid}/integrations/providers", (Guid customerId, IntegrationProviderRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, request.ProjectId, actor, "integration.manage", out var error)) return error;
    if (request.ProjectId.HasValue && FindProject(store, customerId, request.ProjectId.Value) is null) return Results.NotFound(new { error = "Project not found." });
    var provider = new IntegrationProvider { CustomerId = customerId, ProjectId = request.ProjectId, ProviderKey = request.ProviderKey, Name = request.Name, Category = request.Category, BaseUrl = request.BaseUrl, DocumentationUrl = request.DocumentationUrl, SupportsInboundWebhook = request.SupportsInboundWebhook, SupportsOutboundWebhook = request.SupportsOutboundWebhook, SupportsSignatureVerification = request.SupportsSignatureVerification, SupportsRetry = request.SupportsRetry, DefaultTimeoutSeconds = request.DefaultTimeoutSeconds, ConfigJson = request.ConfigJson };
    store.IntegrationProviders.Add(provider);
    audit.Write(customerId, request.ProjectId, "INTEGRATION_PROVIDER_REGISTERED", nameof(IntegrationProvider), provider.Id, provider);
    return Results.Created($"/api/customers/{customerId}/integrations/providers/{provider.Id}", provider);
});

app.MapGet("/api/customers/{customerId:guid}/integrations/endpoints", (Guid customerId, Guid? projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "integration.view", out var error)) return error;
    return Results.Ok(store.IntegrationEndpoints.Where(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == null || x.ProjectId == projectId)).OrderBy(x => x.Direction).ThenBy(x => x.Name));
});

app.MapPost("/api/customers/{customerId:guid}/integrations/endpoints", (Guid customerId, IntegrationEndpointRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, request.ProjectId, actor, "integration.manage", out var error)) return error;
    if (!ValidIntegrationSecretRef(request.AuthType, request.SecretRef)) return Results.BadRequest(new { error = "Only secret_ref values are allowed for integration auth." });
    var provider = store.IntegrationProviders.SingleOrDefault(x => x.CustomerId == customerId && x.Id == request.ProviderId && x.Active);
    if (provider is null) return Results.NotFound(new { error = "Provider not found." });
    if (request.ProjectId.HasValue && FindProject(store, customerId, request.ProjectId.Value) is null) return Results.NotFound(new { error = "Project not found." });
    var endpoint = new IntegrationEndpoint { CustomerId = customerId, ProjectId = request.ProjectId, ProviderId = provider.Id, EndpointKey = request.EndpointKey, Name = request.Name, Direction = request.Direction, HttpMethod = request.HttpMethod, PathOrUrl = request.PathOrUrl, AuthType = request.AuthType, SecretRef = request.SecretRef, TimeoutSeconds = request.TimeoutSeconds, MaxDataClassification = request.MaxDataClassification, MaskOutboundPayloads = request.MaskOutboundPayloads };
    store.IntegrationEndpoints.Add(endpoint);
    audit.Write(customerId, request.ProjectId, "INTEGRATION_ENDPOINT_REGISTERED", nameof(IntegrationEndpoint), endpoint.Id, new { endpoint, rawSecretStored = false });
    return Results.Created($"/api/customers/{customerId}/integrations/endpoints/{endpoint.Id}", endpoint);
});

app.MapGet("/api/customers/{customerId:guid}/integrations/payload-mappings", (Guid customerId, Guid? projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "integration.view", out var error)) return error;
    return Results.Ok(store.IntegrationPayloadMappings.Where(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == null || x.ProjectId == projectId)).OrderBy(x => x.MappingKey));
});

app.MapPost("/api/customers/{customerId:guid}/integrations/payload-mappings", (Guid customerId, IntegrationPayloadMappingRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, request.ProjectId, actor, "integration.manage", out var error)) return error;
    if (!store.IntegrationProviders.Any(x => x.CustomerId == customerId && x.Id == request.ProviderId)) return Results.NotFound(new { error = "Provider not found." });
    var mapping = new IntegrationPayloadMapping { CustomerId = customerId, ProjectId = request.ProjectId, ProviderId = request.ProviderId, EndpointId = request.EndpointId, MappingKey = request.MappingKey, SourceSystem = request.SourceSystem, TargetSystem = request.TargetSystem, EventType = request.EventType, MappingJson = request.MappingJson };
    store.IntegrationPayloadMappings.Add(mapping);
    audit.Write(customerId, request.ProjectId, "INTEGRATION_PAYLOAD_MAPPING_CREATED", nameof(IntegrationPayloadMapping), mapping.Id, mapping);
    return Results.Created($"/api/customers/{customerId}/integrations/payload-mappings/{mapping.Id}", mapping);
});

app.MapGet("/api/customers/{customerId:guid}/integrations/event-subscriptions", (Guid customerId, Guid? projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "integration.view", out var error)) return error;
    return Results.Ok(store.IntegrationEventSubscriptions.Where(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == null || x.ProjectId == projectId)).OrderBy(x => x.EventType));
});

app.MapPost("/api/customers/{customerId:guid}/integrations/event-subscriptions", (Guid customerId, IntegrationEventSubscriptionRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, request.ProjectId, actor, "integration.manage", out var error)) return error;
    if (!store.IntegrationProviders.Any(x => x.CustomerId == customerId && x.Id == request.ProviderId)) return Results.NotFound(new { error = "Provider not found." });
    var subscription = new IntegrationEventSubscription { CustomerId = customerId, ProjectId = request.ProjectId, ProviderId = request.ProviderId, EndpointId = request.EndpointId, EventType = request.EventType, SubscriptionKey = request.SubscriptionKey, FilterJson = request.FilterJson };
    store.IntegrationEventSubscriptions.Add(subscription);
    audit.Write(customerId, request.ProjectId, "INTEGRATION_EVENT_SUBSCRIPTION_CREATED", nameof(IntegrationEventSubscription), subscription.Id, subscription);
    return Results.Created($"/api/customers/{customerId}/integrations/event-subscriptions/{subscription.Id}", subscription);
});

app.MapGet("/api/customers/{customerId:guid}/integrations/outbound-webhooks", (Guid customerId, Guid? projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "integration.view", out var error)) return error;
    return Results.Ok(store.WebhookOutboundSubscriptions.Where(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == null || x.ProjectId == projectId)).OrderBy(x => x.EventType));
});

app.MapPost("/api/customers/{customerId:guid}/integrations/outbound-webhooks", (Guid customerId, WebhookOutboundSubscriptionRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, request.ProjectId, actor, "integration.manage", out var error)) return error;
    if (!ValidIntegrationSecretRef(IntegrationAuthType.SecretRefToken, request.SecretRef)) return Results.BadRequest(new { error = "Only secret_ref values are allowed for webhook secrets." });
    if (!store.IntegrationProviders.Any(x => x.CustomerId == customerId && x.Id == request.ProviderId)) return Results.NotFound(new { error = "Provider not found." });
    var subscription = new WebhookOutboundSubscription { CustomerId = customerId, ProjectId = request.ProjectId, ProviderId = request.ProviderId, EndpointId = request.EndpointId, EventType = request.EventType, TargetUrl = request.TargetUrl, SecretRef = request.SecretRef, SignatureMode = request.SignatureMode, MaxRetryAttempts = request.MaxRetryAttempts, RetryBackoffSeconds = request.RetryBackoffSeconds, TimeoutSeconds = request.TimeoutSeconds };
    store.WebhookOutboundSubscriptions.Add(subscription);
    audit.Write(customerId, request.ProjectId, "WEBHOOK_OUTBOUND_SUBSCRIPTION_CREATED", nameof(WebhookOutboundSubscription), subscription.Id, new { subscription, rawSecretStored = false });
    return Results.Created($"/api/customers/{customerId}/integrations/outbound-webhooks/{subscription.Id}", subscription);
});

app.MapGet("/api/customers/{customerId:guid}/integrations/gateway-routes", (Guid customerId, Guid? projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "integration.view", out var error)) return error;
    return Results.Ok(store.ApiGatewayRoutes.Where(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == null || x.ProjectId == projectId)).OrderBy(x => x.RouteKey));
});

app.MapPost("/api/customers/{customerId:guid}/integrations/gateway-routes", (Guid customerId, ApiGatewayRouteRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, request.ProjectId, actor, "integration.gateway.manage", out var error)) return error;
    if (string.IsNullOrWhiteSpace(request.TokenSecretRef) || !request.TokenSecretRef.StartsWith("secret://", StringComparison.OrdinalIgnoreCase)) return Results.BadRequest(new { error = "API gateway route must use token secret_ref." });
    var route = new ApiGatewayRoute { CustomerId = customerId, ProjectId = request.ProjectId, RouteKey = request.RouteKey, PublicPath = request.PublicPath, InternalTarget = request.InternalTarget, HttpMethod = request.HttpMethod, AllowedExternalSystem = request.AllowedExternalSystem, RequiredPermission = request.RequiredPermission, TokenSecretRef = request.TokenSecretRef, TimeoutSeconds = request.TimeoutSeconds, MaxDataClassification = request.MaxDataClassification, AccessPolicyJson = request.AccessPolicyJson };
    store.ApiGatewayRoutes.Add(route);
    audit.Write(customerId, request.ProjectId, "API_GATEWAY_ROUTE_CREATED", nameof(ApiGatewayRoute), route.Id, new { route, rawTokenStored = false });
    return Results.Created($"/api/customers/{customerId}/integrations/gateway-routes/{route.Id}", route);
});

app.MapPost("/api/customers/{customerId:guid}/integrations/endpoints/{endpointId:guid}/outbound-test", async (Guid customerId, Guid endpointId, IntegrationOutboundTestRequest request, IAppStore store, IIntegrationHubService integrations, HttpContext http, CancellationToken cancellationToken) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, request.ProjectId, actor, "integration.execute", out var error)) return error;
    try
    {
        var run = await integrations.ExecuteOutboundAsync(new IntegrationActionRequest(customerId, request.ProjectId, endpointId, request.EventType, request.PayloadJson, actor, request.CorrelationId), cancellationToken);
        return Results.Ok(run);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/customers/{customerId:guid}/integrations/providers/{providerId:guid}/webhooks/inbound", async (Guid customerId, Guid providerId, InboundWebhookDto request, IAppStore store, IIntegrationHubService integrations, HttpContext http, CancellationToken cancellationToken) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, request.ProjectId, actor, "integration.webhook.receive", out var error)) return error;
    var signature = http.Request.Headers["X-Webhook-Signature"].FirstOrDefault();
    try
    {
        var run = await integrations.ReceiveWebhookAsync(new InboundWebhookRequest(customerId, request.ProjectId, providerId, request.EndpointId, request.EventType, request.PayloadJson, signature, actor, request.CorrelationId), cancellationToken);
        return Results.Ok(run);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/customers/{customerId:guid}/integrations/gateway-routes/{routeId:guid}/invoke", async (Guid customerId, Guid routeId, GatewayInvocationDto request, IAppStore store, IIntegrationHubService integrations, HttpContext http, CancellationToken cancellationToken) =>
{
    var actor = Actor(http);
    var tokenSecretRef = http.Request.Headers["X-Gateway-Token-Ref"].FirstOrDefault() ?? "";
    try
    {
        var route = store.ApiGatewayRoutes.SingleOrDefault(x => x.CustomerId == customerId && x.Id == routeId);
        if (route is null) return Results.NotFound(new { error = "Gateway route not found." });
        if (!RequirePermission(store, customerId, route.ProjectId, actor, route.RequiredPermission, out var error)) return error;
        var run = await integrations.InvokeGatewayAsync(new GatewayInvocationRequest(customerId, request.ProjectId ?? route.ProjectId, routeId, request.ExternalSystem, tokenSecretRef, request.PayloadJson, actor, request.CorrelationId), cancellationToken);
        return Results.Ok(run);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/customers/{customerId:guid}/integrations/retries/run-due", async (Guid customerId, Guid? projectId, IAppStore store, IIntegrationHubService integrations, HttpContext http, CancellationToken cancellationToken) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "integration.execute", out var error)) return error;
    var runs = await integrations.ProcessRetriesAsync(customerId, projectId, cancellationToken);
    return Results.Ok(runs);
});

app.MapGet("/api/customers/{customerId:guid}/integrations/runs", (Guid customerId, Guid? projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "integration.view", out var error)) return error;
    return Results.Ok(store.IntegrationRuns.Where(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == null || x.ProjectId == projectId)).OrderByDescending(x => x.StartedAt));
});

app.MapGet("/api/customers/{customerId:guid}/integrations/runs/{runId:guid}/logs", (Guid customerId, Guid runId, IAppStore store, HttpContext http) =>
{
    var run = store.IntegrationRuns.SingleOrDefault(x => x.CustomerId == customerId && x.Id == runId);
    if (run is null) return Results.NotFound(new { error = "Integration run not found." });
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, run.ProjectId, actor, "integration.view", out var error)) return error;
    return Results.Ok(store.IntegrationRunLogs.Where(x => x.CustomerId == customerId && x.IntegrationRunId == runId).OrderBy(x => x.CreatedAt));
});

app.MapGet("/api/customers/{customerId:guid}/integrations/automation-triggers", (Guid customerId, Guid? projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "integration.view", out var error)) return error;
    return Results.Ok(store.IntegrationAutomationTriggers.Where(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == null || x.ProjectId == projectId)).OrderBy(x => x.EventType));
});

app.MapPost("/api/customers/{customerId:guid}/integrations/automation-triggers", (Guid customerId, IntegrationAutomationTriggerRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, request.ProjectId, actor, "integration.manage", out var error)) return error;
    var trigger = new IntegrationAutomationTrigger { CustomerId = customerId, ProjectId = request.ProjectId, ProviderId = request.ProviderId, TriggerKey = request.TriggerKey, EventType = request.EventType, ActionType = request.ActionType, ConditionJson = request.ConditionJson, ActionJson = request.ActionJson, CreateOnFailureOnly = request.CreateOnFailureOnly };
    store.IntegrationAutomationTriggers.Add(trigger);
    audit.Write(customerId, request.ProjectId, "INTEGRATION_AUTOMATION_TRIGGER_CREATED", nameof(IntegrationAutomationTrigger), trigger.Id, trigger);
    return Results.Created($"/api/customers/{customerId}/integrations/automation-triggers/{trigger.Id}", trigger);
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/devops/dashboard", (Guid customerId, Guid projectId, DateTimeOffset? dateFrom, DateTimeOffset? dateTo, IAppStore store, IDevOpsAutomationService devops, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "devops.view", out var error)) return error;
    if (FindProject(store, customerId, projectId) is null) return Results.NotFound(new { error = "Project not found." });
    return Results.Ok(devops.GetDashboard(new DevOpsDashboardFilter(customerId, projectId, dateFrom, dateTo)));
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/devops/repositories", (Guid customerId, Guid projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "devops.view", out var error)) return error;
    return Results.Ok(store.DevOpsRepositories.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderBy(x => x.Name));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/devops/repositories", (Guid customerId, Guid projectId, DevOpsRepositoryRequest request, IAppStore store, IDevOpsAutomationService devops, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "devops.manage", out var error)) return error;
    if (FindProject(store, customerId, projectId) is null) return Results.NotFound(new { error = "Project not found." });
    try
    {
        var repo = devops.RegisterRepository(new DevOpsRepositoryRegistrationRequest(customerId, projectId, request.Provider, request.Name, request.ProviderRepositoryId, request.RepoUrl, request.DefaultBranch, request.SecretRef, actor));
        return Results.Created($"/api/customers/{customerId}/projects/{projectId}/devops/repositories/{repo.Id}", repo);
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/devops/branches", (Guid customerId, Guid projectId, Guid? repositoryId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "devops.view", out var error)) return error;
    return Results.Ok(store.DevOpsBranches.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && (!repositoryId.HasValue || x.RepositoryId == repositoryId.Value)).OrderByDescending(x => x.CreatedAt));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/devops/repositories/{repositoryId:guid}/branches", (Guid customerId, Guid projectId, Guid repositoryId, DevOpsBranchCreateDto request, IDevOpsAutomationService devops, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "devops.manage", out var error)) return error;
    try
    {
        var branch = devops.CreateBranch(new DevOpsBranchRequest(customerId, projectId, repositoryId, request.BranchName, request.SourceBranch, request.CreatedByAi, actor));
        return Results.Created($"/api/customers/{customerId}/projects/{projectId}/devops/branches/{branch.Id}", branch);
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/devops/pull-requests", (Guid customerId, Guid projectId, Guid? repositoryId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "devops.view", out var error)) return error;
    return Results.Ok(store.DevOpsPullRequests.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && (!repositoryId.HasValue || x.RepositoryId == repositoryId.Value)).OrderByDescending(x => x.CreatedAt));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/devops/repositories/{repositoryId:guid}/pull-requests", (Guid customerId, Guid projectId, Guid repositoryId, DevOpsPullRequestRequest request, IDevOpsAutomationService devops, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "devops.manage", out var error)) return error;
    try
    {
        var pr = devops.CreatePullRequest(new DevOpsPullRequestCreateRequest(customerId, projectId, repositoryId, request.SourceBranch, request.TargetBranch, request.Title, request.Description, request.RiskLevel, request.ChangeAreasCsv, request.CreatedByAi, actor));
        return Results.Created($"/api/customers/{customerId}/projects/{projectId}/devops/pull-requests/{pr.Id}", pr);
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/devops/pull-requests/{pullRequestId:guid}/ai-analyze", async (Guid customerId, Guid projectId, Guid pullRequestId, AiCodeAnalysisDto request, IDevOpsAutomationService devops, IAppStore store, HttpContext http, CancellationToken cancellationToken) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "devops.ai", out var error)) return error;
    var pr = store.DevOpsPullRequests.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == pullRequestId);
    if (pr is null) return Results.NotFound(new { error = "Pull request not found." });
    try
    {
        return Results.Ok(await devops.AnalyzeCodeAsync(new AiCodeAnalysisRequest(customerId, projectId, pr.RepositoryId, pullRequestId, string.IsNullOrWhiteSpace(request.BranchName) ? pr.SourceBranch : request.BranchName, request.DiffText, actor), cancellationToken));
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/devops/pull-requests/{pullRequestId:guid}/ai-patch", async (Guid customerId, Guid projectId, Guid pullRequestId, AiPatchProposalDto request, IDevOpsAutomationService devops, IAppStore store, HttpContext http, CancellationToken cancellationToken) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "devops.ai", out var error)) return error;
    var pr = store.DevOpsPullRequests.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == pullRequestId);
    if (pr is null) return Results.NotFound(new { error = "Pull request not found." });
    try
    {
        return Results.Ok(await devops.ProposePatchAsync(new AiPatchProposalRequest(customerId, projectId, pr.RepositoryId, pullRequestId, string.IsNullOrWhiteSpace(request.BranchName) ? pr.SourceBranch : request.BranchName, request.Intent, actor), cancellationToken));
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/devops/pull-requests/{pullRequestId:guid}/reviews", (Guid customerId, Guid projectId, Guid pullRequestId, DevOpsReviewDto request, IDevOpsAutomationService devops, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "devops.review", out var error)) return error;
    try
    {
        return Results.Ok(devops.AddReview(customerId, projectId, pullRequestId, string.IsNullOrWhiteSpace(request.ReviewerUserId) ? actor : request.ReviewerUserId, request.Decision, request.Comments));
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/devops/pull-requests/{pullRequestId:guid}/submit-approval", (Guid customerId, Guid projectId, Guid pullRequestId, DevOpsApprovalDto request, IDevOpsAutomationService devops, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "devops.approve", out var error)) return error;
    try
    {
        return Results.Ok(devops.SubmitApproval(customerId, projectId, pullRequestId, actor, request.ApproverUserId));
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/devops/pipelines/{pipelineId:guid}/run", async (Guid customerId, Guid projectId, Guid pipelineId, DevOpsPipelineRunDto request, IDevOpsAutomationService devops, IAppStore store, HttpContext http, CancellationToken cancellationToken) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "devops.pipeline.run", out var error)) return error;
    try
    {
        return Results.Ok(await devops.RunPipelineAsync(new PipelineRunRequest(customerId, projectId, request.RepositoryId, pipelineId, request.PullRequestId, request.RunType, request.InputJson, actor), cancellationToken));
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/devops/pipelines", (Guid customerId, Guid projectId, Guid? repositoryId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "devops.view", out var error)) return error;
    return Results.Ok(store.CiCdPipelines.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && (!repositoryId.HasValue || x.RepositoryId == repositoryId.Value)).OrderBy(x => x.PipelineKey));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/devops/pipelines", (Guid customerId, Guid projectId, DevOpsPipelineRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "devops.manage", out var error)) return error;
    var repo = store.DevOpsRepositories.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == request.RepositoryId);
    if (repo is null) return Results.BadRequest(new { error = "Repository not found." });
    if (request.TimeoutSeconds <= 0) return Results.BadRequest(new { error = "Pipeline timeout must be greater than zero." });
    var pipeline = new CiCdPipeline { CustomerId = customerId, ProjectId = projectId, RepositoryId = repo.Id, PipelineKey = request.PipelineKey, Name = request.Name, Provider = request.Provider, ConfigPath = request.ConfigPath, TimeoutSeconds = request.TimeoutSeconds, Active = true };
    store.CiCdPipelines.Add(pipeline);
    audit.Write(customerId, projectId, "DEVOPS_PIPELINE_CREATED", nameof(CiCdPipeline), pipeline.Id, pipeline);
    return Results.Created($"/api/customers/{customerId}/projects/{projectId}/devops/pipelines/{pipeline.Id}", pipeline);
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/devops/pipeline-runs", (Guid customerId, Guid projectId, Guid? pullRequestId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "devops.view", out var error)) return error;
    return Results.Ok(store.PipelineRuns.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && (!pullRequestId.HasValue || x.PullRequestId == pullRequestId)).OrderByDescending(x => x.StartedAt));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/devops/pull-requests/{pullRequestId:guid}/release-package", (Guid customerId, Guid projectId, Guid pullRequestId, DevOpsPackageDto request, IDevOpsAutomationService devops, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "devops.deploy", out var error)) return error;
    try
    {
        return Results.Ok(devops.CreateOrUpdatePackage(new DeploymentPackageRequest(customerId, projectId, pullRequestId, request.Version, actor)));
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/devops/pull-requests/{pullRequestId:guid}/merge", (Guid customerId, Guid projectId, Guid pullRequestId, DevOpsMergeDto request, IDevOpsAutomationService devops, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "devops.merge", out var error)) return error;
    try
    {
        return Results.Ok(devops.MergePullRequest(new MergePullRequestRequest(customerId, projectId, pullRequestId, request.RequestedByAi, actor)));
    }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/devops/deployment-packages", (Guid customerId, Guid projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "devops.view", out var error)) return error;
    return Results.Ok(store.DeploymentPackages.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt));
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/devops/source-snapshots", (Guid customerId, Guid projectId, Guid? repositoryId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "devops.view", out var error)) return error;
    return Results.Ok(store.SourceCodeSnapshots.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && (!repositoryId.HasValue || x.RepositoryId == repositoryId.Value)).OrderByDescending(x => x.CapturedAt));
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/devops/runs", (Guid customerId, Guid projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "devops.view", out var error)) return error;
    return Results.Ok(store.DevOpsRuns.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.StartedAt));
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/devops/runs/{runId:guid}/logs", (Guid customerId, Guid projectId, Guid runId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "devops.view", out var error)) return error;
    var run = store.DevOpsRuns.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == runId);
    if (run is null) return Results.NotFound(new { error = "DevOps run not found." });
    return Results.Ok(store.DevOpsRunLogs.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && x.DevOpsRunId == runId).OrderBy(x => x.CreatedAt));
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/devops/governance-policies", (Guid customerId, Guid projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "devops.view", out var error)) return error;
    return Results.Ok(store.AiCodeGovernancePolicies.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderBy(x => x.PolicyKey));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/devops/governance-policies", (Guid customerId, Guid projectId, AiCodeGovernancePolicyDto request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "devops.policy.manage", out var error)) return error;
    var policy = new AiCodeGovernancePolicy { CustomerId = customerId, ProjectId = projectId, RepositoryId = request.RepositoryId, PolicyKey = request.PolicyKey, ProtectedBranchesCsv = request.ProtectedBranchesCsv, RequireHumanReview = request.RequireHumanReview, BlockDirectMainMerge = request.BlockDirectMainMerge, BlockAiProductionDeploy = request.BlockAiProductionDeploy, HighRiskRequiresApproval = request.HighRiskRequiresApproval, SpecialApprovalAreasCsv = request.SpecialApprovalAreasCsv, MaxDiffBytes = request.MaxDiffBytes <= 0 ? 12000 : request.MaxDiffBytes, Active = request.Active };
    store.AiCodeGovernancePolicies.Add(policy);
    audit.Write(customerId, projectId, "DEVOPS_AI_CODE_POLICY_CREATED", nameof(AiCodeGovernancePolicy), policy.Id, policy);
    return Results.Created($"/api/customers/{customerId}/projects/{projectId}/devops/governance-policies/{policy.Id}", policy);
});

app.MapGet("/api/customers/{customerId:guid}/observability/dashboard", (Guid customerId, Guid? projectId, Guid? environmentId, DateTimeOffset? dateFrom, DateTimeOffset? dateTo, IAppStore store, IObservabilityService observability, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "observability.view", out var error)) return error;
    return Results.Ok(observability.GetDashboard(new ObservabilityDashboardFilter(customerId, projectId, environmentId, dateFrom, dateTo)));
});

app.MapGet("/api/customers/{customerId:guid}/observability/sources", (Guid customerId, Guid? projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "observability.view", out var error)) return error;
    return Results.Ok(store.TelemetrySources.Where(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == projectId || x.ProjectId == null)).OrderBy(x => x.SourceType).ThenBy(x => x.Name));
});

app.MapPost("/api/customers/{customerId:guid}/observability/sources", (Guid customerId, TelemetrySourceRequest request, IAppStore store, IObservabilityService observability, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, request.ProjectId, actor, "observability.manage", out var error)) return error;
    try
    {
        var source = observability.RegisterSource(new TelemetrySourceRegistrationRequest(customerId, request.ProjectId, request.EnvironmentId, request.ConnectorId, request.ProductionReleasePackageId, request.ProductionDeploymentRunId, request.SourceKey, request.Name, request.SourceType, request.EndpointRef, request.PollIntervalSeconds, request.TimeoutSeconds, request.MaskLogs, actor));
        return Results.Created($"/api/customers/{customerId}/observability/sources/{source.Id}", source);
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapPost("/api/customers/{customerId:guid}/observability/sources/{sourceId:guid}/mock-collect", async (Guid customerId, Guid sourceId, Guid? projectId, IAppStore store, IObservabilityService observability, HttpContext http, CancellationToken cancellationToken) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "observability.collect", out var error)) return error;
    try
    {
        return Results.Ok(await observability.CollectMockTelemetryAsync(customerId, projectId, sourceId, actor, cancellationToken));
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapPost("/api/customers/{customerId:guid}/observability/telemetry", (Guid customerId, TelemetryIngestDto request, IAppStore store, IObservabilityService observability, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, request.ProjectId, actor, "observability.collect", out var error)) return error;
    try
    {
        var sample = observability.IngestTelemetry(new TelemetryIngestRequest(customerId, request.ProjectId, request.TelemetrySourceId, request.EnvironmentId, request.ConnectorId, request.ProductionReleasePackageId, request.ProductionDeploymentRunId, request.SignalType, request.HealthStatus, request.MetricName, request.MetricValue, request.Unit, request.ApiLatencyMs, request.UptimePercent, request.Summary, request.PayloadJson, request.CorrelationId), actor);
        return Results.Created($"/api/customers/{customerId}/observability/telemetry/{sample.Id}", sample);
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapGet("/api/customers/{customerId:guid}/observability/telemetry", (Guid customerId, Guid? projectId, Guid? sourceId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "observability.view", out var error)) return error;
    return Results.Ok(store.RuntimeTelemetrySamples.Where(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == projectId || x.ProjectId == null) && (!sourceId.HasValue || x.TelemetrySourceId == sourceId.Value)).OrderByDescending(x => x.ObservedAt).Take(200));
});

app.MapGet("/api/customers/{customerId:guid}/observability/log-summaries", (Guid customerId, Guid? projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "observability.view", out var error)) return error;
    return Results.Ok(store.TelemetryLogSummaries.Where(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == projectId || x.ProjectId == null)).OrderByDescending(x => x.ObservedAt).Take(100));
});

app.MapGet("/api/customers/{customerId:guid}/observability/monitoring-rules", (Guid customerId, Guid? projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "observability.view", out var error)) return error;
    return Results.Ok(store.MonitoringRules.Where(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == projectId || x.ProjectId == null)).OrderBy(x => x.RuleKey));
});

app.MapPost("/api/customers/{customerId:guid}/observability/monitoring-rules", (Guid customerId, MonitoringRuleRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, request.ProjectId, actor, "observability.manage", out var error)) return error;
    var rule = new MonitoringRule { CustomerId = customerId, ProjectId = request.ProjectId, TelemetrySourceId = request.TelemetrySourceId, RuleKey = request.RuleKey, Name = request.Name, SignalType = request.SignalType, MetricName = request.MetricName, Operator = request.Operator, ThresholdValue = request.ThresholdValue, MatchText = request.MatchText, Severity = request.Severity, AutoCreateIncident = request.AutoCreateIncident, AutoCreateIssue = request.AutoCreateIssue, Active = request.Active };
    store.MonitoringRules.Add(rule);
    audit.Write(customerId, request.ProjectId, "OBSERVABILITY_MONITORING_RULE_CREATED", nameof(MonitoringRule), rule.Id, rule);
    return Results.Created($"/api/customers/{customerId}/observability/monitoring-rules/{rule.Id}", rule);
});

app.MapPost("/api/customers/{customerId:guid}/observability/evaluate", (Guid customerId, Guid? projectId, Guid? sourceId, IAppStore store, IObservabilityService observability, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "observability.evaluate", out var error)) return error;
    return Results.Ok(observability.EvaluateMonitoring(new MonitoringEvaluationRequest(customerId, projectId, sourceId, actor)));
});

app.MapGet("/api/customers/{customerId:guid}/observability/alert-rules", (Guid customerId, Guid? projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "observability.view", out var error)) return error;
    return Results.Ok(store.AlertRules.Where(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == projectId || x.ProjectId == null)).OrderBy(x => x.AlertKey));
});

app.MapPost("/api/customers/{customerId:guid}/observability/alert-rules", (Guid customerId, AlertRuleRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, request.ProjectId, actor, "observability.manage", out var error)) return error;
    var rule = new AlertRule { CustomerId = customerId, ProjectId = request.ProjectId, MonitoringRuleId = request.MonitoringRuleId, AlertKey = request.AlertKey, MinimumSeverity = request.MinimumSeverity, Channel = request.Channel, RecipientRef = request.RecipientRef, CreateNotification = request.CreateNotification, CreateEscalationForCritical = request.CreateEscalationForCritical, Active = request.Active };
    store.AlertRules.Add(rule);
    audit.Write(customerId, request.ProjectId, "OBSERVABILITY_ALERT_RULE_CREATED", nameof(AlertRule), rule.Id, rule);
    return Results.Created($"/api/customers/{customerId}/observability/alert-rules/{rule.Id}", rule);
});

app.MapGet("/api/customers/{customerId:guid}/observability/alerts", (Guid customerId, Guid? projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "observability.view", out var error)) return error;
    return Results.Ok(store.AlertEvents.Where(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == projectId || x.ProjectId == null)).OrderByDescending(x => x.TriggeredAt));
});

app.MapPost("/api/customers/{customerId:guid}/observability/alerts/{alertId:guid}/ack", (Guid customerId, Guid alertId, Guid? projectId, IAppStore store, IObservabilityService observability, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "observability.incident.manage", out var error)) return error;
    try { return Results.Ok(observability.AcknowledgeAlert(customerId, projectId, alertId, actor)); }
    catch (InvalidOperationException ex) { return Results.NotFound(new { error = ex.Message }); }
});

app.MapGet("/api/customers/{customerId:guid}/observability/incidents", (Guid customerId, Guid? projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "observability.view", out var error)) return error;
    return Results.Ok(store.IncidentRecords.Where(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == projectId || x.ProjectId == null)).OrderByDescending(x => x.DetectedAt));
});

app.MapPost("/api/customers/{customerId:guid}/observability/incidents", (Guid customerId, IncidentRequest request, IAppStore store, IObservabilityService observability, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, request.ProjectId, actor, "observability.incident.manage", out var error)) return error;
    try
    {
        var incident = observability.CreateIncident(new IncidentCreateRequest(customerId, request.ProjectId, request.EnvironmentId, request.ConnectorId, request.ProductionReleasePackageId, request.ProductionDeploymentRunId, request.IssueId, request.SlaPolicyId, request.AlertEventId, request.Title, request.Description, request.Severity, request.Priority, request.ImpactSummary, actor));
        return Results.Created($"/api/customers/{customerId}/observability/incidents/{incident.Id}", incident);
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapPost("/api/customers/{customerId:guid}/observability/incidents/{incidentId:guid}/convert-to-issue", (Guid customerId, Guid incidentId, Guid projectId, IAppStore store, IObservabilityService observability, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "observability.incident.manage", out var error)) return error;
    try { return Results.Ok(observability.ConvertIncidentToIssue(customerId, projectId, incidentId, actor)); }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapPost("/api/customers/{customerId:guid}/observability/incidents/{incidentId:guid}/ai-diagnose", async (Guid customerId, Guid incidentId, Guid? projectId, IAppStore store, IObservabilityService observability, HttpContext http, CancellationToken cancellationToken) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "observability.ai", out var error)) return error;
    try { return Results.Ok(await observability.DiagnoseIncidentAsync(customerId, projectId, incidentId, actor, cancellationToken)); }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapPost("/api/customers/{customerId:guid}/observability/incidents/{incidentId:guid}/post-review", (Guid customerId, Guid incidentId, Guid? projectId, IAppStore store, IObservabilityService observability, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "observability.incident.manage", out var error)) return error;
    try { return Results.Ok(observability.CreatePostIncidentReview(customerId, projectId, incidentId, actor)); }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapPost("/api/customers/{customerId:guid}/observability/incidents/{incidentId:guid}/resolve", (Guid customerId, Guid incidentId, Guid? projectId, IncidentResolveDto request, IAppStore store, IObservabilityService observability, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "observability.incident.manage", out var error)) return error;
    try { return Results.Ok(observability.ResolveIncident(customerId, projectId, incidentId, actor, request.Resolution)); }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapGet("/api/customers/{customerId:guid}/observability/incident-actions", (Guid customerId, Guid? projectId, Guid? incidentId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "observability.view", out var error)) return error;
    return Results.Ok(store.IncidentActions.Where(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == projectId || x.ProjectId == null) && (!incidentId.HasValue || x.IncidentId == incidentId.Value)).OrderByDescending(x => x.CreatedAt));
});

app.MapGet("/api/customers/{customerId:guid}/observability/ai-diagnoses", (Guid customerId, Guid? projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "observability.view", out var error)) return error;
    return Results.Ok(store.AiIncidentDiagnoses.Where(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == projectId || x.ProjectId == null)).OrderByDescending(x => x.CreatedAt));
});

app.MapGet("/api/customers/{customerId:guid}/observability/post-incident-reviews", (Guid customerId, Guid? projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "observability.view", out var error)) return error;
    return Results.Ok(store.PostIncidentReviews.Where(x => x.CustomerId == customerId && (!projectId.HasValue || x.ProjectId == projectId || x.ProjectId == null)).OrderByDescending(x => x.CreatedAt));
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/data-migration/dashboard", (Guid customerId, Guid projectId, Guid? environmentId, IAppStore store, IDataMigrationService migration, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "data.migration.view", out var error)) return error;
    if (FindProject(store, customerId, projectId) is null) return Results.NotFound(new { error = "Project not found." });
    return Results.Ok(migration.GetDashboard(new DataMigrationDashboardFilter(customerId, projectId, environmentId)));
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/data-migration/templates", (Guid customerId, Guid projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "data.migration.view", out var error)) return error;
    return Results.Ok(store.DataImportTemplates.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderBy(x => x.Domain).ThenBy(x => x.Name));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/data-migration/templates", (Guid customerId, Guid projectId, DataImportTemplateDto request, IAppStore store, IDataMigrationService migration, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "data.migration.manage", out var error)) return error;
    try
    {
        var template = migration.CreateTemplate(new DataImportTemplateRequest(customerId, projectId, request.TemplateKey, request.Name, request.Domain, request.DefaultFileType, request.MaxClassification, request.SchemaJson, request.SampleFileRef, actor));
        return Results.Created($"/api/customers/{customerId}/projects/{projectId}/data-migration/templates/{template.Id}", template);
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/data-migration/template-versions", (Guid customerId, Guid projectId, Guid? templateId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "data.migration.view", out var error)) return error;
    return Results.Ok(store.DataImportTemplateVersions.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && (!templateId.HasValue || x.TemplateId == templateId.Value)).OrderByDescending(x => x.Version));
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/data-migration/files", (Guid customerId, Guid projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "data.migration.view", out var error)) return error;
    var canViewSensitive = RequirePermission(store, customerId, projectId, actor, "data.migration.sensitive.preview", out _);
    return Results.Ok(store.DataImportFiles.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt).Select(x => new { file = x, previewJson = canViewSensitive ? x.MaskedPreviewJson : x.MaskedPreviewJson }));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/data-migration/files", (Guid customerId, Guid projectId, DataImportFileDto request, IAppStore store, IDataMigrationService migration, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "data.migration.upload", out var error)) return error;
    var canViewSensitive = RequirePermission(store, customerId, projectId, actor, "data.migration.sensitive.preview", out _);
    try
    {
        var file = migration.RegisterFile(new DataImportFileRequest(customerId, projectId, request.EnvironmentId, request.TemplateId, request.FileRef, request.FileName, request.FileType, request.SizeBytes, request.RowCount, request.Classification, request.PreviewJson, actor), canViewSensitive);
        return Results.Created($"/api/customers/{customerId}/projects/{projectId}/data-migration/files/{file.Id}", file);
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/data-migration/mappings", (Guid customerId, Guid projectId, Guid? templateId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "data.migration.view", out var error)) return error;
    return Results.Ok(store.DataColumnMappings.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && (!templateId.HasValue || x.TemplateId == templateId.Value)).OrderBy(x => x.MappingKey).ThenByDescending(x => x.MappingVersion));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/data-migration/mappings", (Guid customerId, Guid projectId, DataColumnMappingDto request, IAppStore store, IDataMigrationService migration, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "data.migration.manage", out var error)) return error;
    try
    {
        return Results.Ok(migration.CreateMapping(new DataColumnMappingRequest(customerId, projectId, request.TemplateId, request.TemplateVersion, request.MappingKey, request.SourceColumn, request.TargetEntity, request.TargetField, request.TransformExpression, request.DataClassification)));
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/data-migration/validation-rules", (Guid customerId, Guid projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "data.migration.view", out var error)) return error;
    return Results.Ok(store.DataValidationRules.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderBy(x => x.Domain).ThenBy(x => x.RuleKey));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/data-migration/validation-rules", (Guid customerId, Guid projectId, DataValidationRuleDto request, IAppStore store, IDataMigrationService migration, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "data.migration.manage", out var error)) return error;
    return Results.Ok(migration.CreateValidationRule(new DataValidationRuleRequest(customerId, projectId, request.TemplateId, request.RuleKey, request.Name, request.Domain, request.TargetField, request.RuleType, request.ExpressionJson, request.Severity)));
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/data-migration/batches", (Guid customerId, Guid projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "data.migration.view", out var error)) return error;
    return Results.Ok(store.DataImportBatches.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/data-migration/batches", (Guid customerId, Guid projectId, DataImportBatchDto request, IAppStore store, IDataMigrationService migration, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "data.migration.manage", out var error)) return error;
    try
    {
        var batch = migration.CreateBatch(new DataImportBatchRequest(customerId, projectId, request.EnvironmentId, request.ConnectorId, request.TemplateId, request.TemplateVersion, request.ImportFileId, request.Domain, actor));
        return Results.Created($"/api/customers/{customerId}/projects/{projectId}/data-migration/batches/{batch.Id}", batch);
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/data-migration/batches/{batchId:guid}/dry-run", async (Guid customerId, Guid projectId, Guid batchId, IAppStore store, IDataMigrationService migration, HttpContext http, CancellationToken cancellationToken) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "data.migration.execute", out var error)) return error;
    try { return Results.Ok(await migration.DryRunAsync(customerId, projectId, batchId, actor, cancellationToken)); }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/data-migration/batches/{batchId:guid}/apply-test-uat", async (Guid customerId, Guid projectId, Guid batchId, DataImportApplyDto request, IAppStore store, IDataMigrationService migration, HttpContext http, CancellationToken cancellationToken) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "data.migration.execute", out var error)) return error;
    if (!request.Confirmed) return Results.BadRequest(new { error = "User confirmation is required before apply. AI cannot import without user confirmation." });
    try { return Results.Ok(await migration.ApplyToTestUatAsync(customerId, projectId, batchId, actor, cancellationToken)); }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/data-migration/batches/{batchId:guid}/reconcile", (Guid customerId, Guid projectId, Guid batchId, IAppStore store, IDataMigrationService migration, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "data.migration.execute", out var error)) return error;
    try { return Results.Ok(migration.Reconcile(customerId, projectId, batchId, actor)); }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/data-migration/ai-assistance", async (Guid customerId, Guid projectId, DataAiAssistanceDto request, IAppStore store, IDataMigrationService migration, HttpContext http, CancellationToken cancellationToken) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "data.migration.ai", out var error)) return error;
    try { return Results.Ok(await migration.GenerateAiAssistanceAsync(customerId, projectId, request.BatchId, request.TemplateId, request.AssistanceType, request.Context, actor, cancellationToken)); }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/data-migration/batches/{batchId:guid}/sign-off", (Guid customerId, Guid projectId, Guid batchId, DataSignOffDto request, IAppStore store, IDataMigrationService migration, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "data.migration.signoff", out var error)) return error;
    try { return Results.Ok(migration.SignOff(customerId, projectId, batchId, request.SignedBy, request.Role, request.Comment)); }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/data-migration/runs", (Guid customerId, Guid projectId, Guid? batchId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "data.migration.view", out var error)) return error;
    return Results.Ok(store.DataImportRuns.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && (!batchId.HasValue || x.BatchId == batchId.Value)).OrderByDescending(x => x.StartedAt));
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/data-migration/validation-issues", (Guid customerId, Guid projectId, Guid? runId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "data.migration.view", out var error)) return error;
    return Results.Ok(store.DataValidationIssues.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && (!runId.HasValue || x.ImportRunId == runId.Value)).OrderByDescending(x => x.CreatedAt));
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/data-migration/reconciliation-reports", (Guid customerId, Guid projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "data.migration.view", out var error)) return error;
    return Results.Ok(store.DataReconciliationReports.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt));
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/data-migration/sign-offs", (Guid customerId, Guid projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "data.migration.view", out var error)) return error;
    return Results.Ok(store.DataSignOffs.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt));
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/data-migration/ai-assistance", (Guid customerId, Guid projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!RequirePermission(store, customerId, projectId, actor, "data.migration.view", out var error)) return error;
    return Results.Ok(store.AiDataMigrationAssistances.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt));
});

app.MapGet("/api/portal/customers/{customerId:guid}/dashboard", (Guid customerId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    return Results.Ok(PortalDashboardSummary(store, customerId, actor));
});

app.MapGet("/api/portal/customers/{customerId:guid}/projects", (Guid customerId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    return Results.Ok(PortalProjectsForUser(store, customerId, actor));
});

app.MapGet("/api/portal/customers/{customerId:guid}/projects/{projectId:guid}/workspace", (Guid customerId, Guid projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor) || !PortalUserCanAccessProject(store, customerId, projectId, actor)) return Results.Json(new { error = "Portal project access denied." }, statusCode: StatusCodes.Status403Forbidden);
    if (FindProject(store, customerId, projectId) is null) return Results.NotFound(new { error = "Project not found." });
    return Results.Ok(PortalProjectWorkspace(store, customerId, projectId, actor));
});

app.MapGet("/api/portal/customers/{customerId:guid}/users", (Guid customerId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    return Results.Ok(store.PortalUsers.Where(x => x.CustomerId == customerId).OrderBy(x => x.DisplayName));
});

app.MapPost("/api/portal/customers/{customerId:guid}/users", (Guid customerId, PortalUserRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var user = new PortalUser { CustomerId = customerId, UserId = request.UserId, DisplayName = request.DisplayName, Email = request.Email, RoleKey = request.RoleKey, CanViewBilling = request.CanViewBilling, CanViewReports = request.CanViewReports, CanApprove = request.CanApprove };
    store.PortalUsers.Add(user);
    store.TenantAccessGrants.Add(new TenantAccessGrant { CustomerId = customerId, UserId = request.UserId, RoleKey = request.RoleKey, GrantedBy = actor });
    audit.Write(customerId, null, "PORTAL_USER_CREATED", nameof(PortalUser), user.Id, user);
    return Results.Created($"/api/portal/customers/{customerId}/users/{user.Id}", user);
});

app.MapPut("/api/portal/customers/{customerId:guid}/users/{portalUserId:guid}", (Guid customerId, Guid portalUserId, PortalUserRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var user = store.PortalUsers.SingleOrDefault(x => x.CustomerId == customerId && x.Id == portalUserId);
    if (user is null) return Results.NotFound(new { error = "Portal user not found." });
    user.DisplayName = request.DisplayName;
    user.Email = request.Email;
    user.RoleKey = request.RoleKey;
    user.CanViewBilling = request.CanViewBilling;
    user.CanViewReports = request.CanViewReports;
    user.CanApprove = request.CanApprove;
    user.UpdatedAt = DateTimeOffset.UtcNow;
    audit.Write(customerId, null, "PORTAL_USER_UPDATED", nameof(PortalUser), user.Id, user);
    return Results.Ok(user);
});

app.MapPost("/api/portal/customers/{customerId:guid}/users/{portalUserId:guid}/disable", (Guid customerId, Guid portalUserId, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var user = store.PortalUsers.SingleOrDefault(x => x.CustomerId == customerId && x.Id == portalUserId);
    if (user is null) return Results.NotFound(new { error = "Portal user not found." });
    user.Status = "Disabled";
    user.UpdatedAt = DateTimeOffset.UtcNow;
    foreach (var grant in store.TenantAccessGrants.Where(x => x.CustomerId == customerId && x.UserId == user.UserId)) grant.Status = TenantAccessStatus.Revoked;
    audit.Write(customerId, null, "PORTAL_USER_DISABLED", nameof(PortalUser), user.Id, user);
    return Results.Ok(user);
});

app.MapGet("/api/portal/customers/{customerId:guid}/requests", (Guid customerId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    return Results.Ok(store.PortalRequests.Where(x => x.CustomerId == customerId && x.Visibility == PortalVisibility.CustomerVisible).OrderByDescending(x => x.CreatedAt));
});

app.MapPost("/api/portal/customers/{customerId:guid}/projects/{projectId:guid}/requests", (Guid customerId, Guid projectId, PortalRequestDto request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor) || !PortalUserCanAccessProject(store, customerId, projectId, actor)) return Results.Json(new { error = "Portal project access denied." }, statusCode: StatusCodes.Status403Forbidden);
    if (FindProject(store, customerId, projectId) is null) return Results.NotFound(new { error = "Project not found." });
    var portalRequest = new PortalRequest { CustomerId = customerId, ProjectId = projectId, RequestNo = store.NextNumber("PREQ"), RequestType = request.RequestType, Title = request.Title, Description = request.Description, Priority = request.Priority, SubmittedByUserId = actor };
    store.PortalRequests.Add(portalRequest);
    CreatePortalNotification(store, customerId, projectId, null, NotificationType.TicketUpdate, "Customer request created", portalRequest.Title, nameof(PortalRequest), portalRequest.Id);
    audit.Write(customerId, projectId, "PORTAL_REQUEST_CREATED", nameof(PortalRequest), portalRequest.Id, portalRequest);
    return Results.Created($"/api/portal/customers/{customerId}/requests/{portalRequest.Id}", portalRequest);
});

app.MapGet("/api/portal/customers/{customerId:guid}/requests/{requestId:guid}", (Guid customerId, Guid requestId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var request = store.PortalRequests.SingleOrDefault(x => x.CustomerId == customerId && x.Id == requestId && x.Visibility == PortalVisibility.CustomerVisible);
    return request is null ? Results.NotFound(new { error = "Portal request not found." }) : Results.Ok(new { request, timeline = PortalTimeline(store, customerId, request.ProjectId, nameof(PortalRequest), request.Id) });
});

app.MapPost("/api/portal/customers/{customerId:guid}/requests/{requestId:guid}/submit", (Guid customerId, Guid requestId, IAppStore store, IAuditWriter audit, INotificationDeliveryProvider deliveryProvider, IDataMaskingService masking, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var request = store.PortalRequests.SingleOrDefault(x => x.CustomerId == customerId && x.Id == requestId);
    if (request is null) return Results.NotFound(new { error = "Portal request not found." });
    request.Status = PortalRequestStatus.Submitted;
    request.SubmittedAt = DateTimeOffset.UtcNow;
    request.UpdatedAt = DateTimeOffset.UtcNow;
    CreatePortalNotification(store, customerId, request.ProjectId, null, NotificationType.TicketUpdate, "Customer request submitted", request.Title, nameof(PortalRequest), request.Id);
    ExecuteWorkflowEvent(store, audit, deliveryProvider, masking, customerId, request.ProjectId, WorkflowTriggerType.CustomerRequestSubmitted, nameof(PortalRequest), request.Id, request.Title, actor);
    audit.Write(customerId, request.ProjectId, "PORTAL_REQUEST_SUBMITTED", nameof(PortalRequest), request.Id, request);
    return Results.Ok(request);
});

app.MapPost("/api/portal/customers/{customerId:guid}/requests/{requestId:guid}/convert-to-issue", (Guid customerId, Guid requestId, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var request = store.PortalRequests.SingleOrDefault(x => x.CustomerId == customerId && x.Id == requestId);
    if (request is null) return Results.NotFound(new { error = "Portal request not found." });
    var issue = new Issue { CustomerId = customerId, ProjectId = request.ProjectId, IssueNo = store.NextNumber("ISS"), Title = request.Title, Description = request.Description, Category = IssueCategory.Functional, Severity = IssueSeverity.Medium, RiskLevel = RiskLevel.Medium, ReportedBy = request.SubmittedByUserId };
    store.Issues.Add(issue);
    request.ConvertedIssueId = issue.Id;
    request.Status = PortalRequestStatus.InProgress;
    store.TraceLinks.Add(NewTrace(customerId, request.ProjectId, nameof(PortalRequest), request.Id, nameof(Issue), issue.Id, "PortalRequestIssue"));
    audit.Write(customerId, request.ProjectId, "PORTAL_REQUEST_CONVERTED_TO_ISSUE", nameof(PortalRequest), request.Id, new { request, issue });
    return Results.Ok(new { request, issue });
});

app.MapPost("/api/portal/customers/{customerId:guid}/requests/{requestId:guid}/convert-to-service-request", (Guid customerId, Guid requestId, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var request = store.PortalRequests.SingleOrDefault(x => x.CustomerId == customerId && x.Id == requestId);
    if (request is null) return Results.NotFound(new { error = "Portal request not found." });
    var serviceRequest = new ServiceRequest { CustomerId = customerId, ProjectId = request.ProjectId, RequestNo = store.NextNumber("SRQ"), RequestType = request.RequestType, Title = request.Title, Description = request.Description, RiskLevel = RiskLevel.Medium, EstimatedHours = 4, RequestedBy = request.SubmittedByUserId };
    store.ServiceRequests.Add(serviceRequest);
    request.ConvertedServiceRequestId = serviceRequest.Id;
    request.Status = PortalRequestStatus.InProgress;
    store.TraceLinks.Add(NewTrace(customerId, request.ProjectId, nameof(PortalRequest), request.Id, nameof(ServiceRequest), serviceRequest.Id, "PortalRequestServiceRequest"));
    audit.Write(customerId, request.ProjectId, "PORTAL_REQUEST_CONVERTED_TO_SERVICE_REQUEST", nameof(PortalRequest), request.Id, new { request, serviceRequest });
    return Results.Ok(new { request, serviceRequest });
});

app.MapPost("/api/portal/customers/{customerId:guid}/requests/{requestId:guid}/convert-to-change-request", (Guid customerId, Guid requestId, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var request = store.PortalRequests.SingleOrDefault(x => x.CustomerId == customerId && x.Id == requestId);
    if (request is null) return Results.NotFound(new { error = "Portal request not found." });
    var targetEnvironment = store.Environments.FirstOrDefault(x => x.CustomerId == customerId && x.ProjectId == request.ProjectId && x.Kind != EnvironmentKind.Production) ?? store.Environments.FirstOrDefault(x => x.CustomerId == customerId && x.ProjectId == request.ProjectId);
    if (targetEnvironment is null) return Results.BadRequest(new { error = "No target environment available for change request conversion." });
    var changeRequest = new ChangeRequest { CustomerId = customerId, ProjectId = request.ProjectId, CrNo = store.NextNumber("CR"), Title = request.Title, Description = request.Description, TargetEnvironmentId = targetEnvironment.Id, RequiresApproval = true };
    store.ChangeRequests.Add(changeRequest);
    request.ConvertedChangeRequestId = changeRequest.Id;
    request.Status = PortalRequestStatus.WaitingForApproval;
    store.TraceLinks.Add(NewTrace(customerId, request.ProjectId, nameof(PortalRequest), request.Id, nameof(ChangeRequest), changeRequest.Id, "PortalRequestChangeRequest"));
    audit.Write(customerId, request.ProjectId, "PORTAL_REQUEST_CONVERTED_TO_CHANGE_REQUEST", nameof(PortalRequest), request.Id, new { request, changeRequest });
    return Results.Ok(new { request, changeRequest });
});

app.MapGet("/api/portal/customers/{customerId:guid}/projects/{projectId:guid}/requirement-intakes", (Guid customerId, Guid projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor) || !PortalUserCanAccessProject(store, customerId, projectId, actor)) return Results.Json(new { error = "Portal project access denied." }, statusCode: StatusCodes.Status403Forbidden);
    return Results.Ok(store.PortalRequirementIntakes.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt));
});

app.MapPost("/api/portal/customers/{customerId:guid}/projects/{projectId:guid}/requirement-intakes", (Guid customerId, Guid projectId, PortalRequirementIntakeRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor) || !PortalUserCanAccessProject(store, customerId, projectId, actor)) return Results.Json(new { error = "Portal project access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var intake = new PortalRequirementIntake { CustomerId = customerId, ProjectId = projectId, PortalRequestId = request.PortalRequestId, Title = request.Title, BusinessContext = request.BusinessContext, RequirementText = request.RequirementText, CreatedByUserId = actor };
    store.PortalRequirementIntakes.Add(intake);
    audit.Write(customerId, projectId, "PORTAL_REQUIREMENT_INTAKE_CREATED", nameof(PortalRequirementIntake), intake.Id, intake);
    return Results.Created($"/api/portal/customers/{customerId}/requirement-intakes/{intake.Id}", intake);
});

app.MapPost("/api/portal/customers/{customerId:guid}/requirement-intakes/{intakeId:guid}/convert-to-requirement", (Guid customerId, Guid intakeId, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var intake = store.PortalRequirementIntakes.SingleOrDefault(x => x.CustomerId == customerId && x.Id == intakeId);
    if (intake is null) return Results.NotFound(new { error = "Requirement intake not found." });
    var requirement = new Requirement { CustomerId = customerId, ProjectId = intake.ProjectId, VersionGroupId = Guid.NewGuid(), RequirementNo = store.NextNumber("REQ"), Title = intake.Title, ContentText = $"{intake.BusinessContext}\n\n{intake.RequirementText}", SourceType = "Portal", CreatedBy = intake.CreatedByUserId };
    store.Requirements.Add(requirement);
    intake.ConvertedRequirementId = requirement.Id;
    intake.Status = PortalRequestStatus.InReview;
    store.TraceLinks.Add(NewTrace(customerId, intake.ProjectId, nameof(PortalRequirementIntake), intake.Id, nameof(Requirement), requirement.Id, "PortalIntakeRequirement"));
    audit.Write(customerId, intake.ProjectId, "PORTAL_REQUIREMENT_INTAKE_CONVERTED", nameof(PortalRequirementIntake), intake.Id, new { intake, requirement });
    return Results.Ok(new { intake, requirement });
});

app.MapGet("/api/portal/customers/{customerId:guid}/projects/{projectId:guid}/documents/shared", (Guid customerId, Guid projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor) || !PortalUserCanAccessProject(store, customerId, projectId, actor)) return Results.Json(new { error = "Portal project access denied." }, statusCode: StatusCodes.Status403Forbidden);
    return Results.Ok(store.PortalDocumentShares.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Visibility == PortalVisibility.CustomerVisible && x.Status == "Published").Select(x => PortalDocumentDto(store, x)));
});

app.MapPost("/api/portal/customers/{customerId:guid}/projects/{projectId:guid}/documents/share", (Guid customerId, Guid projectId, PortalDocumentShareRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    if (FindProject(store, customerId, projectId) is null) return Results.NotFound(new { error = "Project not found." });
    var share = new PortalDocumentShare { CustomerId = customerId, ProjectId = projectId, DocumentType = request.DocumentType, DocumentId = request.DocumentId, DocumentVersion = request.DocumentVersion, Visibility = request.Visibility, SharedBy = actor, ExpiresAt = request.ExpiresAt };
    store.PortalDocumentShares.Add(share);
    audit.Write(customerId, projectId, "PORTAL_DOCUMENT_SHARED", nameof(PortalDocumentShare), share.Id, share);
    return Results.Created($"/api/portal/customers/{customerId}/documents/{share.Id}", share);
});

app.MapPost("/api/portal/customers/{customerId:guid}/documents/{shareId:guid}/review", (Guid customerId, Guid shareId, PortalDocumentReviewRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var share = store.PortalDocumentShares.SingleOrDefault(x => x.CustomerId == customerId && x.Id == shareId && x.Visibility == PortalVisibility.CustomerVisible);
    if (share is null) return Results.NotFound(new { error = "Shared document not found." });
    var review = new PortalDocumentReview { CustomerId = customerId, ProjectId = share.ProjectId, DocumentShareId = share.Id, ReviewerUserId = actor, Status = request.Status, Comment = request.Comment ?? "", ReviewedAt = DateTimeOffset.UtcNow };
    store.PortalDocumentReviews.Add(review);
    audit.Write(customerId, share.ProjectId, "PORTAL_DOCUMENT_REVIEWED", nameof(PortalDocumentReview), review.Id, review);
    return Results.Created($"/api/portal/customers/{customerId}/documents/{shareId}/reviews/{review.Id}", review);
});

app.MapGet("/api/portal/customers/{customerId:guid}/approvals", (Guid customerId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var user = PortalUserForActor(store, customerId, actor);
    var approvals = store.PortalApprovals.Where(x => x.CustomerId == customerId && (user is null || x.ApproverPortalUserId == null || x.ApproverPortalUserId == user.Id)).OrderByDescending(x => x.CreatedAt);
    return Results.Ok(approvals);
});

app.MapPost("/api/portal/customers/{customerId:guid}/approvals/{approvalId:guid}/approve", (Guid customerId, Guid approvalId, PortalApprovalDecisionRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var approval = store.PortalApprovals.SingleOrDefault(x => x.CustomerId == customerId && x.Id == approvalId);
    if (approval is null) return Results.NotFound(new { error = "Portal approval not found." });
    var user = PortalUserForActor(store, customerId, actor);
    if (!string.Equals(actor, "security.admin", StringComparison.OrdinalIgnoreCase) && user?.CanApprove != true) return Results.Json(new { error = "Portal approval permission denied." }, statusCode: StatusCodes.Status403Forbidden);
    approval.Status = PortalApprovalStatus.Approved;
    approval.Comment = request.Comment ?? "";
    approval.DecidedAt = DateTimeOffset.UtcNow;
    CreatePortalNotification(store, customerId, approval.ProjectId, approval.ApproverPortalUserId, NotificationType.ApprovalResult, "Portal approval approved", approval.SourceEntityType, nameof(PortalApproval), approval.Id);
    audit.Write(customerId, approval.ProjectId, "PORTAL_APPROVAL_APPROVED", nameof(PortalApproval), approval.Id, approval);
    return Results.Ok(approval);
});

app.MapPost("/api/portal/customers/{customerId:guid}/approvals/{approvalId:guid}/reject", (Guid customerId, Guid approvalId, PortalApprovalDecisionRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var approval = store.PortalApprovals.SingleOrDefault(x => x.CustomerId == customerId && x.Id == approvalId);
    if (approval is null) return Results.NotFound(new { error = "Portal approval not found." });
    approval.Status = PortalApprovalStatus.Rejected;
    approval.Comment = request.Comment ?? "";
    approval.DecidedAt = DateTimeOffset.UtcNow;
    CreatePortalNotification(store, customerId, approval.ProjectId, approval.ApproverPortalUserId, NotificationType.ApprovalResult, "Portal approval rejected", approval.SourceEntityType, nameof(PortalApproval), approval.Id);
    audit.Write(customerId, approval.ProjectId, "PORTAL_APPROVAL_REJECTED", nameof(PortalApproval), approval.Id, approval);
    return Results.Ok(approval);
});

app.MapGet("/api/portal/customers/{customerId:guid}/sla/summary", (Guid customerId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    return Results.Ok(new
    {
        policies = store.SlaPolicies.Where(x => x.CustomerId == customerId).OrderBy(x => x.Severity),
        tickets = store.CustomerPortalTickets.Where(x => x.CustomerId == customerId).OrderByDescending(x => x.SubmittedAt),
        breached = store.CustomerPortalTickets.Count(x => x.CustomerId == customerId && x.SlaStatus == SlaStatus.Breached),
        warnings = store.CustomerPortalTickets.Count(x => x.CustomerId == customerId && x.SlaStatus == SlaStatus.Warning)
    });
});

app.MapGet("/api/portal/customers/{customerId:guid}/sla/tickets", (Guid customerId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    return Results.Ok(store.CustomerPortalTickets.Where(x => x.CustomerId == customerId).OrderByDescending(x => x.SubmittedAt));
});

app.MapGet("/api/portal/customers/{customerId:guid}/knowledge", (Guid customerId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    return Results.Ok(store.PortalKnowledgeArticles.Where(x => x.CustomerId == customerId && x.Visibility == PortalVisibility.CustomerVisible && x.Status == "Published").OrderBy(x => x.Category).ThenBy(x => x.Title));
});

app.MapGet("/api/portal/customers/{customerId:guid}/training", (Guid customerId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    return Results.Ok(store.PortalTrainingSections.Where(x => x.CustomerId == customerId && x.Visibility == PortalVisibility.CustomerVisible && x.Status == "Published").OrderBy(x => x.ModuleName).ThenBy(x => x.Title));
});

app.MapGet("/api/portal/customers/{customerId:guid}/ai-chat/sessions", (Guid customerId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var user = PortalUserForActor(store, customerId, actor);
    return Results.Ok(store.PortalAiChatSessions.Where(x => x.CustomerId == customerId && (user == null || x.PortalUserId == user.Id)).OrderByDescending(x => x.CreatedAt));
});

app.MapPost("/api/portal/customers/{customerId:guid}/projects/{projectId:guid}/ai-chat/sessions", (Guid customerId, Guid projectId, PortalAiChatSessionRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor) || !PortalUserCanAccessProject(store, customerId, projectId, actor)) return Results.Json(new { error = "Portal project access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var user = EnsurePortalUser(store, customerId, actor);
    var session = new PortalAiChatSession { CustomerId = customerId, ProjectId = projectId, PortalUserId = user.Id, Title = request.Title, ContextPolicy = "ApprovedPublishedCustomerVisibleOnly" };
    store.PortalAiChatSessions.Add(session);
    audit.Write(customerId, projectId, "PORTAL_AI_CHAT_SESSION_CREATED", nameof(PortalAiChatSession), session.Id, session);
    return Results.Created($"/api/portal/customers/{customerId}/ai-chat/sessions/{session.Id}", session);
});

app.MapGet("/api/portal/customers/{customerId:guid}/ai-chat/sessions/{sessionId:guid}", (Guid customerId, Guid sessionId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var session = store.PortalAiChatSessions.SingleOrDefault(x => x.CustomerId == customerId && x.Id == sessionId);
    if (session is null) return Results.NotFound(new { error = "AI chat session not found." });
    return Results.Ok(new { session, messages = store.PortalAiChatMessages.Where(x => x.CustomerId == customerId && x.SessionId == sessionId).OrderBy(x => x.CreatedAt) });
});

app.MapPost("/api/portal/customers/{customerId:guid}/ai-chat/sessions/{sessionId:guid}/messages", (Guid customerId, Guid sessionId, PortalAiChatMessageRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var session = store.PortalAiChatSessions.SingleOrDefault(x => x.CustomerId == customerId && x.Id == sessionId);
    if (session is null) return Results.NotFound(new { error = "AI chat session not found." });
    var masked = MaskByClassification(store, customerId, session.ProjectId, "PortalAiChat", request.Message);
    var userMessage = new PortalAiChatMessage { CustomerId = customerId, ProjectId = session.ProjectId, SessionId = session.Id, SenderType = "User", Message = request.Message, MaskedMessage = masked };
    store.PortalAiChatMessages.Add(userMessage);
    var contextSummary = BuildPortalAiContext(store, customerId, session.ProjectId);
    var aiRun = new AiRun { CustomerId = customerId, ProjectId = session.ProjectId, RunType = "PortalSelfServiceChat", Provider = "LocalStub", Model = "portal-context-rule-based", PromptTemplateKey = "portal.self_service", PromptVersion = 1, InputSummary = "Portal user question with approved/published/customer-visible context only.", MaskedInputPreview = masked, OutputSummary = $"Answered from {contextSummary.VisibleDocumentCount} shared documents, {contextSummary.KnowledgeCount} knowledge articles and {contextSummary.TrainingCount} training sections.", Status = AiRunStatus.Completed, StartedAt = DateTimeOffset.UtcNow, CompletedAt = DateTimeOffset.UtcNow };
    store.AiRuns.Add(aiRun);
    var answer = $"Portal self-service answer: I found {contextSummary.KnowledgeCount} knowledge articles, {contextSummary.TrainingCount} training sections and {contextSummary.VisibleDocumentCount} customer-visible shared documents for this project. Internal comments and restricted data were excluded.";
    var aiMessage = new PortalAiChatMessage { CustomerId = customerId, ProjectId = session.ProjectId, SessionId = session.Id, SenderType = "AI", Message = answer, MaskedMessage = answer, AiRunId = aiRun.Id };
    store.PortalAiChatMessages.Add(aiMessage);
    audit.Write(customerId, session.ProjectId, "PORTAL_AI_CHAT_MESSAGE_CREATED", nameof(PortalAiChatSession), session.Id, new { userMessage, aiMessage, aiRunId = aiRun.Id });
    return Results.Ok(new { userMessage, aiMessage, aiRun });
});

app.MapPost("/api/portal/customers/{customerId:guid}/ai-chat/sessions/{sessionId:guid}/close", (Guid customerId, Guid sessionId, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var session = store.PortalAiChatSessions.SingleOrDefault(x => x.CustomerId == customerId && x.Id == sessionId);
    if (session is null) return Results.NotFound(new { error = "AI chat session not found." });
    session.Status = AiSelfServiceSessionStatus.Closed;
    session.ClosedAt = DateTimeOffset.UtcNow;
    audit.Write(customerId, session.ProjectId, "PORTAL_AI_CHAT_SESSION_CLOSED", nameof(PortalAiChatSession), session.Id, session);
    return Results.Ok(session);
});

app.MapGet("/api/portal/customers/{customerId:guid}/notifications", (Guid customerId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var user = PortalUserForActor(store, customerId, actor);
    return Results.Ok(store.PortalNotifications.Where(x => x.CustomerId == customerId && (user == null || x.PortalUserId == null || x.PortalUserId == user.Id)).OrderByDescending(x => x.CreatedAt));
});

app.MapPost("/api/portal/customers/{customerId:guid}/notifications/{notificationId:guid}/read", (Guid customerId, Guid notificationId, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var notification = store.PortalNotifications.SingleOrDefault(x => x.CustomerId == customerId && x.Id == notificationId);
    if (notification is null) return Results.NotFound(new { error = "Notification not found." });
    notification.Status = NotificationStatus.Read;
    notification.ReadAt = DateTimeOffset.UtcNow;
    audit.Write(customerId, notification.ProjectId, "PORTAL_NOTIFICATION_READ", nameof(PortalNotification), notification.Id, notification);
    return Results.Ok(notification);
});

app.MapGet("/api/portal/customers/{customerId:guid}/comments", (Guid customerId, IAppStore store, string? sourceEntityType, Guid? sourceEntityId, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var query = store.PortalComments.Where(x => x.CustomerId == customerId && x.Visibility == PortalVisibility.CustomerVisible);
    if (!string.IsNullOrWhiteSpace(sourceEntityType)) query = query.Where(x => x.SourceEntityType == sourceEntityType);
    if (sourceEntityId.HasValue) query = query.Where(x => x.SourceEntityId == sourceEntityId.Value);
    return Results.Ok(query.OrderBy(x => x.CreatedAt));
});

app.MapPost("/api/portal/customers/{customerId:guid}/comments", (Guid customerId, PortalCommentRequest request, IAppStore store, IAuditWriter audit, INotificationDeliveryProvider deliveryProvider, IDataMaskingService masking, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor) || !PortalUserCanAccessProject(store, customerId, request.ProjectId, actor)) return Results.Json(new { error = "Portal project access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var user = PortalUserForActor(store, customerId, actor);
    var comment = new PortalComment { CustomerId = customerId, ProjectId = request.ProjectId, SourceEntityType = request.SourceEntityType, SourceEntityId = request.SourceEntityId, PortalUserId = user?.Id, Message = MaskByClassification(store, customerId, request.ProjectId, "PortalComment", request.Message), Visibility = request.Visibility };
    store.PortalComments.Add(comment);
    CreatePortalNotification(store, customerId, request.ProjectId, null, NotificationType.CommentMention, "Portal comment added", comment.Message, request.SourceEntityType, request.SourceEntityId);
    AddTimeline(store, customerId, request.ProjectId, ActivityTimelineItemType.Comment, request.SourceEntityType, request.SourceEntityId, actor, "Comment added", comment.Message, request.Visibility);
    ExecuteWorkflowEvent(store, audit, deliveryProvider, masking, customerId, request.ProjectId, WorkflowTriggerType.CommentAdded, request.SourceEntityType, request.SourceEntityId, comment.Message, actor);
    audit.Write(customerId, request.ProjectId, "PORTAL_COMMENT_ADDED", nameof(PortalComment), comment.Id, comment);
    return Results.Created($"/api/portal/customers/{customerId}/comments/{comment.Id}", comment);
});

app.MapGet("/api/portal/customers/{customerId:guid}/attachments", (Guid customerId, IAppStore store, string? sourceEntityType, Guid? sourceEntityId, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var query = store.PortalAttachments.Where(x => x.CustomerId == customerId && x.Visibility == PortalVisibility.CustomerVisible);
    if (!string.IsNullOrWhiteSpace(sourceEntityType)) query = query.Where(x => x.SourceEntityType == sourceEntityType);
    if (sourceEntityId.HasValue) query = query.Where(x => x.SourceEntityId == sourceEntityId.Value);
    return Results.Ok(query.OrderByDescending(x => x.CreatedAt));
});

app.MapPost("/api/portal/customers/{customerId:guid}/attachments", (Guid customerId, PortalAttachmentRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor) || !PortalUserCanAccessProject(store, customerId, request.ProjectId, actor)) return Results.Json(new { error = "Portal project access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var user = PortalUserForActor(store, customerId, actor);
    var attachment = new PortalAttachment { CustomerId = customerId, ProjectId = request.ProjectId, SourceEntityType = request.SourceEntityType, SourceEntityId = request.SourceEntityId, UploadedByPortalUserId = user?.Id, FileName = request.FileName, ContentType = request.ContentType, StorageRef = request.StorageRef, Visibility = request.Visibility };
    store.PortalAttachments.Add(attachment);
    audit.Write(customerId, request.ProjectId, "PORTAL_ATTACHMENT_ADDED", nameof(PortalAttachment), attachment.Id, attachment);
    return Results.Created($"/api/portal/customers/{customerId}/attachments/{attachment.Id}", attachment);
});

app.MapGet("/api/portal/customers/{customerId:guid}/billing/summary", (Guid customerId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var user = PortalUserForActor(store, customerId, actor);
    if (!string.Equals(actor, "security.admin", StringComparison.OrdinalIgnoreCase) && user?.CanViewBilling != true) return Results.Json(new { error = "Billing permission denied." }, statusCode: StatusCodes.Status403Forbidden);
    foreach (var invoice in store.InvoiceDrafts.Where(x => x.CustomerId == customerId))
    {
        if (user != null && !store.PortalBillingSummaryViews.Any(x => x.CustomerId == customerId && x.PortalUserId == user.Id && x.InvoiceDraftId == invoice.Id))
        {
            store.PortalBillingSummaryViews.Add(new PortalBillingSummaryView { CustomerId = customerId, PortalUserId = user.Id, InvoiceDraftId = invoice.Id });
        }
    }
    return Results.Ok(new { subscriptions = store.Subscriptions.Where(x => x.CustomerId == customerId), billingDrafts = store.BillingDrafts.Where(x => x.CustomerId == customerId), invoiceDrafts = store.InvoiceDrafts.Where(x => x.CustomerId == customerId), paymentTracking = store.PaymentTrackingRecords.Where(x => x.CustomerId == customerId) });
});

app.MapGet("/api/portal/customers/{customerId:guid}/service-reports", (Guid customerId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var user = PortalUserForActor(store, customerId, actor);
    if (!string.Equals(actor, "security.admin", StringComparison.OrdinalIgnoreCase) && user?.CanViewReports != true) return Results.Json(new { error = "Service report permission denied." }, statusCode: StatusCodes.Status403Forbidden);
    return Results.Ok(store.CustomerServiceReports.Where(x => x.CustomerId == customerId).OrderByDescending(x => x.PeriodEnd));
});

app.MapGet("/api/portal/customers/{customerId:guid}/service-reports/{reportId:guid}", (Guid customerId, Guid reportId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var user = PortalUserForActor(store, customerId, actor);
    if (!string.Equals(actor, "security.admin", StringComparison.OrdinalIgnoreCase) && user?.CanViewReports != true) return Results.Json(new { error = "Service report permission denied." }, statusCode: StatusCodes.Status403Forbidden);
    var report = store.CustomerServiceReports.SingleOrDefault(x => x.CustomerId == customerId && x.Id == reportId);
    return report is null ? Results.NotFound(new { error = "Service report not found." }) : Results.Ok(report);
});

app.MapGet("/api/portal/customers/{customerId:guid}/projects/{projectId:guid}/reports/shared", (Guid customerId, Guid projectId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor) || !PortalUserCanAccessProject(store, customerId, projectId, actor)) return Results.Json(new { error = "Portal project access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var user = PortalUserForActor(store, customerId, actor);
    if (!string.Equals(actor, "security.admin", StringComparison.OrdinalIgnoreCase) && user?.CanViewReports != true) return Results.Json(new { error = "Report permission denied." }, statusCode: StatusCodes.Status403Forbidden);
    var sharedFileIds = store.PortalReportShares
        .Where(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Status is "Shared" or "Published" && (!x.ExpiresAt.HasValue || x.ExpiresAt > DateTimeOffset.UtcNow) && (user == null || x.SharedWithPortalUserId == null || x.SharedWithPortalUserId == user.Id))
        .Select(x => x.ReportExportFileId)
        .ToHashSet();
    var files = store.ReportExportFiles
        .Where(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Visibility != ReportVisibility.InternalOnly && sharedFileIds.Contains(x.Id))
        .OrderByDescending(x => x.CreatedAt);
    return Results.Ok(files);
});

app.MapPost("/api/portal/customers/{customerId:guid}/projects/{projectId:guid}/reports/{fileId:guid}/export-reference", (Guid customerId, Guid projectId, Guid fileId, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor) || !PortalUserCanAccessProject(store, customerId, projectId, actor)) return Results.Json(new { error = "Portal project access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var user = PortalUserForActor(store, customerId, actor);
    if (!string.Equals(actor, "security.admin", StringComparison.OrdinalIgnoreCase) && user?.CanViewReports != true) return Results.Json(new { error = "Report permission denied." }, statusCode: StatusCodes.Status403Forbidden);
    var shareAllowed = store.PortalReportShares.Any(x => x.CustomerId == customerId && x.ProjectId == projectId && x.ReportExportFileId == fileId && x.Status is "Shared" or "Published" && (!x.ExpiresAt.HasValue || x.ExpiresAt > DateTimeOffset.UtcNow) && (user == null || x.SharedWithPortalUserId == null || x.SharedWithPortalUserId == user.Id));
    var file = store.ReportExportFiles.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == fileId && x.Visibility != ReportVisibility.InternalOnly);
    if (file is null || !shareAllowed) return Results.NotFound(new { error = "Shared report not found." });
    audit.Write(customerId, projectId, "PORTAL_REPORT_EXPORT_REFERENCE_ACCESSED", nameof(ReportExportFile), file.Id, new { file.Id, file.ReportType, file.OutputFormat, file.StorageRef, actor });
    return Results.Ok(file);
});

app.MapGet("/api/customers/{customerId:guid}/collaboration/dashboard", (Guid customerId, IAppStore store, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    return Results.Ok(CollaborationDashboard(store, customerId));
});

app.MapGet("/api/customers/{customerId:guid}/notification-templates", (Guid customerId, IAppStore store) =>
    Results.Ok(store.NotificationTemplates.Where(x => x.CustomerId == customerId).OrderBy(x => x.TemplateKey).Select(x => new { template = x, versions = store.NotificationTemplateVersions.Where(v => v.CustomerId == customerId && v.TemplateId == x.Id).OrderByDescending(v => v.Version) })));

app.MapPost("/api/customers/{customerId:guid}/notification-templates", (Guid customerId, NotificationTemplateRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var template = new NotificationTemplate { CustomerId = customerId, ProjectId = request.ProjectId, TemplateKey = request.TemplateKey, Name = request.Name, NotificationType = request.NotificationType, Channel = request.Channel, RecipientType = request.RecipientType };
    var version = new NotificationTemplateVersion { CustomerId = customerId, TemplateId = template.Id, Version = 1, SubjectTemplate = request.SubjectTemplate, BodyTemplate = request.BodyTemplate, MaxClassification = request.MaxClassification, CreatedBy = actor };
    store.NotificationTemplates.Add(template);
    store.NotificationTemplateVersions.Add(version);
    audit.Write(customerId, request.ProjectId, "NOTIFICATION_TEMPLATE_CREATED", nameof(NotificationTemplate), template.Id, new { template, version });
    return Results.Created($"/api/customers/{customerId}/notification-templates/{template.Id}", new { template, version });
});

app.MapPost("/api/customers/{customerId:guid}/notification-templates/{templateId:guid}/versions", (Guid customerId, Guid templateId, NotificationTemplateVersionRequestDto request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var template = store.NotificationTemplates.SingleOrDefault(x => x.CustomerId == customerId && x.Id == templateId);
    if (template is null) return Results.NotFound(new { error = "Notification template not found." });
    var nextVersion = store.NotificationTemplateVersions.Where(x => x.CustomerId == customerId && x.TemplateId == templateId).Select(x => x.Version).DefaultIfEmpty(0).Max() + 1;
    foreach (var old in store.NotificationTemplateVersions.Where(x => x.CustomerId == customerId && x.TemplateId == templateId)) old.Active = false;
    var version = new NotificationTemplateVersion { CustomerId = customerId, TemplateId = templateId, Version = nextVersion, SubjectTemplate = request.SubjectTemplate, BodyTemplate = request.BodyTemplate, MaxClassification = request.MaxClassification, CreatedBy = actor };
    store.NotificationTemplateVersions.Add(version);
    audit.Write(customerId, template.ProjectId, "NOTIFICATION_TEMPLATE_VERSION_CREATED", nameof(NotificationTemplateVersion), version.Id, version);
    return Results.Created($"/api/customers/{customerId}/notification-templates/{templateId}/versions/{version.Id}", version);
});

app.MapGet("/api/customers/{customerId:guid}/notification-deliveries", (Guid customerId, IAppStore store) =>
    Results.Ok(store.NotificationDeliveryLogs.Where(x => x.CustomerId == customerId).OrderByDescending(x => x.CreatedAt)));

app.MapGet("/api/customers/{customerId:guid}/workflow-rules", (Guid customerId, IAppStore store) =>
    Results.Ok(store.WorkflowRules.Where(x => x.CustomerId == customerId).OrderBy(x => x.Priority)));

app.MapPost("/api/customers/{customerId:guid}/workflow-rules", (Guid customerId, WorkflowRuleRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var rule = new WorkflowRule { CustomerId = customerId, ProjectId = request.ProjectId, RuleKey = request.RuleKey, Name = request.Name, TriggerEvent = request.TriggerEvent, ConditionJson = request.ConditionJson, ActionJson = request.ActionJson, Priority = request.Priority };
    store.WorkflowRules.Add(rule);
    audit.Write(customerId, request.ProjectId, "WORKFLOW_RULE_CREATED", nameof(WorkflowRule), rule.Id, rule);
    return Results.Created($"/api/customers/{customerId}/workflow-rules/{rule.Id}", rule);
});

app.MapPost("/api/customers/{customerId:guid}/workflow-events", (Guid customerId, WorkflowEventRequest request, IAppStore store, IAuditWriter audit, INotificationDeliveryProvider deliveryProvider, IDataMaskingService masking, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    if (request.ProjectId.HasValue && FindProject(store, customerId, request.ProjectId.Value) is null) return Results.NotFound(new { error = "Project not found." });
    var runs = ExecuteWorkflowEvent(store, audit, deliveryProvider, masking, customerId, request.ProjectId, request.TriggerEvent, request.SourceEntityType, request.SourceEntityId, request.Message, actor);
    return Results.Ok(new { runs, actionLogs = store.WorkflowActionLogs.Where(x => runs.Select(r => r.Id).Contains(x.WorkflowRunId)) });
});

app.MapGet("/api/customers/{customerId:guid}/workflow-runs", (Guid customerId, IAppStore store) =>
    Results.Ok(store.WorkflowRuns.Where(x => x.CustomerId == customerId).OrderByDescending(x => x.StartedAt).Select(x => new { run = x, actions = store.WorkflowActionLogs.Where(a => a.CustomerId == customerId && a.WorkflowRunId == x.Id) })));

app.MapGet("/api/customers/{customerId:guid}/collaboration/tasks", (Guid customerId, IAppStore store) =>
    Results.Ok(store.CollaborationTasks.Where(x => x.CustomerId == customerId).OrderBy(x => x.DueAt ?? DateTimeOffset.MaxValue)));

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/collaboration/tasks", (Guid customerId, Guid projectId, CollaborationTaskRequest request, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var task = new CollaborationTask { CustomerId = customerId, ProjectId = projectId, TaskNo = store.NextNumber("TASK"), Title = request.Title, Description = request.Description, AssigneeUserId = request.AssigneeUserId, AssigneeType = request.AssigneeType, Priority = request.Priority, DueAt = request.DueAt, SourceEntityType = request.SourceEntityType, SourceEntityId = request.SourceEntityId };
    store.CollaborationTasks.Add(task);
    AddTimeline(store, customerId, projectId, ActivityTimelineItemType.Task, nameof(CollaborationTask), task.Id, actor, "Task created", task.Title, PortalVisibility.InternalOnly);
    audit.Write(customerId, projectId, "COLLABORATION_TASK_CREATED", nameof(CollaborationTask), task.Id, task);
    return Results.Created($"/api/customers/{customerId}/collaboration/tasks/{task.Id}", task);
});

app.MapPost("/api/customers/{customerId:guid}/collaboration/tasks/{taskId:guid}/complete", (Guid customerId, Guid taskId, IAppStore store, IAuditWriter audit, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var task = store.CollaborationTasks.SingleOrDefault(x => x.CustomerId == customerId && x.Id == taskId);
    if (task is null) return Results.NotFound(new { error = "Task not found." });
    task.Status = CollaborationTaskStatus.Completed;
    task.CompletedAt = DateTimeOffset.UtcNow;
    AddTimeline(store, customerId, task.ProjectId, ActivityTimelineItemType.Task, nameof(CollaborationTask), task.Id, actor, "Task completed", task.Title, PortalVisibility.InternalOnly);
    audit.Write(customerId, task.ProjectId, "COLLABORATION_TASK_COMPLETED", nameof(CollaborationTask), task.Id, task);
    return Results.Ok(task);
});

app.MapGet("/api/customers/{customerId:guid}/collaboration/reminders", (Guid customerId, IAppStore store) =>
    Results.Ok(store.ReminderSchedules.Where(x => x.CustomerId == customerId).OrderBy(x => x.RemindAt)));

app.MapPost("/api/customers/{customerId:guid}/collaboration/reminders/run-due", (Guid customerId, IAppStore store, IAuditWriter audit, INotificationDeliveryProvider deliveryProvider, IDataMaskingService masking, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    var due = RunDueReminders(store, audit, deliveryProvider, masking, customerId, DateTimeOffset.UtcNow, actor);
    return Results.Ok(due);
});

app.MapGet("/api/customers/{customerId:guid}/collaboration/escalations", (Guid customerId, IAppStore store) =>
    Results.Ok(store.EscalationEvents.Where(x => x.CustomerId == customerId).OrderByDescending(x => x.EscalatedAt)));

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/collaboration/escalations", (Guid customerId, Guid projectId, EscalationRequest request, IAppStore store, IAuditWriter audit, INotificationDeliveryProvider deliveryProvider, IDataMaskingService masking, HttpContext http) =>
{
    var actor = Actor(http);
    if (!HasTenantAccess(store, customerId, actor)) return Results.Json(new { error = "Tenant access denied." }, statusCode: StatusCodes.Status403Forbidden);
    if (FindProject(store, customerId, projectId) is null) return Results.NotFound(new { error = "Project not found." });
    var escalation = CreateEscalation(store, deliveryProvider, masking, customerId, projectId, request.SourceEntityType, request.SourceEntityId, request.Reason, request.EscalatedToUserId, actor);
    audit.Write(customerId, projectId, "ESCALATION_CREATED", nameof(EscalationEvent), escalation.Id, escalation);
    return Results.Created($"/api/customers/{customerId}/collaboration/escalations/{escalation.Id}", escalation);
});

app.MapGet("/api/customers/{customerId:guid}/collaboration/timeline", (Guid customerId, IAppStore store, Guid? projectId, PortalVisibility? visibility) =>
{
    var query = store.ActivityTimelineEntries.Where(x => x.CustomerId == customerId);
    if (projectId.HasValue) query = query.Where(x => x.ProjectId == projectId.Value);
    if (visibility.HasValue) query = query.Where(x => x.Visibility == visibility.Value);
    return Results.Ok(query.OrderByDescending(x => x.CreatedAt).Take(100));
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/ai-proposals", (Guid customerId, Guid projectId, IAppStore store) =>
    Results.Ok(store.AiProposals.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt)));

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/ai-proposals/{proposalId:guid}/accept", (Guid customerId, Guid projectId, Guid proposalId, ReviewAiProposalRequest request, IAppStore store, IAuditWriter audit) =>
{
    var proposal = store.AiProposals.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == proposalId);
    if (proposal is null)
    {
        return Results.NotFound(new { error = "AI proposal not found." });
    }

    if (proposal.Status != AiProposalStatus.PendingReview)
    {
        return Results.Conflict(new { error = "Only pending AI proposals can be accepted." });
    }

    object created = proposal.TaskType switch
    {
        AiTaskType.GenerateUrs => AcceptUrsProposal(store, audit, proposal, request),
        AiTaskType.GenerateBlueprint => AcceptBlueprintProposal(store, audit, proposal, request),
        AiTaskType.GenerateConfigSpec => AcceptConfigSpecProposal(store, audit, proposal, request),
        AiTaskType.ClassifyIssue => AcceptIssueClassification(store, audit, proposal, request),
        AiTaskType.AnalyzeRootCause => AcceptRootCauseProposal(store, audit, proposal, request),
        AiTaskType.GenerateFixProposal => AcceptFixProposal(store, audit, proposal, request),
        AiTaskType.GenerateChangeRequest => AcceptChangeRequestProposal(store, audit, proposal, request),
        AiTaskType.GenerateRegressionTestPlan => AcceptRegressionTestPlanProposal(store, audit, proposal, request),
        AiTaskType.GenerateReleaseDraft => AcceptReleaseDraftProposal(store, audit, proposal, request),
        AiTaskType.GenerateKnowledgeUpdate => AcceptKnowledgeUpdateProposal(store, audit, proposal, request),
        _ => throw new InvalidOperationException("Unsupported AI proposal task.")
    };

    proposal.Status = AiProposalStatus.Accepted;
    proposal.ReviewedBy = request.ReviewedBy;
    proposal.ReviewComment = request.Comment;
    proposal.ReviewedAt = DateTimeOffset.UtcNow;
    proposal.UpdatedAt = DateTimeOffset.UtcNow;
    audit.Write(customerId, projectId, "AI_PROPOSAL_ACCEPTED", nameof(AiProposal), proposal.Id, proposal);
    return Results.Ok(new { proposal, created });
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/ai-proposals/{proposalId:guid}/reject", (Guid customerId, Guid projectId, Guid proposalId, ReviewAiProposalRequest request, IAppStore store, IAuditWriter audit) =>
{
    var proposal = store.AiProposals.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == proposalId);
    if (proposal is null)
    {
        return Results.NotFound(new { error = "AI proposal not found." });
    }

    if (proposal.Status != AiProposalStatus.PendingReview)
    {
        return Results.Conflict(new { error = "Only pending AI proposals can be rejected." });
    }

    proposal.Status = AiProposalStatus.Rejected;
    proposal.ReviewedBy = request.ReviewedBy;
    proposal.ReviewComment = request.Comment;
    proposal.ReviewedAt = DateTimeOffset.UtcNow;
    proposal.UpdatedAt = DateTimeOffset.UtcNow;
    audit.Write(customerId, projectId, "AI_PROPOSAL_REJECTED", nameof(AiProposal), proposal.Id, proposal);
    return Results.Ok(proposal);
});

app.MapGet("/api/ai/prompt-templates", (IAppStore store) =>
{
    var items = store.AiPromptTemplates
        .OrderBy(x => x.TaskType)
        .Select(x => new
        {
            template = x,
            versions = store.AiPromptTemplateVersions.Where(v => v.TemplateId == x.Id).OrderByDescending(v => v.Version)
        });
    return Results.Ok(items);
});

app.MapPost("/api/ai/prompt-templates", (PromptTemplateRequest request, IAppStore store, IAuditWriter audit) =>
{
    if (store.AiPromptTemplates.Any(x => string.Equals(x.Key, request.Key, StringComparison.OrdinalIgnoreCase)))
    {
        return Results.Conflict(new { error = "Prompt template key already exists." });
    }

    var template = new AiPromptTemplate
    {
        Key = request.Key,
        Name = request.Name,
        TaskType = request.TaskType,
        Description = request.Description
    };
    store.AiPromptTemplates.Add(template);
    var version = new AiPromptTemplateVersion
    {
        TemplateId = template.Id,
        TemplateKey = template.Key,
        Version = 1,
        SystemPrompt = request.SystemPrompt,
        UserPromptTemplate = request.UserPromptTemplate,
        OutputJsonSchema = request.OutputJsonSchema,
        CreatedBy = request.CreatedBy ?? "system"
    };
    store.AiPromptTemplateVersions.Add(version);
    audit.Write(Guid.Empty, null, "AI_PROMPT_TEMPLATE_CREATED", nameof(AiPromptTemplate), template.Id, new { template, version });
    return Results.Created($"/api/ai/prompt-templates/{template.Id}", new { template, versions = new[] { version } });
});

app.MapPost("/api/ai/prompt-templates/{templateId:guid}/versions", (Guid templateId, PromptTemplateVersionRequest request, IAppStore store, IAuditWriter audit) =>
{
    var template = store.AiPromptTemplates.SingleOrDefault(x => x.Id == templateId);
    if (template is null)
    {
        return Results.NotFound(new { error = "Prompt template not found." });
    }

    foreach (var existing in store.AiPromptTemplateVersions.Where(x => x.TemplateId == template.Id))
    {
        existing.IsActive = false;
    }

    var nextVersion = store.AiPromptTemplateVersions.Where(x => x.TemplateId == template.Id).Select(x => x.Version).DefaultIfEmpty(0).Max() + 1;
    var version = new AiPromptTemplateVersion
    {
        TemplateId = template.Id,
        TemplateKey = template.Key,
        Version = nextVersion,
        SystemPrompt = request.SystemPrompt,
        UserPromptTemplate = request.UserPromptTemplate,
        OutputJsonSchema = request.OutputJsonSchema,
        CreatedBy = request.CreatedBy ?? "system"
    };
    store.AiPromptTemplateVersions.Add(version);
    audit.Write(Guid.Empty, null, "AI_PROMPT_VERSION_CREATED", nameof(AiPromptTemplateVersion), version.Id, version);
    return Results.Created($"/api/ai/prompt-templates/{templateId}/versions/{version.Id}", version);
});

app.MapPost("/api/customers/{customerId:guid}/connectors", (Guid customerId, ConnectorRequest request, IAppStore store, IAuditWriter audit) =>
{
    if (FindCustomer(store, customerId) is null)
    {
        return Results.NotFound(new { error = "Customer not found." });
    }

    if (string.IsNullOrWhiteSpace(request.SecretRef))
    {
        return Results.BadRequest(new { error = "secret_ref is required. Do not store tokens in the database." });
    }

    var connector = new CustomerConnector
    {
        CustomerId = customerId,
        ProjectId = request.ProjectId,
        EnvironmentId = request.EnvironmentId,
        PermissionPolicyId = request.PermissionPolicyId,
        ConnectorType = request.ConnectorType,
        Name = request.Name,
        BaseUrl = request.BaseUrl,
        SecretRef = request.SecretRef,
        ConfigJson = request.ConfigJson
    };
    store.CustomerConnectors.Add(connector);
    audit.Write(customerId, request.ProjectId, "CONNECTOR_REGISTERED", nameof(CustomerConnector), connector.Id, connector);
    return Results.Created($"/api/customers/{customerId}/connectors/{connector.Id}", connector);
});

app.MapGet("/api/customers/{customerId:guid}/connectors", (Guid customerId, IAppStore store) =>
    Results.Ok(store.CustomerConnectors.Where(x => x.CustomerId == customerId).OrderBy(x => x.ConnectorType)));

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/connectors", (Guid customerId, Guid projectId, IAppStore store) =>
    FindProject(store, customerId, projectId) is null
        ? Results.NotFound()
        : Results.Ok(store.CustomerConnectors.Where(x => x.CustomerId == customerId && (x.ProjectId == projectId || x.ProjectId == null)).OrderBy(x => x.Name)));

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/connector-policies", (Guid customerId, Guid projectId, ConnectorPolicyRequest request, IAppStore store, IAuditWriter audit) =>
{
    if (FindProject(store, customerId, projectId) is null)
    {
        return Results.NotFound(new { error = "Project not found." });
    }

    if (request.EnvironmentId.HasValue && FindEnvironment(store, customerId, projectId, request.EnvironmentId.Value) is null)
    {
        return Results.BadRequest(new { error = "Environment does not belong to this customer/project." });
    }

    var policy = new ConnectorPermissionPolicy
    {
        CustomerId = customerId,
        ProjectId = projectId,
        EnvironmentId = request.EnvironmentId,
        Name = request.Name,
        AllowSchemaRead = request.AllowSchemaRead,
        AllowConfigRead = request.AllowConfigRead,
        AllowLogRead = request.AllowLogRead,
        AllowSourceMetadataRead = request.AllowSourceMetadataRead,
        AllowHealthCheck = request.AllowHealthCheck,
        AllowTestApply = request.AllowTestApply,
        AllowProductionApplyWithApproval = request.AllowProductionApplyWithApproval,
        MaskingProfile = string.IsNullOrWhiteSpace(request.MaskingProfile) ? "Default" : request.MaskingProfile
    };
    store.ConnectorPermissionPolicies.Add(policy);
    audit.Write(customerId, projectId, "CONNECTOR_POLICY_CREATED", nameof(ConnectorPermissionPolicy), policy.Id, policy);
    return Results.Created($"/api/customers/{customerId}/projects/{projectId}/connector-policies/{policy.Id}", policy);
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/connector-policies", (Guid customerId, Guid projectId, IAppStore store) =>
    Results.Ok(store.ConnectorPermissionPolicies.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderBy(x => x.Name)));

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/connector-runs", (Guid customerId, Guid projectId, IAppStore store) =>
    Results.Ok(store.ConnectorRuns.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.StartedAt)));

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/environment-snapshots", (Guid customerId, Guid projectId, IAppStore store) =>
    Results.Ok(store.EnvironmentSnapshots.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CapturedAt)));

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/snapshot-diffs", (Guid customerId, Guid projectId, IAppStore store) =>
    Results.Ok(store.SnapshotDiffs.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt)));

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/controlled-test-apply/dry-run", async (Guid customerId, Guid projectId, TestApplyRequest request, IControlledApplyService applyService, CancellationToken cancellationToken) =>
{
    try
    {
        var run = await applyService.DryRunAsync(new ControlledApplyRequest(
            customerId,
            projectId,
            request.EnvironmentId,
            request.ConnectorId,
            request.SourceType,
            request.SourceId,
            request.RequestedBy ?? "ops.user"), cancellationToken);
        return Results.Ok(run);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/apply-runs/{applyRunId:guid}/execute", async (Guid customerId, Guid projectId, Guid applyRunId, ExecuteApplyRequest request, IControlledApplyService applyService, CancellationToken cancellationToken) =>
{
    try
    {
        var run = await applyService.ApplyAsync(customerId, projectId, applyRunId, request.RequestedBy ?? "ops.user", cancellationToken);
        return Results.Ok(run);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/apply-runs", (Guid customerId, Guid projectId, IAppStore store) =>
    Results.Ok(store.ApplyRuns.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt)));

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/apply-runs/{applyRunId:guid}", (Guid customerId, Guid projectId, Guid applyRunId, IAppStore store) =>
{
    var run = store.ApplyRuns.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == applyRunId);
    if (run is null)
    {
        return Results.NotFound(new { error = "Apply run not found." });
    }

    return Results.Ok(new
    {
        run,
        steps = store.ApplySteps.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && x.ApplyRunId == applyRunId).OrderBy(x => x.StepOrder),
        logs = store.ApplyLogs.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && x.ApplyRunId == applyRunId).OrderBy(x => x.CreatedAt),
        rollbackPlan = run.RollbackPlanId.HasValue ? store.RollbackPlans.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == run.RollbackPlanId.Value) : null,
        preSnapshot = run.PreSnapshotId.HasValue ? store.EnvironmentSnapshots.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == run.PreSnapshotId.Value) : null,
        postSnapshot = run.PostSnapshotId.HasValue ? store.EnvironmentSnapshots.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == run.PostSnapshotId.Value) : null,
        snapshotDiff = run.SnapshotDiffId.HasValue ? store.SnapshotDiffs.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == run.SnapshotDiffId.Value) : null,
        regression = run.RegressionTestRunId.HasValue ? store.RegressionTestRuns.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == run.RegressionTestRunId.Value) : null,
        readiness = run.ReleaseReadinessReportId.HasValue ? store.ReleaseReadinessReports.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == run.ReleaseReadinessReportId.Value) : null
    });
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/regression-test-runs", (Guid customerId, Guid projectId, IAppStore store) =>
    Results.Ok(store.RegressionTestRuns.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.StartedAt)));

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/release-readiness-reports", (Guid customerId, Guid projectId, IAppStore store) =>
    Results.Ok(store.ReleaseReadinessReports.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.GeneratedAt)));

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-releases/dashboard", (Guid customerId, Guid projectId, IAppStore store) =>
{
    var releases = store.ProductionReleasePackages.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).ToList();
    return Results.Ok(new
    {
        drafts = releases.Count(x => x.Status == ProductionReleaseStatus.Draft),
        pendingApproval = releases.Count(x => x.Status == ProductionReleaseStatus.PendingApproval),
        scheduled = releases.Count(x => x.Status == ProductionReleaseStatus.Scheduled),
        deploying = releases.Count(x => x.Status == ProductionReleaseStatus.Deploying),
        validationFailed = releases.Count(x => x.Status == ProductionReleaseStatus.ValidationFailed),
        rollbackRequested = releases.Count(x => x.Status == ProductionReleaseStatus.RollbackRequested),
        readyToClose = releases.Count(x => x.Status == ProductionReleaseStatus.ReadyToClose),
        highCritical = releases.Count(x => x.RiskLevel is RiskLevel.High or RiskLevel.Critical)
    });
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-release-packages", (Guid customerId, Guid projectId, IAppStore store) =>
    Results.Ok(store.ProductionReleasePackages.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt)));

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-release-packages", (Guid customerId, Guid projectId, CreateProductionReleasePackageRequest request, IAppStore store, IAuditWriter audit) =>
{
    var readiness = store.ReleaseReadinessReports.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == request.ReleaseReadinessReportId);
    if (readiness is null)
    {
        return Results.NotFound(new { error = "Release readiness report not found." });
    }
    if (readiness.Status != ReleaseReadinessStatus.Ready)
    {
        return Results.Conflict(new { error = "Cannot create production release package until ReleaseReadinessReport is Ready." });
    }
    var prod = request.ProductionEnvironmentId.HasValue
        ? FindEnvironment(store, customerId, projectId, request.ProductionEnvironmentId.Value)
        : store.Environments.FirstOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Kind == EnvironmentKind.Production);
    if (prod is null || prod.Kind != EnvironmentKind.Production)
    {
        return Results.BadRequest(new { error = "ProductionEnvironmentId must belong to a Production environment." });
    }
    var applyRun = store.ApplyRuns.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == readiness.ApplyRunId);
    var package = new ProductionReleasePackage
    {
        CustomerId = customerId,
        ProjectId = projectId,
        ProductionEnvironmentId = prod.Id,
        ReleaseReadinessReportId = readiness.Id,
        PackageNo = store.NextNumber("PRD"),
        Version = request.Version,
        Title = string.IsNullOrWhiteSpace(request.Title) ? $"Production release {request.Version}" : request.Title,
        Summary = readiness.Summary,
        RiskLevel = applyRun?.RiskLevel ?? RiskLevel.Medium,
        RollbackPlanValidated = applyRun?.RollbackPlanId is not null
    };
    store.ProductionReleasePackages.Add(package);
    store.TraceLinks.Add(NewTrace(customerId, projectId, nameof(ReleaseReadinessReport), readiness.Id, nameof(ProductionReleasePackage), package.Id, "ProductionPackageCreated"));
    audit.Write(customerId, projectId, "PRODUCTION_RELEASE_PACKAGE_CREATED", nameof(ProductionReleasePackage), package.Id, package);
    return Results.Created($"/api/customers/{customerId}/projects/{projectId}/production-release-packages/{package.Id}", package);
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-release-packages/{packageId:guid}", (Guid customerId, Guid projectId, Guid packageId, IAppStore store) =>
    FindProductionPackage(store, customerId, projectId, packageId) is { } package
        ? Results.Ok(ProductionReleaseDetail(store, customerId, projectId, package))
        : Results.NotFound(new { error = "Production release package not found." }));

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-release-packages/{packageId:guid}/generate-checklist", (Guid customerId, Guid projectId, Guid packageId, IAppStore store, IAuditWriter audit) =>
{
    var package = RequireProductionPackage(store, customerId, projectId, packageId);
    if (!store.ReleaseChecklistItems.Any(x => x.CustomerId == customerId && x.ProjectId == projectId && x.ProductionReleasePackageId == packageId))
    {
        foreach (var (title, required) in ProductionChecklist(package))
        {
            store.ReleaseChecklistItems.Add(new ReleaseChecklistItem { CustomerId = customerId, ProjectId = projectId, ProductionReleasePackageId = packageId, Title = title, Required = required });
        }
    }
    audit.Write(customerId, projectId, "PRODUCTION_RELEASE_CHECKLIST_GENERATED", nameof(ProductionReleasePackage), package.Id, package);
    return Results.Ok(ProductionReleaseDetail(store, customerId, projectId, package));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-release-packages/{packageId:guid}/checklist/{itemId:guid}/complete", (Guid customerId, Guid projectId, Guid packageId, Guid itemId, CompleteChecklistItemRequest request, IAppStore store, IAuditWriter audit) =>
{
    var item = store.ReleaseChecklistItems.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.ProductionReleasePackageId == packageId && x.Id == itemId);
    if (item is null) return Results.NotFound(new { error = "Checklist item not found." });
    item.Completed = true;
    item.EvidenceRef = request.EvidenceRef;
    item.CompletedAt = DateTimeOffset.UtcNow;
    audit.Write(customerId, projectId, "PRODUCTION_RELEASE_CHECKLIST_ITEM_COMPLETED", nameof(ReleaseChecklistItem), item.Id, item);
    return Results.Ok(item);
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-release-packages/{packageId:guid}/submit-approval", (Guid customerId, Guid projectId, Guid packageId, SubmitApprovalRequest request, IAppStore store, IAuditWriter audit) =>
{
    var package = RequireProductionPackage(store, customerId, projectId, packageId);
    var approval = new ApprovalRequest { CustomerId = customerId, ProjectId = projectId, EntityType = nameof(ProductionReleasePackage), EntityId = package.Id, TargetEnvironmentId = package.ProductionEnvironmentId, RequestedBy = request.RequestedBy };
    store.ApprovalRequests.Add(approval);
    var approvers = ProductionApprovers(package).ToArray();
    for (var i = 0; i < approvers.Length; i++)
    {
        store.ApprovalSteps.Add(new ApprovalStep { CustomerId = customerId, ApprovalRequestId = approval.Id, StepOrder = i + 1, ApproverUserId = approvers[i] });
    }
    package.ApprovalRequestId = approval.Id;
    package.Status = ProductionReleaseStatus.PendingApproval;
    audit.Write(customerId, projectId, "PRODUCTION_RELEASE_SUBMITTED_FOR_APPROVAL", nameof(ApprovalRequest), approval.Id, approval);
    return Results.Ok(ProductionReleaseDetail(store, customerId, projectId, package));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-release-packages/{packageId:guid}/approve", (Guid customerId, Guid projectId, Guid packageId, ApprovalActionRequest request, IAppStore store, IAuditWriter audit) =>
{
    var package = RequireProductionPackage(store, customerId, projectId, packageId);
    if (!package.ApprovalRequestId.HasValue) return Results.Conflict(new { error = "Approval has not been submitted." });
    var approval = store.ApprovalRequests.Single(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == package.ApprovalRequestId.Value);
    approval.Status = ApprovalStatus.Approved;
    approval.CompletedAt = DateTimeOffset.UtcNow;
    foreach (var step in store.ApprovalSteps.Where(x => x.CustomerId == customerId && x.ApprovalRequestId == approval.Id))
    {
        step.Status = ApprovalStatus.Approved;
        step.Comment = request.Comment;
        step.ActedAt = DateTimeOffset.UtcNow;
    }
    package.Status = ProductionReleaseStatus.Approved;
    audit.Write(customerId, projectId, "PRODUCTION_RELEASE_APPROVED", nameof(ProductionReleasePackage), package.Id, package);
    return Results.Ok(ProductionReleaseDetail(store, customerId, projectId, package));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-release-packages/{packageId:guid}/reject", (Guid customerId, Guid projectId, Guid packageId, ApprovalActionRequest request, IAppStore store, IAuditWriter audit) =>
{
    var package = RequireProductionPackage(store, customerId, projectId, packageId);
    if (package.ApprovalRequestId.HasValue)
    {
        var approval = store.ApprovalRequests.Single(x => x.CustomerId == customerId && x.ProjectId == projectId && x.Id == package.ApprovalRequestId.Value);
        approval.Status = ApprovalStatus.Rejected;
        approval.CompletedAt = DateTimeOffset.UtcNow;
        foreach (var step in store.ApprovalSteps.Where(x => x.CustomerId == customerId && x.ApprovalRequestId == approval.Id))
        {
            step.Status = ApprovalStatus.Rejected;
            step.Comment = request.Comment;
            step.ActedAt = DateTimeOffset.UtcNow;
        }
    }
    package.Status = ProductionReleaseStatus.Rejected;
    audit.Write(customerId, projectId, "PRODUCTION_RELEASE_REJECTED", nameof(ProductionReleasePackage), package.Id, package);
    return Results.Ok(ProductionReleaseDetail(store, customerId, projectId, package));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-release-packages/{packageId:guid}/release-window", (Guid customerId, Guid projectId, Guid packageId, ReleaseWindowRequest request, IAppStore store, IAuditWriter audit) =>
{
    var package = RequireProductionPackage(store, customerId, projectId, packageId);
    var window = package.ReleaseWindowId.HasValue ? store.ReleaseWindows.SingleOrDefault(x => x.Id == package.ReleaseWindowId.Value) : null;
    window ??= new ReleaseWindow { CustomerId = customerId, ProjectId = projectId, ProductionReleasePackageId = package.Id };
    window.StartsAt = request.StartsAt;
    window.EndsAt = request.EndsAt;
    window.Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? "Asia/Bangkok" : request.Timezone;
    window.Status = ReleaseWindowStatus.Scheduled;
    if (!store.ReleaseWindows.Contains(window)) store.ReleaseWindows.Add(window);
    package.ReleaseWindowId = window.Id;
    package.Status = ProductionReleaseStatus.Scheduled;
    audit.Write(customerId, projectId, "PRODUCTION_RELEASE_WINDOW_SCHEDULED", nameof(ReleaseWindow), window.Id, window);
    return Results.Ok(ProductionReleaseDetail(store, customerId, projectId, package));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-release-packages/{packageId:guid}/release-window/cancel", (Guid customerId, Guid projectId, Guid packageId, IAppStore store, IAuditWriter audit) =>
{
    var package = RequireProductionPackage(store, customerId, projectId, packageId);
    if (package.ReleaseWindowId.HasValue && store.ReleaseWindows.SingleOrDefault(x => x.Id == package.ReleaseWindowId.Value) is { } window)
    {
        window.Status = ReleaseWindowStatus.Cancelled;
        audit.Write(customerId, projectId, "PRODUCTION_RELEASE_WINDOW_CANCELLED", nameof(ReleaseWindow), window.Id, window);
    }
    return Results.Ok(ProductionReleaseDetail(store, customerId, projectId, package));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-release-packages/{packageId:guid}/deployment-plan/generate", (Guid customerId, Guid projectId, Guid packageId, IAppStore store, IAuditWriter audit) =>
{
    var package = RequireProductionPackage(store, customerId, projectId, packageId);
    var plan = new ProductionDeploymentPlan { CustomerId = customerId, ProjectId = projectId, ProductionReleasePackageId = package.Id, PlanNo = store.NextNumber("PDP") };
    store.ProductionDeploymentPlans.Add(plan);
    package.DeploymentPlanId = plan.Id;
    foreach (var step in ProductionDeploymentSteps(package, plan.Id))
    {
        store.ProductionDeploymentSteps.Add(step);
    }
    audit.Write(customerId, projectId, "PRODUCTION_DEPLOYMENT_PLAN_GENERATED", nameof(ProductionDeploymentPlan), plan.Id, plan);
    return Results.Ok(ProductionReleaseDetail(store, customerId, projectId, package));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-release-packages/{packageId:guid}/deployment-plan/steps/{stepId:guid}/confirm", (Guid customerId, Guid projectId, Guid packageId, Guid stepId, ManualConfirmationRequest request, IAppStore store, IAuditWriter audit) =>
{
    var step = store.ProductionDeploymentSteps.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.ProductionReleasePackageId == packageId && x.Id == stepId);
    if (step is null) return Results.NotFound(new { error = "Deployment step not found." });
    step.Confirmed = true;
    step.GuardResult = $"Confirmed by {request.ConfirmedBy ?? "release.operator"}";
    audit.Write(customerId, projectId, "PRODUCTION_DEPLOYMENT_STEP_CONFIRMED", nameof(ProductionDeploymentStep), step.Id, step);
    return Results.Ok(step);
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-release-packages/{packageId:guid}/deployment-plan/validate", (Guid customerId, Guid projectId, Guid packageId, IAppStore store, IAuditWriter audit) =>
{
    var package = RequireProductionPackage(store, customerId, projectId, packageId);
    var plan = RequireDeploymentPlan(store, customerId, projectId, package);
    var errors = ValidateProductionDeploymentPlan(store, package, plan);
    plan.Validated = errors.Count == 0;
    plan.ValidationErrors = string.Join("; ", errors);
    if (plan.Validated) package.RollbackPlanValidated = true;
    audit.Write(customerId, projectId, "PRODUCTION_DEPLOYMENT_PLAN_VALIDATED", nameof(ProductionDeploymentPlan), plan.Id, plan);
    return Results.Ok(ProductionReleaseDetail(store, customerId, projectId, package));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-release-packages/{packageId:guid}/pre-snapshot", (Guid customerId, Guid projectId, Guid packageId, IAppStore store, IAuditWriter audit) =>
{
    var package = RequireProductionPackage(store, customerId, projectId, packageId);
    var snapshot = NewProductionSnapshot(store, package, SnapshotStage.PreApply, "Production pre-deployment snapshot.");
    package.PreSnapshotId = snapshot.Id;
    audit.Write(customerId, projectId, "PRODUCTION_PRE_SNAPSHOT_CREATED", nameof(EnvironmentSnapshot), snapshot.Id, snapshot);
    return Results.Ok(ProductionReleaseDetail(store, customerId, projectId, package));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-release-packages/{packageId:guid}/deploy", (Guid customerId, Guid projectId, Guid packageId, IAppStore store, IAuditWriter audit) =>
{
    var package = RequireProductionPackage(store, customerId, projectId, packageId);
    var blockers = ProductionDeployBlockers(store, package);
    if (blockers.Count > 0) return Results.Conflict(new { error = "Production deploy blocked.", blockers });
    var plan = RequireDeploymentPlan(store, customerId, projectId, package);
    var run = new ProductionDeploymentRun { CustomerId = customerId, ProjectId = projectId, ProductionReleasePackageId = package.Id, ProductionEnvironmentId = package.ProductionEnvironmentId, DeploymentPlanId = plan.Id, PreSnapshotId = package.PreSnapshotId, RunNo = store.NextNumber("PDR"), Status = DeploymentRunStatus.Running };
    store.ProductionDeploymentRuns.Add(run);
    foreach (var step in store.ProductionDeploymentSteps.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && x.DeploymentPlanId == plan.Id).OrderBy(x => x.StepOrder))
    {
        store.DeploymentStepRuns.Add(new DeploymentStepRun { CustomerId = customerId, ProjectId = projectId, ProductionReleasePackageId = package.Id, DeploymentRunId = run.Id, DeploymentStepId = step.Id, StepOrder = step.StepOrder, Title = step.Title, ManualConfirmationRequired = step.ManualConfirmationRequired, ConfirmedBy = step.Confirmed ? "release.operator" : null, Status = DeploymentStepRunStatus.Succeeded, StartedAt = DateTimeOffset.UtcNow, CompletedAt = DateTimeOffset.UtcNow });
    }
    store.ProductionDeploymentLogs.Add(new ProductionDeploymentLog { CustomerId = customerId, ProjectId = projectId, ProductionReleasePackageId = package.Id, DeploymentRunId = run.Id, Level = "Info", Message = "MockProductionApplyConnector executed guarded production deployment. No raw secret was stored." });
    run.Status = DeploymentRunStatus.Succeeded;
    run.CompletedAt = DateTimeOffset.UtcNow;
    package.LatestDeploymentRunId = run.Id;
    package.Status = ProductionReleaseStatus.Deploying;
    audit.Write(customerId, projectId, "PRODUCTION_DEPLOYMENT_RUN_CREATED", nameof(ProductionDeploymentRun), run.Id, run);
    return Results.Ok(ProductionReleaseDetail(store, customerId, projectId, package));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-release-packages/{packageId:guid}/post-snapshot", (Guid customerId, Guid projectId, Guid packageId, IAppStore store, IAuditWriter audit) =>
{
    var package = RequireProductionPackage(store, customerId, projectId, packageId);
    if (!package.LatestDeploymentRunId.HasValue) return Results.Conflict(new { error = "Deployment run is required before post-snapshot." });
    var snapshot = NewProductionSnapshot(store, package, SnapshotStage.PostApply, "Production post-deployment snapshot.");
    package.PostSnapshotId = snapshot.Id;
    var run = store.ProductionDeploymentRuns.Single(x => x.Id == package.LatestDeploymentRunId.Value);
    run.PostSnapshotId = snapshot.Id;
    audit.Write(customerId, projectId, "PRODUCTION_POST_SNAPSHOT_CREATED", nameof(EnvironmentSnapshot), snapshot.Id, snapshot);
    return Results.Ok(ProductionReleaseDetail(store, customerId, projectId, package));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-release-packages/{packageId:guid}/snapshot-diff", (Guid customerId, Guid projectId, Guid packageId, IAppStore store, IAuditWriter audit) =>
{
    var package = RequireProductionPackage(store, customerId, projectId, packageId);
    if (!package.PreSnapshotId.HasValue || !package.PostSnapshotId.HasValue) return Results.Conflict(new { error = "Pre and post snapshots are required." });
    var diff = new SnapshotDiff { CustomerId = customerId, ProjectId = projectId, EnvironmentId = package.ProductionEnvironmentId, FromSnapshotId = package.PreSnapshotId.Value, ToSnapshotId = package.PostSnapshotId.Value, SnapshotKind = SnapshotKind.ApplyComposite, RiskLevel = package.RiskLevel, DiffSummary = "Production pre/post diff generated. Mock connector reports controlled package changes only.", DiffJson = """{"productionGuard":"passed","secretStored":false}""" };
    store.SnapshotDiffs.Add(diff);
    package.SnapshotDiffId = diff.Id;
    if (package.LatestDeploymentRunId.HasValue) store.ProductionDeploymentRuns.Single(x => x.Id == package.LatestDeploymentRunId.Value).SnapshotDiffId = diff.Id;
    audit.Write(customerId, projectId, "PRODUCTION_SNAPSHOT_DIFF_CREATED", nameof(SnapshotDiff), diff.Id, diff);
    return Results.Ok(ProductionReleaseDetail(store, customerId, projectId, package));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-release-packages/{packageId:guid}/validation-plan/generate", (Guid customerId, Guid projectId, Guid packageId, IAppStore store, IAuditWriter audit) =>
{
    var package = RequireProductionPackage(store, customerId, projectId, packageId);
    if (!store.PostReleaseValidationChecks.Any(x => x.CustomerId == customerId && x.ProjectId == projectId && x.ProductionReleasePackageId == packageId))
    {
        foreach (var title in new[] { "Production health check", "HRM smoke test", "Permission boundary validation", "Integration heartbeat" })
            store.PostReleaseValidationChecks.Add(new PostReleaseValidationCheck { CustomerId = customerId, ProjectId = projectId, ProductionReleasePackageId = package.Id, Title = title });
    }
    audit.Write(customerId, projectId, "POST_RELEASE_VALIDATION_PLAN_GENERATED", nameof(ProductionReleasePackage), package.Id, package);
    return Results.Ok(ProductionReleaseDetail(store, customerId, projectId, package));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-release-packages/{packageId:guid}/validation/{checkId:guid}", (Guid customerId, Guid projectId, Guid packageId, Guid checkId, ValidationCheckRequest request, IAppStore store, IAuditWriter audit) =>
{
    var check = store.PostReleaseValidationChecks.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.ProductionReleasePackageId == packageId && x.Id == checkId);
    if (check is null) return Results.NotFound(new { error = "Validation check not found." });
    check.Status = request.Status;
    check.Evidence = request.Evidence;
    check.CompletedAt = DateTimeOffset.UtcNow;
    var package = RequireProductionPackage(store, customerId, projectId, packageId);
    if (request.Status == PostReleaseValidationStatus.Failed)
    {
        package.Status = ProductionReleaseStatus.ValidationFailed;
        CreateRollbackDecisionIfMissing(store, package, "Validation failed", "Production validation failed; rollback approval is required before rollback execution.");
    }
    audit.Write(customerId, projectId, "POST_RELEASE_VALIDATION_CHECK_UPDATED", nameof(PostReleaseValidationCheck), check.Id, check);
    return Results.Ok(ProductionReleaseDetail(store, customerId, projectId, package));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-release-packages/{packageId:guid}/validation/complete", (Guid customerId, Guid projectId, Guid packageId, IAppStore store, IAuditWriter audit) =>
{
    var package = RequireProductionPackage(store, customerId, projectId, packageId);
    var checks = store.PostReleaseValidationChecks.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && x.ProductionReleasePackageId == packageId).ToList();
    if (checks.Count == 0) return Results.Conflict(new { error = "Validation plan has not been generated." });
    if (checks.Any(x => x.Status == PostReleaseValidationStatus.Failed))
    {
        package.Status = ProductionReleaseStatus.ValidationFailed;
        CreateRollbackDecisionIfMissing(store, package, "Validation failed", "Rollback decision request created.");
    }
    else
    {
        foreach (var check in checks.Where(x => x.Status == PostReleaseValidationStatus.Pending)) check.Status = PostReleaseValidationStatus.Passed;
        package.Status = ProductionReleaseStatus.ReadyToClose;
    }
    audit.Write(customerId, projectId, "POST_RELEASE_VALIDATION_COMPLETED", nameof(ProductionReleasePackage), package.Id, package);
    return Results.Ok(ProductionReleaseDetail(store, customerId, projectId, package));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-release-packages/{packageId:guid}/rollback/request", (Guid customerId, Guid projectId, Guid packageId, RollbackDecisionRequestDto request, IAppStore store, IAuditWriter audit) =>
{
    var package = RequireProductionPackage(store, customerId, projectId, packageId);
    var rollback = CreateRollbackDecisionIfMissing(store, package, request.Reason, request.Impact);
    package.Status = ProductionReleaseStatus.RollbackRequested;
    audit.Write(customerId, projectId, "ROLLBACK_DECISION_REQUESTED", nameof(RollbackDecisionRequest), rollback.Id, rollback);
    return Results.Ok(ProductionReleaseDetail(store, customerId, projectId, package));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-release-packages/{packageId:guid}/rollback/{rollbackId:guid}/approve", (Guid customerId, Guid projectId, Guid packageId, Guid rollbackId, ApprovalActionRequest request, IAppStore store, IAuditWriter audit) =>
{
    var rollback = store.RollbackDecisionRequests.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.ProductionReleasePackageId == packageId && x.Id == rollbackId);
    if (rollback is null) return Results.NotFound(new { error = "Rollback decision not found." });
    rollback.Status = RollbackDecisionStatus.Approved;
    rollback.ApprovedBy = "release.manager";
    audit.Write(customerId, projectId, "ROLLBACK_DECISION_APPROVED", nameof(RollbackDecisionRequest), rollback.Id, rollback);
    return Results.Ok(rollback);
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-release-packages/{packageId:guid}/rollback/{rollbackId:guid}/execute", (Guid customerId, Guid projectId, Guid packageId, Guid rollbackId, IAppStore store, IAuditWriter audit) =>
{
    var rollback = store.RollbackDecisionRequests.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.ProductionReleasePackageId == packageId && x.Id == rollbackId);
    if (rollback is null) return Results.NotFound(new { error = "Rollback decision not found." });
    if (rollback.Status != RollbackDecisionStatus.Approved) return Results.Conflict(new { error = "Rollback execution requires approved rollback decision." });
    rollback.Status = RollbackDecisionStatus.Executed;
    rollback.RollbackRunRef = store.NextNumber("RBK");
    audit.Write(customerId, projectId, "ROLLBACK_EXECUTED", nameof(RollbackDecisionRequest), rollback.Id, rollback);
    return Results.Ok(rollback);
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-release-packages/{packageId:guid}/communications/generate", (Guid customerId, Guid projectId, Guid packageId, IAppStore store, IAuditWriter audit) =>
{
    var package = RequireProductionPackage(store, customerId, projectId, packageId);
    if (!store.ReleaseCommunications.Any(x => x.CustomerId == customerId && x.ProjectId == projectId && x.ProductionReleasePackageId == packageId))
        foreach (ReleaseCommunicationAudience audience in Enum.GetValues<ReleaseCommunicationAudience>())
            store.ReleaseCommunications.Add(new ReleaseCommunication { CustomerId = customerId, ProjectId = projectId, ProductionReleasePackageId = package.Id, Audience = audience, Subject = $"{package.PackageNo} {package.Title}", Content = $"{audience} communication for {package.Version}." });
    audit.Write(customerId, projectId, "RELEASE_COMMUNICATIONS_GENERATED", nameof(ProductionReleasePackage), package.Id, package);
    return Results.Ok(ProductionReleaseDetail(store, customerId, projectId, package));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-release-packages/{packageId:guid}/communications/{communicationId:guid}/sent", (Guid customerId, Guid projectId, Guid packageId, Guid communicationId, IAppStore store, IAuditWriter audit) =>
{
    var communication = store.ReleaseCommunications.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.ProductionReleasePackageId == packageId && x.Id == communicationId);
    if (communication is null) return Results.NotFound(new { error = "Communication not found." });
    communication.Sent = true;
    communication.SentAt = DateTimeOffset.UtcNow;
    audit.Write(customerId, projectId, "RELEASE_COMMUNICATION_SENT", nameof(ReleaseCommunication), communication.Id, communication);
    return Results.Ok(communication);
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-release-packages/{packageId:guid}/post-release-tasks/generate", (Guid customerId, Guid projectId, Guid packageId, IAppStore store, IAuditWriter audit) =>
{
    var package = RequireProductionPackage(store, customerId, projectId, packageId);
    if (!store.PostReleaseTasks.Any(x => x.CustomerId == customerId && x.ProjectId == projectId && x.ProductionReleasePackageId == packageId))
        foreach (var (target, title) in new[] { ("Blueprint", "Update Blueprint"), ("ConfigSpec", "Update Config Spec"), ("Training", "Update Training"), ("KnowledgeBase", "Update Knowledge Base"), ("Issue", "Close Issue"), ("ChangeRequest", "Close Change Request") })
            store.PostReleaseTasks.Add(new PostReleaseTask { CustomerId = customerId, ProjectId = projectId, ProductionReleasePackageId = package.Id, Target = target, Title = title });
    audit.Write(customerId, projectId, "POST_RELEASE_TASKS_GENERATED", nameof(ProductionReleasePackage), package.Id, package);
    return Results.Ok(ProductionReleaseDetail(store, customerId, projectId, package));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-release-packages/{packageId:guid}/post-release-tasks/{taskId:guid}/complete", (Guid customerId, Guid projectId, Guid packageId, Guid taskId, IAppStore store, IAuditWriter audit) =>
{
    var task = store.PostReleaseTasks.SingleOrDefault(x => x.CustomerId == customerId && x.ProjectId == projectId && x.ProductionReleasePackageId == packageId && x.Id == taskId);
    if (task is null) return Results.NotFound(new { error = "Post-release task not found." });
    task.Completed = true;
    task.CompletedAt = DateTimeOffset.UtcNow;
    audit.Write(customerId, projectId, "POST_RELEASE_TASK_COMPLETED", nameof(PostReleaseTask), task.Id, task);
    return Results.Ok(task);
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-release-packages/{packageId:guid}/closure-report/generate", (Guid customerId, Guid projectId, Guid packageId, IAppStore store, IAuditWriter audit) =>
{
    var package = RequireProductionPackage(store, customerId, projectId, packageId);
    var report = new ReleaseClosureReport { CustomerId = customerId, ProjectId = projectId, ProductionReleasePackageId = package.Id, ReportNo = store.NextNumber("RCR"), DeploymentSummary = $"{store.ProductionDeploymentRuns.Count(x => x.ProductionReleasePackageId == package.Id)} deployment run(s).", ValidationSummary = $"{store.PostReleaseValidationChecks.Count(x => x.ProductionReleasePackageId == package.Id && x.Status == PostReleaseValidationStatus.Passed)} validation checks passed.", RollbackSummary = store.RollbackDecisionRequests.LastOrDefault(x => x.ProductionReleasePackageId == package.Id)?.Status.ToString() ?? "No rollback requested.", DocumentUpdateSummary = $"{store.PostReleaseTasks.Count(x => x.ProductionReleasePackageId == package.Id && x.Completed)} post-release tasks completed.", FinalRecommendation = "Close release when communications and post-release tasks are complete." };
    store.ReleaseClosureReports.Add(report);
    package.ClosureReportId = report.Id;
    audit.Write(customerId, projectId, "RELEASE_CLOSURE_REPORT_GENERATED", nameof(ReleaseClosureReport), report.Id, report);
    return Results.Ok(ProductionReleaseDetail(store, customerId, projectId, package));
});

app.MapPost("/api/customers/{customerId:guid}/projects/{projectId:guid}/production-release-packages/{packageId:guid}/close", (Guid customerId, Guid projectId, Guid packageId, IAppStore store, IAuditWriter audit) =>
{
    var package = RequireProductionPackage(store, customerId, projectId, packageId);
    var blockers = ProductionCloseBlockers(store, package);
    if (blockers.Count > 0) return Results.Conflict(new { error = "Release close blocked.", blockers });
    package.Status = ProductionReleaseStatus.Closed;
    package.ClosedAt = DateTimeOffset.UtcNow;
    audit.Write(customerId, projectId, "PRODUCTION_RELEASE_CLOSED", nameof(ProductionReleasePackage), package.Id, package);
    return Results.Ok(ProductionReleaseDetail(store, customerId, projectId, package));
});

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/traceability", (Guid customerId, Guid projectId, IAppStore store) =>
    Results.Ok(store.TraceLinks.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt)));

app.MapGet("/api/customers/{customerId:guid}/projects/{projectId:guid}/traceability/view", (Guid customerId, Guid projectId, IAppStore store) =>
{
    if (FindProject(store, customerId, projectId) is null)
    {
        return Results.NotFound(new { error = "Project not found." });
    }

    var requirements = store.Requirements.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).ToDictionary(x => x.Id);
    var ursDocuments = store.UrsDocuments.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).ToDictionary(x => x.Id);
    var blueprints = store.Blueprints.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).ToDictionary(x => x.Id);
    var configSpecs = store.ConfigSpecs.Where(x => x.CustomerId == customerId && x.ProjectId == projectId).ToDictionary(x => x.Id);

    var chains =
        from req in requirements.Values
        let reqUrs = store.TraceLinks.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && x.FromEntityType == "Requirement" && x.FromEntityId == req.Id && x.ToEntityType == "UrsDocument")
        from reqUr in reqUrs.DefaultIfEmpty()
        let urs = reqUr is null || !ursDocuments.TryGetValue(reqUr.ToEntityId, out var u) ? null : u
        let ursBlueprints = urs is null ? [] : store.TraceLinks.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && x.FromEntityType == "UrsDocument" && x.FromEntityId == urs.Id && x.ToEntityType == "Blueprint").ToArray()
        from ursBlueprint in ursBlueprints.DefaultIfEmpty()
        let blueprint = ursBlueprint is null || !blueprints.TryGetValue(ursBlueprint.ToEntityId, out var b) ? null : b
        let blueprintConfigs = blueprint is null ? [] : store.TraceLinks.Where(x => x.CustomerId == customerId && x.ProjectId == projectId && x.FromEntityType == "Blueprint" && x.FromEntityId == blueprint.Id && x.ToEntityType == "ConfigSpec").ToArray()
        from blueprintConfig in blueprintConfigs.DefaultIfEmpty()
        let config = blueprintConfig is null || !configSpecs.TryGetValue(blueprintConfig.ToEntityId, out var c) ? null : c
        select new
        {
            requirement = new { req.Id, req.RequirementNo, req.Title, req.Version, req.Status, req.IsLatest },
            urs = urs is null ? null : new { urs.Id, urs.UrsNo, urs.Title, urs.Version, urs.Status, urs.IsLatest },
            blueprint = blueprint is null ? null : new { blueprint.Id, blueprint.BlueprintNo, blueprint.Type, blueprint.Version, blueprint.Status, blueprint.IsLatest },
            configSpec = config is null ? null : new { config.Id, config.ConfigNo, config.ModuleName, config.RiskLevel, config.Version, config.Status, config.IsLatest }
        };

    return Results.Ok(chains);
});

app.MapGet("/api/customers/{customerId:guid}/audit-logs", (Guid customerId, IAppStore store, int page = 1, int pageSize = 50) =>
    Results.Ok(store.AuditLogs.Where(x => x.CustomerId == customerId).OrderByDescending(x => x.CreatedAt).ToPagedResult(page, pageSize)));

SeedDemoData(
    app.Services.GetRequiredService<IAppStore>(),
    app.Services.GetRequiredService<IAuditWriter>(),
    app.Services.GetRequiredService<INotificationDeliveryProvider>(),
    app.Services.GetRequiredService<IDataMaskingService>());

app.Run();

