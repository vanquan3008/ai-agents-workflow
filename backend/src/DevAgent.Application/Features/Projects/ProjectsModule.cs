using DevAgent.Application.Common;
using Microsoft.Extensions.DependencyInjection;

namespace DevAgent.Application.Features.Projects;

internal static class ProjectsModule
{
    public static IServiceCollection AddProjectsModule(this IServiceCollection services) => services
        .AddScoped<ICommandHandler<CreateProjectCommand, ProjectDto>, CreateProjectHandler>()
        .AddScoped<ICommandHandler<UpsertProjectMemberCommand, bool>, UpsertProjectMemberHandler>()
        .AddScoped<ICommandHandler<RemoveProjectMemberCommand, bool>, RemoveProjectMemberHandler>()
        .AddScoped<ICommandHandler<RegisterRepositoryCommand, RepositoryDto>, RegisterRepositoryHandler>()
        .AddScoped<IQueryHandler<GetProjectQuery, ProjectDto>, GetProjectHandler>()
        .AddScoped<IQueryHandler<ListMyProjectsQuery, IReadOnlyList<ProjectDto>>, ListMyProjectsHandler>()
        .AddScoped<IQueryHandler<ListRepositoriesQuery, IReadOnlyList<RepositoryDto>>, ListRepositoriesHandler>()
        .AddScoped<IQueryHandler<ListProjectMembersQuery, IReadOnlyList<ProjectMemberDto>>, ListProjectMembersHandler>();
}
