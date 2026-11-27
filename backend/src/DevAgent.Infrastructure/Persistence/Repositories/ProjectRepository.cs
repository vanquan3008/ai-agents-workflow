using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Projects.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DevAgent.Infrastructure.Persistence.Repositories;

internal sealed class ProjectRepository(DevAgentDbContext context) : IProjectRepository
{
    public Task<Project?> GetByIdAsync(ProjectId id, CancellationToken cancellationToken) =>
        context.Projects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        context.Projects.AnyAsync(p => p.Slug == slug, cancellationToken);

    public void Add(Project project) => context.Projects.Add(project);
}
