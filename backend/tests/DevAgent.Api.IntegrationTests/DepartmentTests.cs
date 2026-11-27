using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Departments.Contracts;
using DevAgent.Application.Features.Tasks;
using DevAgent.Domain.Common;
using DevAgent.Domain.Departments;
using DevAgent.Domain.Departments.Templates;
using DevAgent.Domain.Policies;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Repositories;
using DevAgent.Domain.Tasks;
using DevAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;

namespace DevAgent.Api.IntegrationTests;

/// <summary>IT department offices end to end: API authorization, draft/publish/restore lifecycle, task scoping and PostgreSQL constraints.</summary>
public sealed class DepartmentTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly Guid Owner = TestData.OwnerUserId.Value;
    private static readonly Guid Requester = TestData.RequesterUserId.Value;
    private static readonly Guid Outsider = TestData.OutsiderUserId.Value;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Owner_creates_customizes_publishes_and_restores_a_department()
    {
        var (projectId, repositoryId) = await ArrangeAsync();
        var owner = factory.ClientFor(Owner);

        var created = await CreateAsync(owner, projectId, "SOFTWARE_DEVELOPMENT", [repositoryId]);
        created.EditVersion.ShouldBe(1);
        created.PublishedVersion.ShouldBeNull();
        var second = await CreateAsync(owner, projectId, "TESTING", [repositoryId]);

        var draft = await GetDraftAsync(owner, projectId, created.Id);
        draft.HasDraft.ShouldBeTrue();
        draft.PublishIssues.ShouldBeEmpty();
        draft.Config.Rooms.Count.ShouldBe(8);

        // Rename the department and one room, then publish version 1.
        var renamed = draft.Config with
        {
            Name = "Phòng phát triển",
            Rooms = draft.Config.Rooms.Select(r => r.SortOrder == 0 ? r with { Label = "Bàn kế hoạch" } : r).ToArray(),
        };
        var saved = await SaveDraftAsync(owner, projectId, created.Id, 1, renamed);
        saved.EditVersion.ShouldBe(2);
        (await PublishAsync(owner, projectId, created.Id, 2)).PublishedVersion.ShouldBe(1);

        var published = await Read<PublishedDepartmentDto>(await owner.GetAsync(DepartmentUrl(projectId, created.Id), Ct));
        published.Version.ShouldBe(1);
        published.Name.ShouldBe("Phòng phát triển");
        published.Rooms[0].Label.ShouldBe("Bàn kế hoạch");
        published.Repositories.Select(r => r.Id).ShouldBe([repositoryId]);
        published.CanManage.ShouldBeTrue();

        // Without a draft the editor starts from the published revision.
        var fromPublished = await GetDraftAsync(owner, projectId, created.Id);
        fromPublished.HasDraft.ShouldBeFalse();
        fromPublished.EditVersion.ShouldBe(3);

        // Move a room, publish version 2, then restore version 1 as a new draft and publish it as version 3.
        var runbook = fromPublished.Config.Rooms.Single(r => r.SortOrder == 7);
        var moved = fromPublished.Config with
        {
            Rooms = fromPublished.Config.Rooms.Select(r => r.RoomId == runbook.RoomId ? r with { X = 17, Y = 11, Width = 6, Height = 5 } : r).ToArray(),
        };
        await SaveDraftAsync(owner, projectId, created.Id, 3, moved);
        (await PublishAsync(owner, projectId, created.Id, 4)).PublishedVersion.ShouldBe(2);

        var restored = await Read<DepartmentDraftDto>(await owner.PostAsJsonAsync(
            $"{DepartmentUrl(projectId, created.Id)}/revisions/1/restore", new { expectedEditVersion = 5 }, Ct));
        restored.HasDraft.ShouldBeTrue();
        restored.PublishedVersion.ShouldBe(2);
        restored.Config.Rooms.Single(r => r.RoomId == runbook.RoomId).X.ShouldBe(runbook.X);
        (await PublishAsync(owner, projectId, created.Id, 6)).PublishedVersion.ShouldBe(3);

        var revisions = await Read<List<DepartmentRevisionDto>>(await owner.GetAsync($"{DepartmentUrl(projectId, created.Id)}/revisions", Ct));
        revisions.Select(r => r.Version).ShouldBe([3, 2, 1]);
        revisions.Single(r => r.IsCurrent).Version.ShouldBe(3);
        revisions[0].PublishedByName.ShouldBe("Owner");

        var directory = await Read<DepartmentDirectoryDto>(await owner.GetAsync(DepartmentsUrl(projectId), Ct));
        directory.CanManage.ShouldBeTrue();
        directory.Items.Single(d => d.Id == created.Id).PublishedVersion.ShouldBe(3);
        var unpublished = directory.Items.Single(d => d.Id == second.Id);
        unpublished.PublishedVersion.ShouldBeNull();
        unpublished.HasDraft.ShouldBeTrue();
    }

    [Fact]
    public async Task Members_without_manage_permission_see_only_published_offices_and_cannot_edit()
    {
        var (projectId, repositoryId) = await ArrangeAsync();
        var owner = factory.ClientFor(Owner);
        var requester = factory.ClientFor(Requester);
        var outsider = factory.ClientFor(Outsider);

        var published = await CreateAsync(owner, projectId, "SOFTWARE_DEVELOPMENT", [repositoryId]);
        await PublishAsync(owner, projectId, published.Id, 1);
        var hidden = await CreateAsync(owner, projectId, "TESTING", [repositoryId]);

        var directory = await Read<DepartmentDirectoryDto>(await requester.GetAsync(DepartmentsUrl(projectId), Ct));
        directory.CanManage.ShouldBeFalse();
        directory.Items.ShouldContain(d => d.Id == published.Id);
        directory.Items.ShouldNotContain(d => d.Id == hidden.Id);

        (await Read<PublishedDepartmentDto>(await requester.GetAsync(DepartmentUrl(projectId, published.Id), Ct))).CanManage.ShouldBeFalse();
        (await requester.GetAsync(DepartmentUrl(projectId, hidden.Id), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await requester.GetAsync($"{DepartmentUrl(projectId, published.Id)}/draft", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await requester.PostAsJsonAsync($"{DepartmentUrl(projectId, published.Id)}/publish", new { expectedEditVersion = 2 }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await requester.PostAsJsonAsync(DepartmentsUrl(projectId), new { template = "TESTING", slug = UniqueSlug("x") }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await outsider.GetAsync(DepartmentsUrl(projectId), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await outsider.GetAsync(DepartmentUrl(projectId, published.Id), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Stale_edits_and_competing_publications_return_conflict()
    {
        var (projectId, repositoryId) = await ArrangeAsync();
        var owner = factory.ClientFor(Owner);
        var created = await CreateAsync(owner, projectId, "PLATFORM_OPERATIONS", [repositoryId]);
        var draft = await GetDraftAsync(owner, projectId, created.Id);

        await SaveDraftAsync(owner, projectId, created.Id, 1, draft.Config);
        var stale = await owner.PutAsJsonAsync($"{DepartmentUrl(projectId, created.Id)}/draft", new { expectedEditVersion = 1, config = draft.Config }, Ct);
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ProblemCode(stale)).ShouldBe("department.edit_conflict");

        // Two managers publish the same edit version at once: exactly one revision is created.
        var attempts = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            factory.ClientFor(Owner).PostAsJsonAsync($"{DepartmentUrl(projectId, created.Id)}/publish", new { expectedEditVersion = 2 }, Ct)));
        attempts.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        attempts.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);

        var revisions = await Read<List<DepartmentRevisionDto>>(await owner.GetAsync($"{DepartmentUrl(projectId, created.Id)}/revisions", Ct));
        revisions.Select(r => r.Version).ShouldBe([1]);
    }

    [Fact]
    public async Task Invalid_layouts_and_foreign_repositories_are_rejected_with_every_violation()
    {
        var (projectId, repositoryId) = await ArrangeAsync();
        var (_, foreignRepositoryId) = await AddProjectWithRepositoryAsync();
        var owner = factory.ClientFor(Owner);

        var foreignCreate = await owner.PostAsJsonAsync(DepartmentsUrl(projectId),
            new { template = "TESTING", slug = UniqueSlug("qa"), repositoryIds = new[] { foreignRepositoryId } }, Ct);
        foreignCreate.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ProblemCode(foreignCreate)).ShouldBe("department.repository_not_in_project");

        var created = await CreateAsync(owner, projectId, "TESTING", [repositoryId]);
        var draft = await GetDraftAsync(owner, projectId, created.Id);
        var invalid = draft.Config with
        {
            RepositoryIds = [repositoryId, foreignRepositoryId],
            Rooms = draft.Config.Rooms.Select(r => r.SortOrder == 6 ? r with { X = 20, AssetPreset = "unknown" } : r).ToArray(),
        };
        var rejected = await owner.PutAsJsonAsync($"{DepartmentUrl(projectId, created.Id)}/draft", new { expectedEditVersion = 1, config = invalid }, Ct);
        rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await rejected.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        errors.TryGetProperty("rooms[6]", out _).ShouldBeTrue();
        errors.TryGetProperty("rooms[6].assetPreset", out _).ShouldBeTrue();
        errors.TryGetProperty("repositoryIds", out _).ShouldBeTrue();

        // A draft without the publishing room saves, but cannot be published.
        var incomplete = draft.Config with { Rooms = draft.Config.Rooms.Where(r => r.Stage != "PUBLISHING").ToArray() };
        var saved = await SaveDraftAsync(owner, projectId, created.Id, 1, incomplete);
        saved.PublishIssues.Select(i => i.Code).ShouldContain("department.station_missing");
        var publish = await owner.PostAsJsonAsync($"{DepartmentUrl(projectId, created.Id)}/publish", new { expectedEditVersion = 2 }, Ct);
        publish.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ProblemCode(publish)).ShouldBe("department.station_missing");
    }

    [Fact]
    public async Task Department_tasks_are_limited_to_the_followed_repositories()
    {
        var (projectId, followedRepositoryId) = await ArrangeAsync();
        var otherRepositoryId = await AddRepositoryAsync(new ProjectId(projectId));
        var (foreignProjectId, foreignRepositoryId) = await AddProjectWithRepositoryAsync();
        var followedTask = await AddTaskAsync(new ProjectId(projectId), new RepositoryId(followedRepositoryId));
        var otherTask = await AddTaskAsync(new ProjectId(projectId), new RepositoryId(otherRepositoryId));
        var foreignTask = await AddTaskAsync(foreignProjectId, new RepositoryId(foreignRepositoryId));
        var owner = factory.ClientFor(Owner);
        var requester = factory.ClientFor(Requester);

        var created = await CreateAsync(owner, projectId, "SOFTWARE_DEVELOPMENT", [followedRepositoryId]);
        await PublishAsync(owner, projectId, created.Id, 1);

        var page = await Read<PagedResult<TaskSummaryDto>>(await requester.GetAsync($"{DepartmentUrl(projectId, created.Id)}/tasks?pageSize=100", Ct));
        page.Items.ShouldAllBe(t => t.RepositoryId == followedRepositoryId);
        page.Items.ShouldContain(t => t.Id == followedTask);
        page.TotalCount.ShouldBe(page.Items.Count);

        (await Read<TaskDetailDto>(await requester.GetAsync($"{DepartmentUrl(projectId, created.Id)}/tasks/{followedTask}", Ct))).Id.ShouldBe(followedTask);
        (await requester.GetAsync($"{DepartmentUrl(projectId, created.Id)}/tasks/{otherTask}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await owner.GetAsync($"{DepartmentUrl(projectId, created.Id)}/tasks/{foreignTask}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // A department id from this project cannot be read through another project's route.
        (await owner.GetAsync(DepartmentUrl(foreignProjectId.Value, created.Id), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Archived_departments_leave_the_directory_and_keep_their_history()
    {
        var (projectId, repositoryId) = await ArrangeAsync();
        var owner = factory.ClientFor(Owner);
        var created = await CreateAsync(owner, projectId, "TESTING", [repositoryId]);
        await PublishAsync(owner, projectId, created.Id, 1);

        (await owner.PostAsJsonAsync($"{DepartmentUrl(projectId, created.Id)}/archive", new { expectedEditVersion = 2 }, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await owner.GetAsync(DepartmentUrl(projectId, created.Id), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Read<DepartmentDirectoryDto>(await owner.GetAsync(DepartmentsUrl(projectId), Ct))).Items.ShouldNotContain(d => d.Id == created.Id);
        (await Read<DepartmentDirectoryDto>(await owner.GetAsync($"{DepartmentsUrl(projectId)}?includeArchived=true", Ct)))
            .Items.Single(d => d.Id == created.Id).IsArchived.ShouldBeTrue();
        (await Read<List<DepartmentRevisionDto>>(await owner.GetAsync($"{DepartmentUrl(projectId, created.Id)}/revisions", Ct))).Count.ShouldBe(1);

        var edit = await owner.PostAsJsonAsync($"{DepartmentUrl(projectId, created.Id)}/publish", new { expectedEditVersion = 3 }, Ct);
        edit.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ProblemCode(edit)).ShouldBe("department.archived");

        var reuse = await owner.PostAsJsonAsync(DepartmentsUrl(projectId), new { template = "TESTING", slug = created.Slug }, Ct);
        reuse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Database_rejects_rows_that_break_department_constraints()
    {
        var (projectId, repositoryId) = await ArrangeAsync();
        var (foreignProjectId, foreignRepositoryId) = await AddProjectWithRepositoryAsync();
        var project = new ProjectId(projectId);
        Guid departmentId;
        Guid otherDepartmentId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DevAgentDbContext>();
            var now = DateTimeOffset.UtcNow;
            var configuration = DepartmentTemplates.Build(DepartmentTemplate.SoftwareDevelopment, null, [new RepositoryId(repositoryId)]);
            var department = ItDepartment.Create(project, UniqueSlug("db"), configuration, TestData.OwnerUserId, now);
            var other = ItDepartment.Create(project, UniqueSlug("db"), configuration, TestData.OwnerUserId, now);
            db.Departments.AddRange(department, other);
            await db.SaveChangesAsync(Ct);

            db.DepartmentRevisions.Add(department.Publish(1, TestData.OwnerUserId, now));
            db.DepartmentRevisions.Add(other.Publish(1, TestData.OwnerUserId, now));
            await db.SaveChangesAsync(Ct);
            other.SaveDraft(configuration, other.EditVersion, now);
            db.DepartmentRevisions.Add(other.Publish(other.EditVersion, TestData.OwnerUserId, now));
            await db.SaveChangesAsync(Ct);
            departmentId = department.Id.Value;
            otherDepartmentId = other.Id.Value;
        }

        // Out of the 24 × 16 grid.
        (await SqlError(db => db.Database.ExecuteSqlAsync($"""
            INSERT INTO devagent.it_department_revision_rooms (project_id, department_id, version, room_id, label, stage, x, y, width, height, sort_order, asset_preset)
            VALUES ({projectId}, {departmentId}, 1, {Guid.NewGuid()}, 'Ngoài lưới', NULL, 20, 0, 8, 2, 50, 'lounge')
            """, Ct))).ShouldBe(PostgresErrorCodes.CheckViolation);

        // A second room bound to the same station.
        (await SqlError(db => db.Database.ExecuteSqlAsync($"""
            INSERT INTO devagent.it_department_revision_rooms (project_id, department_id, version, room_id, label, stage, x, y, width, height, sort_order, asset_preset)
            VALUES ({projectId}, {departmentId}, 1, {Guid.NewGuid()}, 'Trùng', 'Planning', 0, 14, 2, 2, 51, 'lounge')
            """, Ct))).ShouldBe(PostgresErrorCodes.UniqueViolation);

        // A repository link to another project's repository.
        (await SqlError(db => db.Database.ExecuteSqlAsync($"""
            INSERT INTO devagent.it_department_revision_repositories (project_id, department_id, version, repository_id)
            VALUES ({projectId}, {departmentId}, 1, {foreignRepositoryId})
            """, Ct))).ShouldBe(PostgresErrorCodes.ForeignKeyViolation);

        // A revision filed under another project.
        (await SqlError(db => db.Database.ExecuteSqlAsync($"""
            INSERT INTO devagent.it_department_revisions (project_id, department_id, version, name, theme, published_by, published_at)
            VALUES ({foreignProjectId.Value}, {departmentId}, 9, 'Sai project', 'Indigo', {Owner}, now())
            """, Ct))).ShouldBe(PostgresErrorCodes.ForeignKeyViolation);

        // A published pointer to a version that exists only for a different department.
        (await SqlError(db => db.Database.ExecuteSqlAsync(
            $"UPDATE devagent.it_departments SET published_version = 2 WHERE id = {departmentId}", Ct))).ShouldBe(PostgresErrorCodes.ForeignKeyViolation);

        // Duplicate slug within the project.
        (await SqlError(db => db.Database.ExecuteSqlAsync(
            $"UPDATE devagent.it_departments SET slug = (SELECT slug FROM devagent.it_departments WHERE id = {otherDepartmentId}) WHERE id = {departmentId}",
            Ct))).ShouldBe(PostgresErrorCodes.UniqueViolation);

        // A task whose repository belongs to another project. The task is created here so the UPDATE always hits a row.
        var taskId = await AddTaskAsync(project, new RepositoryId(repositoryId));
        (await SqlError(db => db.Database.ExecuteSqlAsync(
            $"UPDATE devagent.tasks SET repository_id = {foreignRepositoryId} WHERE id = {taskId}", Ct))).ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
    }

    private async Task<(Guid ProjectId, Guid RepositoryId)> ArrangeAsync()
    {
        Assert.SkipUnless(factory.DockerAvailable, "Cần Docker.");
        _ = factory.CreateClient();
        await factory.SeedAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevAgentDbContext>();
        var project = await db.Projects.AsNoTracking().SingleAsync(p => p.Slug == TestData.ProjectSlug, Ct);
        var repository = await db.Repositories.AsNoTracking().Where(r => r.ProjectId == project.Id).OrderBy(r => r.CreatedAt).FirstAsync(Ct);
        return (project.Id.Value, repository.Id.Value);
    }

    private async Task<Guid> AddRepositoryAsync(ProjectId projectId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevAgentDbContext>();
        var repository = CodeRepository.Register(projectId, 1, "devagent-pilot", $"svc-{Guid.NewGuid():N}"[..20], "main",
            RepositoryPolicy.CreateDefault("dotnet build", "dotnet test"), DateTimeOffset.UtcNow);
        db.Repositories.Add(repository);
        await db.SaveChangesAsync(Ct);
        return repository.Id.Value;
    }

    private async Task<(ProjectId ProjectId, Guid RepositoryId)> AddProjectWithRepositoryAsync()
    {
        ProjectId projectId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DevAgentDbContext>();
            var project = Project.Create("Dự án khác", UniqueSlug("other"), TestData.OwnerUserId, DateTimeOffset.UtcNow);
            db.Projects.Add(project);
            await db.SaveChangesAsync(Ct);
            projectId = project.Id;
        }

        return (projectId, await AddRepositoryAsync(projectId));
    }

    private async Task<Guid> AddTaskAsync(ProjectId projectId, RepositoryId repositoryId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevAgentDbContext>();
        var task = AgentTask.Create(projectId, repositoryId, "Lỗi bộ lọc", "Bộ lọc trạng thái trả sai dòng.", new TaskSource(TaskChannel.Api, null),
            new IdempotencyKey($"dept-{Guid.NewGuid():N}"), TestData.OwnerUserId, DateTimeOffset.UtcNow);
        db.Tasks.Add(task);
        await db.SaveChangesAsync(Ct);
        return task.Id.Value;
    }

    private async Task<string?> SqlError(Func<DevAgentDbContext, Task> statement)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevAgentDbContext>();
        var error = await Should.ThrowAsync<PostgresException>(() => statement(db));
        return error.SqlState;
    }

    private static async Task<DepartmentEditResultDto> CreateAsync(HttpClient client, Guid projectId, string template, Guid[] repositoryIds)
    {
        var response = await client.PostAsJsonAsync(DepartmentsUrl(projectId), new { template, slug = UniqueSlug("dept"), repositoryIds }, Ct);
        return await Read<DepartmentEditResultDto>(response, HttpStatusCode.Created);
    }

    private static async Task<DepartmentDraftDto> GetDraftAsync(HttpClient client, Guid projectId, Guid departmentId) =>
        await Read<DepartmentDraftDto>(await client.GetAsync($"{DepartmentUrl(projectId, departmentId)}/draft", Ct));

    private static async Task<DepartmentDraftDto> SaveDraftAsync(HttpClient client, Guid projectId, Guid departmentId, int editVersion, DepartmentConfigDto config) =>
        await Read<DepartmentDraftDto>(await client.PutAsJsonAsync(
            $"{DepartmentUrl(projectId, departmentId)}/draft", new { expectedEditVersion = editVersion, config }, Ct));

    private static async Task<DepartmentEditResultDto> PublishAsync(HttpClient client, Guid projectId, Guid departmentId, int editVersion) =>
        await Read<DepartmentEditResultDto>(await client.PostAsJsonAsync(
            $"{DepartmentUrl(projectId, departmentId)}/publish", new { expectedEditVersion = editVersion }, Ct));

    private static async Task<T> Read<T>(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    private static async Task<string?> ProblemCode(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();

    private static string DepartmentsUrl(Guid projectId) => $"/api/v1/projects/{projectId}/departments";

    private static string DepartmentUrl(Guid projectId, Guid departmentId) => $"{DepartmentsUrl(projectId)}/{departmentId}";

    private static string UniqueSlug(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 13)];
}
