using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DevAgent.Infrastructure.Persistence;

/// <summary>
/// Used by <c>dotnet ef</c> to create migrations without starting the API or a database.
/// <c>database update</c> reads the connection string from <c>ConnectionStrings__Postgres</c>.
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
