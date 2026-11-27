using DevAgent.Domain.Common;

namespace DevAgent.Domain.Departments;

/// <summary>A repository followed by a published revision. Stored as a row so PostgreSQL can enforce project ownership.</summary>
public sealed class RevisionRepositoryLink
{
    private RevisionRepositoryLink()
    {
    }

    internal RevisionRepositoryLink(RepositoryId repositoryId) => RepositoryId = repositoryId;

    public RepositoryId RepositoryId { get; private set; }
}
