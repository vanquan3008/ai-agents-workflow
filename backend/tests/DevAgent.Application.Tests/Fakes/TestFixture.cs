using DevAgent.Application.Abstractions.Ai;
using DevAgent.Application.Authorization;
using DevAgent.Application.Options;
using DevAgent.Domain.Common;
using DevAgent.Domain.Policies;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Repositories;
using DevAgent.Domain.Users;
using Microsoft.Extensions.Time.Testing;

namespace DevAgent.Application.Tests.Fakes;

/// <summary>Dữ liệu dựng sẵn: một project có Owner, Requester, và một người ngoài; một repo với chính sách mặc định.</summary>
internal sealed class TestFixture
{
    public TestFixture()
    {
        var now = Clock.GetUtcNow();
        Owner = User.Create("Owner", null, now);
        Requester = User.Create("Requester", null, now);
        Outsider = User.Create("Outsider", null, now);
        Store.Users.AddRange([Owner, Requester, Outsider]);

        Project = Project.Create("Pilot", "pilot", Owner.Id, now);
        Project.AddOrUpdateMember(Requester.Id, ProjectRole.Requester, now);
        Store.Projects.Add(Project);

        Repository = CodeRepository.Register(Project.Id, 1, "acme", "app", "main", RepositoryPolicy.CreateDefault("dotnet build", "dotnet test"), now);
        Store.Repositories.Add(Repository);

        CurrentUser.UserId = Requester.Id;
    }

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 26, 8, 0, 0, TimeSpan.Zero));

    public InMemoryStore Store { get; } = new();

    public FakeCurrentUser CurrentUser { get; } = new();

    public User Owner { get; }

    public User Requester { get; }

    public User Outsider { get; }

    public Project Project { get; }

    public CodeRepository Repository { get; }

    public AgentLimitsOptions Limits { get; } = new();

    public ProjectAccess Access => new(CurrentUser, Store, Store, Store, Clock);

    public void ActAs(User user) => CurrentUser.UserId = user.Id;

    public void ActAsNobody() => CurrentUser.UserId = null;
}

/// <summary>Model giả trả văn bản cố định, đếm số lần gọi.</summary>
internal sealed class ScriptedModelClient(params string[] responses) : IModelClient
{
    private int _index;

    public int Calls => _index;

    public ModelStopReason StopReason { get; init; } = ModelStopReason.Completed;

    public string ProviderName => "scripted";

    public string ResolveModel(string purpose) => "scripted-model";

    public Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        var text = responses[Math.Min(_index, responses.Length - 1)];
        _index++;
        return Task.FromResult(new ModelResponse(text, StopReason, null, new ModelUsage(1000, 200, 0, 0.01m), "scripted-model"));
    }
}

internal static class Ids
{
    public static UserId Any() => UserId.New();
}
