using DevAgent.Domain.Common;
using DevAgent.Domain.Repositories;

namespace DevAgent.Domain.Repositories.Repositories;

public interface ICodeRepositoryRepository
{
    Task<CodeRepository?> GetByIdAsync(RepositoryId id, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string owner, string name, CancellationToken cancellationToken);

    void Add(CodeRepository repository);
}
