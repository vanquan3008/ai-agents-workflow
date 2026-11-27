using DevAgent.Application.Common;
using DevAgent.Application.Features.Departments.Commands;
using DevAgent.Application.Features.Departments.Contracts;
using DevAgent.Application.Features.Departments.Queries;
using DevAgent.Application.Features.Departments.Shared;
using DevAgent.Application.Features.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace DevAgent.Application.Features.Departments;

internal static class DepartmentsModule
{
    public static IServiceCollection AddDepartmentsModule(this IServiceCollection services) => services
        .AddScoped<DepartmentAccess>()
        .AddScoped<DepartmentChangeRecorder>()
        .AddScoped<DepartmentDraftProjection>()
        .AddScoped<IQueryHandler<ListDepartmentsQuery, DepartmentDirectoryDto>, ListDepartmentsHandler>()
        .AddScoped<IQueryHandler<GetDepartmentEditorOptionsQuery, DepartmentEditorOptionsDto>, GetDepartmentEditorOptionsHandler>()
        .AddScoped<IQueryHandler<GetPublishedDepartmentQuery, PublishedDepartmentDto>, GetPublishedDepartmentHandler>()
        .AddScoped<IQueryHandler<GetDepartmentDraftQuery, DepartmentDraftDto>, GetDepartmentDraftHandler>()
        .AddScoped<IQueryHandler<ListDepartmentRevisionsQuery, IReadOnlyList<DepartmentRevisionDto>>, ListDepartmentRevisionsHandler>()
        .AddScoped<IQueryHandler<ListDepartmentTasksQuery, PagedResult<TaskSummaryDto>>, ListDepartmentTasksHandler>()
        .AddScoped<IQueryHandler<GetDepartmentTaskQuery, TaskDetailDto>, GetDepartmentTaskHandler>()
        .AddScoped<ICommandHandler<CreateDepartmentCommand, DepartmentEditResultDto>, CreateDepartmentHandler>()
        .AddScoped<ICommandHandler<SaveDepartmentDraftCommand, DepartmentDraftDto>, SaveDepartmentDraftHandler>()
        .AddScoped<ICommandHandler<PublishDepartmentCommand, DepartmentEditResultDto>, PublishDepartmentHandler>()
        .AddScoped<ICommandHandler<RestoreDepartmentRevisionCommand, DepartmentDraftDto>, RestoreDepartmentRevisionHandler>()
        .AddScoped<ICommandHandler<ArchiveDepartmentCommand, DepartmentEditResultDto>, ArchiveDepartmentHandler>();
}
