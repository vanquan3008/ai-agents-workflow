namespace DevAgent.Domain.Policies;

/// <summary>Các vùng nhạy cảm theo chính sách pilot. Đụng tới bất kỳ vùng nào đều phải có người duyệt.</summary>
[Flags]
public enum SensitiveArea
{
    None = 0,
    Authentication = 1 << 0,
    Authorization = 1 << 1,
    Migration = 1 << 2,
    Credentials = 1 << 3,
    CiCd = 1 << 4,
    Destructive = 1 << 5,
}
