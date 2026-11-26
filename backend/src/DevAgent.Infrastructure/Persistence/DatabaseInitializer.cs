using DevAgent.Domain.Agents;
using DevAgent.Domain.Common;
using DevAgent.Domain.Executions;
using DevAgent.Domain.Policies;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Repositories;
using DevAgent.Domain.Users;
using DevAgent.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevAgent.Infrastructure.Persistence;

/// <summary>Định danh cố định của dữ liệu mẫu môi trường phát triển (dùng cùng xác thực Development).</summary>
public static class DevelopmentSeedData
{
    public static readonly UserId OwnerUserId = new(Guid.Parse("0193a1c0-0000-7000-8000-000000000001"));
    public static readonly UserId ApproverUserId = new(Guid.Parse("0193a1c0-0000-7000-8000-000000000002"));
    public static readonly UserId RequesterUserId = new(Guid.Parse("0193a1c0-0000-7000-8000-000000000003"));

    /// <summary>Người dùng không thuộc project nào — dùng để kiểm thử từ chối truy cập.</summary>
    public static readonly UserId OutsiderUserId = new(Guid.Parse("0193a1c0-0000-7000-8000-000000000004"));

    public const string ProjectSlug = "pilot";
}

/// <summary>
/// Áp migration và seed dữ liệu mẫu khi khởi động, theo cấu hình <see cref="DatabaseOptions"/>.
/// Chạy trong StartAsync nên ứng dụng chỉ nhận request sau khi DB đã sẵn sàng.
/// </summary>
internal sealed partial class DatabaseInitializer(
    IServiceProvider services,
    IOptions<DatabaseOptions> options,
    IHostEnvironment environment,
    ILogger<DatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.ApplyMigrationsOnStartup && !options.Value.SeedDevelopmentData)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();

        if (options.Value.ApplyMigrationsOnStartup)
        {
            LogMigrating(logger);
            await scope.ServiceProvider.GetRequiredService<DevAgentDbContext>().Database.MigrateAsync(cancellationToken);
        }

        if (options.Value.SeedDevelopmentData && environment.IsDevelopment())
        {
            await ActivatorUtilities.CreateInstance<DevelopmentDataSeeder>(scope.ServiceProvider).SeedAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Đang áp migration cơ sở dữ liệu")]
    private static partial void LogMigrating(ILogger logger);
}

/// <summary>Seed user/project/repo/agent profile mẫu. Idempotent: bỏ qua nếu project mẫu đã tồn tại.</summary>
public sealed partial class DevelopmentDataSeeder(DevAgentDbContext context, TimeProvider clock, ILogger<DevelopmentDataSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (await context.Projects.AnyAsync(p => p.Slug == DevelopmentSeedData.ProjectSlug, cancellationToken))
        {
            return;
        }

        var now = clock.GetUtcNow();
        var owner = User.Create(DevelopmentSeedData.OwnerUserId, "Dev Owner", "owner@devagent.local", now);
        var approver = User.Create(DevelopmentSeedData.ApproverUserId, "Dev Approver", "approver@devagent.local", now);
        var requester = User.Create(DevelopmentSeedData.RequesterUserId, "Dev Requester", "requester@devagent.local", now);
        var outsider = User.Create(DevelopmentSeedData.OutsiderUserId, "Outsider", "outsider@devagent.local", now);
        context.Users.AddRange(owner, approver, requester, outsider);

        var project = Project.Create("Pilot", DevelopmentSeedData.ProjectSlug, owner.Id, now);
        project.AddOrUpdateMember(approver.Id, ProjectRole.Approver, now);
        project.AddOrUpdateMember(requester.Id, ProjectRole.Requester, now);
        context.Projects.Add(project);

        context.Repositories.Add(CodeRepository.Register(
            project.Id,
            installationId: 1,
            owner: "devagent-pilot",
            name: "sample-app",
            defaultBranch: "main",
            RepositoryPolicy.CreateDefault("dotnet build", "dotnet test"),
            now));

        context.AgentProfiles.AddRange(
            AgentProfile.Create("planner", AgentStep.Planner, "planner", ["repo.list_files", "repo.read_file", "repo.search"], 16_000),
            AgentProfile.Create("coding", AgentStep.Coding, "coding", ["repo.read_file", "repo.search", "repo.write_file", "repo.diff"], 32_000),
            AgentProfile.Create("testing", AgentStep.Testing, "testing", ["sandbox.build", "sandbox.test"], 4_000),
            AgentProfile.Create("reviewer", AgentStep.Reviewer, "reviewer", ["repo.diff", "repo.read_file"], 16_000));

        await context.SaveChangesAsync(cancellationToken);
        LogSeeded(logger, DevelopmentSeedData.ProjectSlug);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Đã seed dữ liệu phát triển cho project {Slug}")]
    private static partial void LogSeeded(ILogger logger, string slug);
}
