using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;

namespace DevAgent.Domain.Projects.Repositories;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(ProjectId id, CancellationToken cancellationToken);

    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken);

    void Add(Project project);
}
