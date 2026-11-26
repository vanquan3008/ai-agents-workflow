using DevAgent.Domain.Common;

namespace DevAgent.Domain.Projects;

public sealed class ProjectMember
{
    private ProjectMember()
    {
    }

    internal ProjectMember(UserId userId, ProjectRole role, DateTimeOffset addedAt)
    {
        UserId = userId;
        Role = role;
        AddedAt = addedAt;
    }

    public UserId UserId { get; private set; }

    public ProjectRole Role { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }

    internal void ChangeRole(ProjectRole role) => Role = role;
}
