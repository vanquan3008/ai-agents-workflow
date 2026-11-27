using DevAgent.Domain.Common;
using DevAgent.Domain.Departments;
using DevAgent.Domain.Departments.Layout;
using DevAgent.Domain.Departments.Rules;
using DevAgent.Domain.Departments.Templates;
using Shouldly;

namespace DevAgent.Domain.Tests;

public sealed class ItDepartmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 8, 0, 0, TimeSpan.Zero);
    private static readonly ProjectId Project = ProjectId.New();
    private static readonly UserId Owner = UserId.New();
    private static readonly RepositoryId Repo = RepositoryId.New();

    private static DepartmentConfiguration Template(DepartmentTemplate template = DepartmentTemplate.SoftwareDevelopment) =>
        DepartmentTemplates.Build(template, null, [Repo]);

    private static DepartmentConfiguration WithRooms(DepartmentConfiguration source, Func<IReadOnlyList<DepartmentRoom>, IEnumerable<DepartmentRoom>> change) =>
        DepartmentConfiguration.Create(source.Name, source.Theme, source.RepositoryIds, change(source.Rooms));

    private static DepartmentRoom Move(DepartmentRoom room, int x, int y, int? width = null, int? height = null) =>
        DepartmentRoom.Create(room.Id, room.Label, room.Station, x, y, width ?? room.Width, height ?? room.Height, room.SortOrder, room.AssetPreset, room.Action);

    private static ItDepartment NewDepartment() => ItDepartment.Create(Project, "Phat-Trien", Template(), Owner, Now);

    [Theory]
    [InlineData(DepartmentTemplate.SoftwareDevelopment)]
    [InlineData(DepartmentTemplate.Testing)]
    [InlineData(DepartmentTemplate.PlatformOperations)]
    public void Every_template_is_publishable_as_is(DepartmentTemplate template)
    {
        var configuration = Template(template);

        configuration.Validate(DepartmentValidationLevel.Publish).ShouldBeEmpty();
        configuration.Rooms.Where(r => r.Station is not null).Select(r => r.Station).ShouldBe(Enum.GetValues<WorkflowStation>().Cast<WorkflowStation?>(), ignoreOrder: true);
    }

    [Fact]
    public void Create_normalizes_the_slug_and_starts_as_an_unpublished_draft()
    {
        var department = NewDepartment();

        department.Slug.ShouldBe("phat-trien");
        department.EditVersion.ShouldBe(1);
        department.IsPublished.ShouldBeFalse();
        department.Draft.ShouldNotBeNull();
        department.Name.ShouldBe("Phát triển phần mềm");
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad slug")]
    [InlineData("-leading")]
    [InlineData("double--dash")]
    public void Invalid_slugs_are_rejected(string slug)
    {
        Should.Throw<DomainException>(() => ItDepartment.Create(Project, slug, Template(), Owner, Now)).Code.ShouldBe("department.slug_invalid");
    }

    [Fact]
    public void Publish_appends_a_numbered_revision_moves_the_pointer_and_clears_the_draft()
    {
        var department = NewDepartment();

        var first = department.Publish(1, Owner, Now);

        first.Version.ShouldBe(1);
        first.ProjectId.ShouldBe(Project);
        first.DepartmentId.ShouldBe(department.Id);
        first.Rooms.Count.ShouldBe(8);
        first.Repositories.Select(r => r.RepositoryId).ShouldBe([Repo]);
        department.PublishedVersion.ShouldBe(1);
        department.Draft.ShouldBeNull();
        department.EditVersion.ShouldBe(2);

        department.SaveDraft(Template(DepartmentTemplate.Testing), 2, Now);
        department.Publish(3, Owner, Now).Version.ShouldBe(2);
    }

    [Fact]
    public void Draft_name_is_the_directory_label_only_until_the_first_publication()
    {
        var department = NewDepartment();
        var renamed = DepartmentConfiguration.Create("Đội nền tảng", DepartmentTheme.Slate, [Repo], Template().Rooms);

        department.SaveDraft(renamed, 1, Now);
        department.Name.ShouldBe("Đội nền tảng");
        department.Publish(2, Owner, Now);

        department.SaveDraft(DepartmentConfiguration.Create("Tên nháp", DepartmentTheme.Slate, [Repo], Template().Rooms), 3, Now);
        department.Name.ShouldBe("Đội nền tảng");
    }

    [Fact]
    public void Stale_edit_versions_are_rejected()
    {
        var department = NewDepartment();
        department.SaveDraft(Template(), 1, Now);

        Should.Throw<DomainException>(() => department.SaveDraft(Template(), 1, Now)).Code.ShouldBe("department.edit_conflict");
        Should.Throw<DomainException>(() => department.Publish(1, Owner, Now)).Code.ShouldBe("department.edit_conflict");
    }

    [Fact]
    public void Drafts_may_be_incomplete_but_publication_requires_every_station()
    {
        var department = NewDepartment();
        var withoutPublisher = WithRooms(Template(), rooms => rooms.Where(r => r.Station != WorkflowStation.Publishing));

        department.SaveDraft(withoutPublisher, 1, Now);

        withoutPublisher.Validate(DepartmentValidationLevel.Publish).Select(v => v.Code).ShouldContain("department.station_missing");
        Should.Throw<DomainException>(() => department.Publish(2, Owner, Now)).Code.ShouldBe("department.station_missing");
    }

    [Fact]
    public void Overlapping_rooms_block_publication_but_not_drafts()
    {
        var overlapping = WithRooms(Template(), rooms => rooms.Select(r => r.Station == WorkflowStation.Development ? Move(r, 4, 2) : r));

        overlapping.Validate(DepartmentValidationLevel.Draft).ShouldBeEmpty();
        overlapping.Validate(DepartmentValidationLevel.Publish).Select(v => v.Code).ShouldContain("department.room_overlap");
    }

    [Theory]
    [InlineData(-1, 0, 4, 4)]
    [InlineData(0, -1, 4, 4)]
    [InlineData(21, 0, 4, 4)]
    [InlineData(0, 13, 4, 4)]
    [InlineData(0, 0, 1, 4)]
    public void Rooms_must_fit_the_grid_at_every_level(int x, int y, int width, int height)
    {
        var configuration = WithRooms(Template(), rooms => rooms.Select(r => r.SortOrder == 7 ? Move(r, x, y, width, height) : r));

        configuration.Validate(DepartmentValidationLevel.Draft).Select(v => v.Code).ShouldContain("department.room_out_of_bounds");
    }

    [Fact]
    public void A_station_can_be_bound_to_only_one_room()
    {
        var configuration = WithRooms(Template(), rooms => rooms.Select(r =>
            r.SortOrder == 6 ? DepartmentRoom.Create(r.Id, r.Label, WorkflowStation.Planning, r.X, r.Y, r.Width, r.Height, r.SortOrder, r.AssetPreset, null) : r));

        configuration.Validate(DepartmentValidationLevel.Draft).Select(v => v.Code).ShouldContain("department.station_duplicate");
    }

    [Fact]
    public void Room_presets_actions_and_counts_are_allowlisted()
    {
        var unknownPreset = WithRooms(Template(), rooms => rooms.Select(r =>
            r.SortOrder == 6 ? DepartmentRoom.Create(r.Id, r.Label, null, r.X, r.Y, r.Width, r.Height, r.SortOrder, "<script>", r.Action) : r));
        var actionOnStation = WithRooms(Template(), rooms => rooms.Select(r =>
            r.SortOrder == 0 ? DepartmentRoom.Create(r.Id, r.Label, r.Station, r.X, r.Y, r.Width, r.Height, r.SortOrder, r.AssetPreset, RoomAction.OpenTasks) : r));
        var tooMany = WithRooms(Template(), rooms => rooms.Concat(Enumerable.Range(0, 5).Select(i =>
            DepartmentRoom.Create(DepartmentRoomId.New(), $"Phòng {i}", null, i * 4, 14, 2, 2, 20 + i, RoomAssetPresets.Lounge, null))));

        unknownPreset.Validate(DepartmentValidationLevel.Draft).Select(v => v.Code).ShouldContain("department.room_preset_invalid");
        actionOnStation.Validate(DepartmentValidationLevel.Draft).Select(v => v.Code).ShouldContain("department.room_action_on_station");
        tooMany.Validate(DepartmentValidationLevel.Draft).Select(v => v.Code).ShouldContain("department.too_many_rooms");
    }

    [Fact]
    public void Publication_requires_a_repository()
    {
        var configuration = DepartmentConfiguration.Create("Trống", DepartmentTheme.Indigo, [], Template().Rooms);

        configuration.Validate(DepartmentValidationLevel.Draft).ShouldBeEmpty();
        configuration.Validate(DepartmentValidationLevel.Publish).Select(v => v.Code).ShouldContain("department.repository_required");
    }

    [Fact]
    public void Restore_copies_a_revision_into_a_new_draft_without_touching_history()
    {
        var department = NewDepartment();
        var first = department.Publish(1, Owner, Now);
        var moved = WithRooms(Template(), rooms => rooms.Select(r => r.SortOrder == 7 ? Move(r, 17, 11, 6, 5) : r));
        department.SaveDraft(moved, 2, Now);
        department.Publish(3, Owner, Now);

        department.RestoreRevision(first, 4, Now);

        department.PublishedVersion.ShouldBe(2);
        department.Draft.ShouldNotBeNull();
        department.Draft.Rooms.Select(r => (r.Id, r.X, r.Y)).ShouldBe(first.Rooms.Select(r => (r.Id, r.X, r.Y)));
        department.Publish(5, Owner, Now).Version.ShouldBe(3);
    }

    [Fact]
    public void Restore_rejects_a_revision_of_another_department()
    {
        var department = NewDepartment();
        var other = ItDepartment.Create(Project, "khac", Template(), Owner, Now);
        var foreign = other.Publish(1, Owner, Now);

        Should.Throw<DomainException>(() => department.RestoreRevision(foreign, 1, Now)).Code.ShouldBe("department.revision_mismatch");
    }

    [Fact]
    public void Archived_departments_reject_further_changes()
    {
        var department = NewDepartment();
        department.Archive(1, Now);

        department.IsArchived.ShouldBeTrue();
        Should.Throw<DomainException>(() => department.SaveDraft(Template(), 2, Now)).Code.ShouldBe("department.archived");
    }

    [Fact]
    public void Publish_without_a_draft_is_rejected()
    {
        var department = NewDepartment();
        department.Publish(1, Owner, Now);

        Should.Throw<DomainException>(() => department.Publish(2, Owner, Now)).Code.ShouldBe("department.no_draft");
    }
}
