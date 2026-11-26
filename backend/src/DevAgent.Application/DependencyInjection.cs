using DevAgent.Application.Abstractions.Events;
using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Application.Events;
using DevAgent.Application.Features.Approvals;
using DevAgent.Application.Features.Executions;
using DevAgent.Application.Features.Projects;
using DevAgent.Application.Features.Tasks;
using DevAgent.Application.Orchestration;
using DevAgent.Application.Orchestration.Agents;
using DevAgent.Domain.Approvals;
using DevAgent.Domain.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace DevAgent.Application;

/// <summary>
/// Đăng ký module tường minh (checklist 1.1): mỗi module tự khai báo handler của mình.
/// Không quét assembly, để nhìn vào đây là biết hệ thống có những use case nào.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ProjectAccess>();

        return services
            .AddProjectsModule()
            .AddTasksModule()
            .AddApprovalsModule()
            .AddExecutionsModule();
    }

    /// <summary>
    /// Chỉ tiến trình Worker: Orchestrator, các agent và handler job. Tách riêng để API không phụ thuộc
    /// <see cref="IWorkerIdentity"/> (DI validate toàn bộ service ở Development).
    /// </summary>
    public static IServiceCollection AddAgentOrchestration(this IServiceCollection services) => services
        .AddScoped<IPlannerAgent, PlannerAgent>()
        .AddScoped<IAgentOrchestrator, AgentOrchestrator>()
        .AddScoped<IJobHandler, ProcessTaskJobHandler>();

    private static IServiceCollection AddProjectsModule(this IServiceCollection services) => services
        .AddScoped<ICommandHandler<CreateProjectCommand, ProjectDto>, CreateProjectHandler>()
        .AddScoped<ICommandHandler<AddProjectMemberCommand, bool>, AddProjectMemberHandler>()
        .AddScoped<ICommandHandler<RegisterRepositoryCommand, RepositoryDto>, RegisterRepositoryHandler>()
        .AddScoped<IQueryHandler<ListMyProjectsQuery, IReadOnlyList<ProjectDto>>, ListMyProjectsHandler>()
        .AddScoped<IQueryHandler<ListRepositoriesQuery, IReadOnlyList<RepositoryDto>>, ListRepositoriesHandler>()
        .AddScoped<IQueryHandler<ListProjectMembersQuery, IReadOnlyList<ProjectMemberDto>>, ListProjectMembersHandler>();

    private static IServiceCollection AddTasksModule(this IServiceCollection services) => services
        .AddScoped<ICommandHandler<CreateTaskCommand, TaskCreatedDto>, CreateTaskHandler>()
        .AddScoped<ICommandHandler<CancelTaskCommand, string>, CancelTaskHandler>()
        .AddScoped<ICommandHandler<ProvideClarificationCommand, string>, ProvideClarificationHandler>()
        .AddScoped<ICommandHandler<RetryTaskCommand, string>, RetryTaskHandler>()
        .AddScoped<IQueryHandler<ListTasksQuery, PagedResult<TaskSummaryDto>>, ListTasksHandler>()
        .AddScoped<IQueryHandler<GetTaskQuery, TaskDetailDto>, GetTaskHandler>()
        .AddScoped<IQueryHandler<GetTaskTimelineQuery, IReadOnlyList<TimelineEntryDto>>, GetTaskTimelineHandler>()
        .AddScoped<IQueryHandler<AuthorizeTaskSubscriptionQuery, Guid>, AuthorizeTaskSubscriptionHandler>()
        .AddScoped<IDomainEventHandler<TaskStatusChanged>, TaskStatusChangedHandler>()
        .AddScoped<IDomainEventHandler<TaskPlanCreated>, TaskPlanCreatedHandler>();

    private static IServiceCollection AddApprovalsModule(this IServiceCollection services) => services
        .AddScoped<ICommandHandler<DecideApprovalCommand, ApprovalDto>, DecideApprovalHandler>()
        .AddScoped<IQueryHandler<ListApprovalsQuery, PagedResult<ApprovalDto>>, ListApprovalsHandler>()
        .AddScoped<IQueryHandler<GetApprovalQuery, ApprovalDto>, GetApprovalHandler>()
        .AddScoped<IDomainEventHandler<ApprovalDecided>, ApprovalDecidedHandler>();

    private static IServiceCollection AddExecutionsModule(this IServiceCollection services) => services
        .AddScoped<IQueryHandler<ListTaskExecutionsQuery, IReadOnlyList<ExecutionSummaryDto>>, ListTaskExecutionsHandler>()
        .AddScoped<IQueryHandler<GetExecutionQuery, ExecutionDetailDto>, GetExecutionHandler>();
}
