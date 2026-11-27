using DevAgent.Application.AgentRuntime;
using DevAgent.Application.Authorization;
using DevAgent.Application.Features.Admin;
using DevAgent.Application.Features.Approvals;
using DevAgent.Application.Features.Auth;
using DevAgent.Application.Features.Departments;
using DevAgent.Application.Features.Executions;
using DevAgent.Application.Features.Notifications;
using DevAgent.Application.Features.Projects;
using DevAgent.Application.Features.Tasks;
using DevAgent.Application.Features.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace DevAgent.Application;

/// <summary>
/// Composition of the application modules. Each module registers its own handlers in its <c>*Module</c> class;
/// there is no assembly scanning, so the module classes together are the catalogue of use cases.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Modules shared by the API and the worker.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ProjectAccess>();

        return services
            .AddProjectsModule()
            .AddTasksModule()
            .AddApprovalsModule()
            .AddExecutionsModule()
            .AddWorkflowsModule()
            .AddDepartmentsModule()
            .AddNotificationsModule()
            .AddUserAdminModule();
    }

    /// <summary>API only: sign-in, refresh and password change. Requires the host to provide <see cref="Abstractions.Security.ITokenService"/>.</summary>
    public static IServiceCollection AddAuthModule(this IServiceCollection services) => services.AddAuthHandlers();

    /// <summary>
    /// Worker only: the agent runtime and every job handler. Kept out of the API so it never depends on
    /// <see cref="AgentRuntime.Jobs.IWorkerIdentity"/> (Development validates the whole container on startup).
    /// </summary>
    public static IServiceCollection AddWorkerJobHandlers(this IServiceCollection services) => services
        .AddAgentRuntimeModule()
        .AddNotificationDelivery();
}
