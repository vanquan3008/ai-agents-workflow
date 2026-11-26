using System.ComponentModel.DataAnnotations;

namespace DevAgent.Application.Options;

/// <summary>URL công khai để dựng liên kết trong thông báo Discord/PR (màn hình duyệt, chi tiết task).</summary>
public sealed class PublicUrlsOptions
{
    public const string SectionName = "PublicUrls";

    [Required]
    [Url]
    public string Dashboard { get; set; } = "http://localhost:5173";

    public string TaskUrl(Guid taskId) => $"{Dashboard.TrimEnd('/')}/tasks/{taskId}";

    public string ApprovalUrl(Guid approvalId) => $"{Dashboard.TrimEnd('/')}/approvals/{approvalId}";
}
