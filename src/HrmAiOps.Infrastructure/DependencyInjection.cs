using HrmAiOps.Application.Abstractions;
using HrmAiOps.Infrastructure.Ai;
using HrmAiOps.Infrastructure.Apply;
using HrmAiOps.Infrastructure.Audit;
using HrmAiOps.Infrastructure.DataMigration;
using HrmAiOps.Infrastructure.DevOps;
using HrmAiOps.Infrastructure.Integrations;
using HrmAiOps.Infrastructure.Notifications;
using HrmAiOps.Infrastructure.Observability;
using HrmAiOps.Infrastructure.Persistence;
using HrmAiOps.Infrastructure.Reporting;
using HrmAiOps.Infrastructure.Secrets;
using Microsoft.Extensions.DependencyInjection;

namespace HrmAiOps.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IAppStore, InMemoryAppStore>();
        services.AddSingleton<IAuditWriter, AuditWriter>();
        services.AddSingleton<IAiProvider, LocalAiProvider>();
        services.AddSingleton<IStructuredAiProvider, LocalStructuredAiProvider>();
        services.AddSingleton<IDataMaskingService, RegexDataMaskingService>();
        services.AddSingleton<INotificationDeliveryProvider, MockNotificationDeliveryProvider>();
        services.AddSingleton<IAiContextBuilder, AiContextBuilder>();
        services.AddSingleton<IStructuredOutputValidator, StructuredOutputValidator>();
        services.AddSingleton<IAiTaskExecutor, AiTaskExecutor>();
        services.AddSingleton<IAiRunRecorder, AiRunRecorder>();
        services.AddSingleton<IControlledApplyService, ControlledApplyService>();
        services.AddSingleton<ISecretResolver, NoOpSecretResolver>();
        services.AddSingleton<IBackgroundJobDispatcher, InlineBackgroundJobDispatcher>();
        services.AddSingleton<IReportingService, ReportingService>();
        services.AddSingleton<IIntegrationHubService, IntegrationHubService>();
        services.AddSingleton<IDevOpsAutomationService, DevOpsAutomationService>();
        services.AddSingleton<IObservabilityService, ObservabilityService>();
        services.AddSingleton<IDataMigrationService, DataMigrationService>();
        return services;
    }
}
