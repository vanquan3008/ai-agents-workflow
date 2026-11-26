namespace DevAgent.Domain.Tasks;

/// <summary>
/// Vòng đời task (checklist 0.4). Tên trạng thái khớp với tài liệu (PENDING, WAITING_FOR_APPROVAL...)
/// và được lưu dạng chuỗi snake/upper trong DB để ổn định khi thêm giá trị mới.
/// </summary>
public enum AgentTaskStatus
{
    Pending = 1,
    Planning = 2,
    NeedsClarification = 3,
    WaitingForApproval = 4,
    InProgress = 5,
    Testing = 6,
    Reviewing = 7,
    Publishing = 8,
    Completed = 9,
    Rejected = 10,
    Failed = 11,
    Blocked = 12,
    Cancelled = 13,
    TimedOut = 14,
}

/// <summary>Bảng chuyển trạng thái hợp lệ. Mọi chuyển trạng thái khác đều bị domain từ chối.</summary>
public static class AgentTaskStateMachine
{
    private static readonly Dictionary<AgentTaskStatus, AgentTaskStatus[]> Allowed = new()
    {
        [AgentTaskStatus.Pending] = [AgentTaskStatus.Planning, AgentTaskStatus.Cancelled],
        [AgentTaskStatus.Planning] =
        [
            AgentTaskStatus.NeedsClarification, AgentTaskStatus.WaitingForApproval, AgentTaskStatus.InProgress,
            AgentTaskStatus.Failed, AgentTaskStatus.Blocked, AgentTaskStatus.Cancelled, AgentTaskStatus.TimedOut,
        ],
        [AgentTaskStatus.NeedsClarification] = [AgentTaskStatus.Planning, AgentTaskStatus.Cancelled, AgentTaskStatus.TimedOut],
        [AgentTaskStatus.WaitingForApproval] =
        [
            AgentTaskStatus.InProgress, AgentTaskStatus.Publishing, AgentTaskStatus.Planning,
            AgentTaskStatus.Rejected, AgentTaskStatus.Cancelled, AgentTaskStatus.TimedOut,
        ],
        [AgentTaskStatus.InProgress] =
        [
            AgentTaskStatus.Testing, AgentTaskStatus.WaitingForApproval, AgentTaskStatus.Failed,
            AgentTaskStatus.Blocked, AgentTaskStatus.Cancelled, AgentTaskStatus.TimedOut,
        ],
        [AgentTaskStatus.Testing] =
        [
            AgentTaskStatus.Reviewing, AgentTaskStatus.InProgress, AgentTaskStatus.Failed,
            AgentTaskStatus.Blocked, AgentTaskStatus.Cancelled, AgentTaskStatus.TimedOut,
        ],
        [AgentTaskStatus.Reviewing] =
        [
            AgentTaskStatus.Publishing, AgentTaskStatus.WaitingForApproval, AgentTaskStatus.InProgress,
            AgentTaskStatus.Failed, AgentTaskStatus.Blocked, AgentTaskStatus.Cancelled, AgentTaskStatus.TimedOut,
        ],
        [AgentTaskStatus.Publishing] = [AgentTaskStatus.Completed, AgentTaskStatus.Failed, AgentTaskStatus.Blocked],

        // Người vận hành có thể thử lại task lỗi/bị chặn/hết giờ: quay về Pending, giữ nguyên lịch sử.
        [AgentTaskStatus.Failed] = [AgentTaskStatus.Pending],
        [AgentTaskStatus.Blocked] = [AgentTaskStatus.Pending, AgentTaskStatus.Cancelled],
        [AgentTaskStatus.TimedOut] = [AgentTaskStatus.Pending],
        [AgentTaskStatus.Completed] = [],
        [AgentTaskStatus.Rejected] = [],
        [AgentTaskStatus.Cancelled] = [],
    };

    public static bool CanTransition(AgentTaskStatus from, AgentTaskStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static bool IsTerminal(AgentTaskStatus status) =>
        status is AgentTaskStatus.Completed or AgentTaskStatus.Rejected or AgentTaskStatus.Cancelled;

    /// <summary>Trạng thái mà Worker đang (hoặc sẽ) chủ động xử lý.</summary>
    public static bool IsActive(AgentTaskStatus status) =>
        status is AgentTaskStatus.Pending or AgentTaskStatus.Planning or AgentTaskStatus.InProgress
            or AgentTaskStatus.Testing or AgentTaskStatus.Reviewing or AgentTaskStatus.Publishing;

    public static IReadOnlyCollection<AgentTaskStatus> NextStates(AgentTaskStatus from) =>
        Allowed.TryGetValue(from, out var targets) ? targets : [];
}
