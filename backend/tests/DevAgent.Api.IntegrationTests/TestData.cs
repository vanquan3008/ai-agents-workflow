using DevAgent.Application.Abstractions.Security;
using DevAgent.Domain.Common;
using DevAgent.Domain.Policies;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Repositories;
using DevAgent.Domain.Users;
using DevAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DevAgent.Api.IntegrationTests;

/// <summary>
/// Test-only data (production seeds only the super admin). Fixed identifiers let tests reference it.
/// </summary>
public static class TestData
{
    public const string Password = "Test-password-123";
    public const string ProjectSlug = "pilot";
    public const string SuperAdminEmail = "root@devagent.test";
    public const string SuperAdminPassword = "Bootstrap-pass-123";

    public static readonly UserId OwnerUserId = new(Guid.Parse("0193a1c0-0000-7000-8000-000000000001"));
    public static readonly UserId ApproverUserId = new(Guid.Parse("0193a1c0-0000-7000-8000-000000000002"));
    public static readonly UserId RequesterUserId = new(Guid.Parse("0193a1c0-0000-7000-8000-000000000003"));
    public static readonly UserId OutsiderUserId = new(Guid.Parse("0193a1c0-0000-7000-8000-000000000004"));

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DevAgentDbContext>();
        if (await context.Projects.AnyAsync(p => p.Slug == ProjectSlug, cancellationToken))
        {
            return;
        }

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var now = DateTimeOffset.UtcNow;
        User NewUser(UserId id, string email, string name)
        {
            var user = User.Create(id, email, name, SystemRole.User, now);
            user.SetPassword(hasher.Hash(Password), mustChange: false, now);
            return user;
        }

        var owner = NewUser(OwnerUserId, "owner@devagent.test", "Owner");
        var approver = NewUser(ApproverUserId, "approver@devagent.test", "Approver");
        var requester = NewUser(RequesterUserId, "requester@devagent.test", "Requester");
        var outsider = NewUser(OutsiderUserId, "outsider@devagent.test", "Outsider");
        context.Users.AddRange(owner, approver, requester, outsider);

        var project = Project.Create("Pilot", ProjectSlug, owner.Id, now);
        project.AddOrUpdateMember(approver.Id, ProjectRole.Approver, now);
        project.AddOrUpdateMember(requester.Id, ProjectRole.Requester, now);
        context.Projects.Add(project);

        context.Repositories.Add(CodeRepository.Register(
            project.Id, 1, "devagent-pilot", "sample-app", "main", RepositoryPolicy.CreateDefault("dotnet build", "dotnet test"), now));

        await context.SaveChangesAsync(cancellationToken);
    }
}
