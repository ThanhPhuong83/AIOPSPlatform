using HrmAiOps.Application.Abstractions;

namespace HrmAiOps.Infrastructure.Secrets;

public sealed class NoOpSecretResolver : ISecretResolver
{
    public Task<string> ResolveAsync(string secretRef, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException($"Secret resolution is intentionally external. Configure a vault provider for '{secretRef}'.");
    }
}

public sealed class InlineBackgroundJobDispatcher : IBackgroundJobDispatcher
{
    public Task EnqueueAsync(string jobType, object payload, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
