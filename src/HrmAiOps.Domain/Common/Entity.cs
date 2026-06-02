namespace HrmAiOps.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public abstract class CustomerScopedEntity : Entity
{
    public Guid CustomerId { get; set; }
}

public abstract class ProjectScopedEntity : CustomerScopedEntity
{
    public Guid ProjectId { get; set; }
}
