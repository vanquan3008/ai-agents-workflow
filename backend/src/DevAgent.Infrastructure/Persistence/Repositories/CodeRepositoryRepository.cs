using DevAgent.Domain.Common;
using DevAgent.Domain.Repositories;
using DevAgent.Domain.Repositories.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DevAgent.Infrastructure.Persistence.Repositories;

internal sealed class CodeRepositoryRepository(DevAgentDbContext context) : ICodeRepositoryRepository
{
    public Task<CodeRepository?> GetByIdAsync(RepositoryId id, CancellationToken cancellationToken) =>
        context.Repositories.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(string owner, string name, CancellationToken cancellationToken) =>
        context.Repositories.AnyAsync(r => r.Owner == owner && r.Name == name, cancellationToken);

    public void Add(CodeRepository repository) => context.Repositories.Add(repository);
}
