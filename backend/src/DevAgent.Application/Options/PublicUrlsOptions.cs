using System.ComponentModel.DataAnnotations;

namespace DevAgent.Application.Options;

/// <summary>Public dashboard URL used to build links in chat notifications and pull requests.</summary>
public sealed class PublicUrlsOptions
{
    public const string SectionName = "PublicUrls";

    [Required]
    [Url]
    public string Dashboard { get; set; } = "http://localhost:5173";

    public string TaskUrl(Guid taskId) => $"{Dashboard.TrimEnd('/')}/tasks/{taskId}";

    public string ApprovalUrl(Guid approvalId) => $"{Dashboard.TrimEnd('/')}/approvals/{approvalId}";
}
