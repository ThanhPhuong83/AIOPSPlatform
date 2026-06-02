using System.Text.Json;
using HrmAiOps.Application.Abstractions;
using HrmAiOps.Domain.Core;

namespace HrmAiOps.Infrastructure.Audit;

public sealed class AuditWriter(IAppStore store) : IAuditWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AuditLog Write(Guid customerId, Guid? projectId, string action, string entityType, Guid entityId, object? after = null)
    {
        var audit = new AuditLog
        {
            CustomerId = customerId,
            ProjectId = projectId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            AfterJson = after is null ? null : JsonSerializer.Serialize(after, JsonOptions)
        };

        store.AuditLogs.Add(audit);
        return audit;
    }
}
