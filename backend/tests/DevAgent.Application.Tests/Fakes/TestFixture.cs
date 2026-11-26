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
        Owner = User.Create("owner@test.local", "Owner", SystemRole.User, now);
        Requester = User.Create("requester@test.local", "Requester", SystemRole.User, now);
        Outsider = User.Create("outsider@test.local", "Outsider", SystemRole.User, now);
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

    public ProjectAccess Access => new(CurrentUser, Store, Store, Store, Store, Store, Clock);

    public void ActAs(User user) => CurrentUser.UserId = user.Id;

    public void ActAsNobody() => CurrentUser.UserId = null;
}

/// <summary>Model giả trả văn bản cố định, đếm số lần gọi và lưu request cuối để kiểm tra trần đầu ra/model.</summary>
internal sealed class ScriptedModelClient(params string[] responses) : IModelClient
{
    private int _index;

    public int Calls => _index;

    public ModelRequest? LastRequest { get; private set; }

    public ModelStopReason StopReason { get; init; } = ModelStopReason.Completed;

    /// <summary>Đơn giá trả về; null mô phỏng model chưa cấu hình giá.</summary>
    public Func<string, ModelPrice?> Price { get; init; } = _ => ModelPrice.Free;

    public decimal CostPerCall { get; init; } = 0.01m;

    public string ProviderName => "scripted";

    public string ResolveModel(string purpose) => "scripted-model";

    public ModelPrice? GetPrice(string model) => Price(model);

    public Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        var text = responses[Math.Min(_index, responses.Length - 1)];
        _index++;
        return Task.FromResult(new ModelResponse(text, StopReason, null, new ModelUsage(1000, 200, 0, CostPerCall), request.ModelOverride ?? "scripted-model"));
    }
}

internal static class Ids
{
    public static UserId Any() => UserId.New();
}
