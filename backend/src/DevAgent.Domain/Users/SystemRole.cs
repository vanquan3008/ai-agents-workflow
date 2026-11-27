namespace DevAgent.Domain.Users;

/// <summary>Platform-level role. Project permissions still come from <see cref="Projects.ProjectMember"/>.</summary>
public enum SystemRole
{
    User = 1,

    /// <summary>Platform administrator: full access to every project, creates projects and manages users.</summary>
    SuperAdmin = 2,
}
