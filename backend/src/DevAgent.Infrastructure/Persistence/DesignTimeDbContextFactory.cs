using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DevAgent.Infrastructure.Persistence;

/// <summary>
/// Dùng bởi <c>dotnet ef</c> khi tạo migration: không cần khởi động API hay có DB thật.
/// Khi chạy <c>database update</c>, chuỗi kết nối lấy từ biến môi trường <c>ConnectionStrings__Postgres</c>.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DevAgentDbContext>
{
    public DevAgentDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=devagent;Username=devagent;Password=devagent";

        var options = new DbContextOptionsBuilder<DevAgentDbContext>();
        DependencyInjection.ConfigureDbContext(options, connectionString, commandTimeoutSeconds: 120, maxRetryCount: 0);
        return new DevAgentDbContext(options.Options);
    }
}
