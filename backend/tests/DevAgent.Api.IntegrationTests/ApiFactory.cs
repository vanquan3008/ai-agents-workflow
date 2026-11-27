using System.Net.Http.Headers;
using DevAgent.Application.Abstractions.Security;
using DevAgent.Domain.Common;
using DevAgent.Domain.Users;
using DevAgent.Domain.Users.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace DevAgent.Api.IntegrationTests;

/// <summary>
/// Runs the real API against a real PostgreSQL (Testcontainers) in the Testing environment with deterministic AI and GitHub fakes.
/// Authentication uses real JWTs signed by the application. Requires Docker; tests are skipped without it (CI always has Docker).
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;

    public bool DockerAvailable { get; private set; }

    public async ValueTask InitializeAsync()
    {
        try
        {
            _postgres = new PostgreSqlBuilder("postgres:17-alpine")
                .WithDatabase("devagent_test")
                .WithUsername("devagent")
                .WithPassword("devagent")
                .Build();
            await _postgres.StartAsync();
            DockerAvailable = true;
        }
#pragma warning disable CA1031 // No Docker: flag it so tests skip themselves.
        catch (Exception)
#pragma warning restore CA1031
        {
            DockerAvailable = false;
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }

    /// <summary>A client carrying a valid access token for the user, signed by the application token service.</summary>
    public HttpClient ClientFor(Guid userId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessTokenFor(userId));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    /// <summary>A valid access token for the user, signed by the application token service.</summary>
    public string AccessTokenFor(Guid userId)
    {
        using var scope = Services.CreateScope();
        var user = scope.ServiceProvider.GetRequiredService<IUserRepository>().GetByIdAsync(new UserId(userId), CancellationToken.None).GetAwaiter().GetResult()
            ?? throw new InvalidOperationException($"Không có người dùng {userId}; gọi SeedAsync trước.");
        return scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(user).Token;
    }

    public Task SeedAsync() => TestData.SeedAsync(Services, CancellationToken.None);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Like Development: catch missing registrations and singletons depending on scoped services early.
        builder.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = true;
            options.ValidateScopes = true;
        });

        // UseSetting (not ConfigureAppConfiguration) so values apply before Program.cs reads configuration.
        builder.UseSetting("ConnectionStrings:Postgres", _postgres?.GetConnectionString() ?? "Host=localhost;Database=unused");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", DockerAvailable ? "true" : "false");
        builder.UseSetting("Bootstrap:SuperAdmin:Email", TestData.SuperAdminEmail);
        builder.UseSetting("Bootstrap:SuperAdmin:Password", TestData.SuperAdminPassword);
    }
}
