using DevAgent.Infrastructure;
using DevAgent.Infrastructure.Options;
using DevAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DevAgent.Api;

/// <summary>
/// Used by <c>dotnet ef</c> to create migrations without starting the API or a database.
/// Uses the API's configuration providers without starting its hosted services.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DevAgentDbContext>
{
    public DevAgentDbContext CreateDbContext(string[] args)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = Path.GetDirectoryName(typeof(DesignTimeDbContextFactory).Assembly.Location),
        });
        var connectionString = builder.Configuration.GetConnectionString(DatabaseOptions.ConnectionStringName);
        var database = builder.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new();

        var options = new DbContextOptionsBuilder<DevAgentDbContext>();
        DependencyInjection.ConfigureDbContext(options, string.IsNullOrWhiteSpace(connectionString) ? null : connectionString,
            database.DesignTimeCommandTimeoutSeconds, maxRetryCount: 0);
        return new DevAgentDbContext(options.Options);
    }
}
