using DevAgent.Domain.Common;
using DevAgent.Domain.Departments.Layout;

namespace DevAgent.Domain.Departments.Rules;

/// <summary>
/// The single definition of a valid office layout. The aggregate enforces it on every change, the application returns
/// every violation at once so the editor can highlight them, and PostgreSQL repeats the per-row limits.
/// </summary>
public static class DepartmentConfigurationRules
{
    public static IReadOnlyList<DepartmentRuleViolation> Validate(DepartmentConfiguration configuration, DepartmentValidationLevel level)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var violations = new List<DepartmentRuleViolation>();
        ValidateHeader(configuration, violations);
        ValidateRepositories(configuration.RepositoryIds, level, violations);
        ValidateRooms(configuration.Rooms, level, violations);
        return violations;
    }

    private static void ValidateHeader(DepartmentConfiguration configuration, List<DepartmentRuleViolation> violations)
    {
        if (configuration.Name.Length == 0)
        {
            violations.Add(new("department.name_required", "name", "Tên phòng ban là bắt buộc."));
        }
        else if (configuration.Name.Length > OfficeGrid.NameMaxLength)
        {
            violations.Add(new("department.name_too_long", "name", $"Tên phòng ban tối đa {OfficeGrid.NameMaxLength} ký tự."));
        }

        if (!Enum.IsDefined(configuration.Theme))
        {
            violations.Add(new("department.theme_invalid", "theme", "Giao diện không thuộc danh sách được hỗ trợ."));
        }
    }

    private static void ValidateRepositories(IReadOnlyList<RepositoryId> repositoryIds, DepartmentValidationLevel level, List<DepartmentRuleViolation> violations)
    {
        if (repositoryIds.Count > OfficeGrid.MaxRepositories)
        {
            violations.Add(new("department.too_many_repositories", "repositoryIds", $"Một phòng ban theo dõi tối đa {OfficeGrid.MaxRepositories} repository."));
        }

        if (repositoryIds.Any(id => id.Value == Guid.Empty))
        {
            violations.Add(new("department.repository_invalid", "repositoryIds", "Repository không hợp lệ."));
        }

        if (repositoryIds.Distinct().Count() != repositoryIds.Count)
        {
            violations.Add(new("department.repository_duplicate", "repositoryIds", "Mỗi repository chỉ được chọn một lần."));
        }

        if (level == DepartmentValidationLevel.Publish && repositoryIds.Count == 0)
        {
            violations.Add(new("department.repository_required", "repositoryIds", "Chọn ít nhất một repository trước khi xuất bản."));
        }
    }

    private static void ValidateRooms(IReadOnlyList<DepartmentRoom> rooms, DepartmentValidationLevel level, List<DepartmentRuleViolation> violations)
    {
        if (rooms.Count > OfficeGrid.MaxRooms)
        {
            violations.Add(new("department.too_many_rooms", "rooms", $"Một phòng ban có tối đa {OfficeGrid.MaxRooms} phòng."));
        }

        for (var i = 0; i < rooms.Count; i++)
        {
            ValidateRoom(rooms[i], $"rooms[{i}]", violations);
        }

        AddIfDuplicated(rooms, r => r.Id, "department.room_id_duplicate", "Mã phòng bị trùng.", violations);
        AddIfDuplicated(rooms, r => r.SortOrder, "department.sort_order_duplicate", "Thứ tự phòng bị trùng.", violations);
        AddIfDuplicated(
            rooms.Where(r => r.Station is not null).ToArray(),
            r => r.Station,
            "department.station_duplicate",
            "Mỗi bước quy trình chỉ gắn với một phòng.",
            violations);

        if (level != DepartmentValidationLevel.Publish)
        {
            return;
        }

        if (rooms.Count < OfficeGrid.MinRooms)
        {
            violations.Add(new("department.too_few_rooms", "rooms", $"Phòng ban cần ít nhất {OfficeGrid.MinRooms} phòng."));
        }

        foreach (var station in Enum.GetValues<WorkflowStation>().Where(s => rooms.All(r => r.Station != s)))
        {
            violations.Add(new("department.station_missing", "rooms", $"Chưa có phòng cho bước {station}."));
        }

        // At most twelve rooms, so the pairwise check stays trivial.
        for (var i = 0; i < rooms.Count; i++)
        {
            for (var j = i + 1; j < rooms.Count; j++)
            {
                if (rooms[i].Overlaps(rooms[j]))
                {
                    violations.Add(new(
                        "department.room_overlap",
                        $"rooms[{j}]",
                        $"Phòng \"{rooms[j].Label}\" chồng lên phòng \"{rooms[i].Label}\"."));
                }
            }
        }
    }

    private static void ValidateRoom(DepartmentRoom room, string field, List<DepartmentRuleViolation> violations)
    {
        if (room.Id.Value == Guid.Empty)
        {
            violations.Add(new("department.room_id_required", $"{field}.roomId", "Thiếu mã phòng."));
        }

        if (room.Label.Length == 0 || room.Label.Length > OfficeGrid.RoomLabelMaxLength)
        {
            violations.Add(new("department.room_label_invalid", $"{field}.label", $"Tên phòng cần từ 1 đến {OfficeGrid.RoomLabelMaxLength} ký tự."));
        }

        if (room.Station is { } station && !Enum.IsDefined(station))
        {
            violations.Add(new("department.room_station_invalid", $"{field}.stage", "Bước quy trình không hợp lệ."));
        }

        if (room.Action is { } action && !Enum.IsDefined(action))
        {
            violations.Add(new("department.room_action_invalid", $"{field}.action", "Hành động không thuộc danh sách được phép."));
        }

        if (room.Station is not null && room.Action is not null)
        {
            violations.Add(new("department.room_action_on_station", $"{field}.action", "Phòng gắn bước quy trình không có hành động riêng."));
        }

        if (!room.FitsGrid())
        {
            violations.Add(new(
                "department.room_out_of_bounds",
                field,
                $"Phòng \"{room.Label}\" phải nằm trong lưới {OfficeGrid.Columns}×{OfficeGrid.Rows}, kích thước tối thiểu {OfficeGrid.MinRoomSize}×{OfficeGrid.MinRoomSize}."));
        }

        if (room.SortOrder < 0)
        {
            violations.Add(new("department.sort_order_invalid", $"{field}.sortOrder", "Thứ tự phòng không được âm."));
        }

        if (!RoomAssetPresets.IsKnown(room.AssetPreset))
        {
            violations.Add(new("department.room_preset_invalid", $"{field}.assetPreset", "Nội thất không thuộc danh sách được hỗ trợ."));
        }
    }

    private static void AddIfDuplicated<TKey>(
        IReadOnlyList<DepartmentRoom> rooms,
        Func<DepartmentRoom, TKey> key,
        string code,
        string message,
        List<DepartmentRuleViolation> violations)
    {
        if (rooms.GroupBy(key).Any(g => g.Count() > 1))
        {
            violations.Add(new(code, "rooms", message));
        }
    }
}
