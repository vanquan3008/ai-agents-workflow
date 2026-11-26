using System.Net.Http.Headers;
using DevAgent.Api.Auth;
using DevAgent.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace DevAgent.Api.IntegrationTests;

/// <summary>
/// Chạy API thật trên PostgreSQL thật (Testcontainers) với môi trường Testing:
/// AI và GitHub dùng bản giả lập xác định, xác thực bằng header Development.
/// Cần Docker; nếu không có Docker, test được bỏ qua (CI luôn có Docker).
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
#pragma warning disable CA1031 // Không có Docker: đánh dấu để test tự bỏ qua.
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

    public HttpClient ClientFor(Guid userId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationHandler.HeaderName, userId.ToString());
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    public async Task SeedAsync()
    {
        using var scope = Services.CreateScope();
        var initializer = ActivatorUtilities.CreateInstance<DevelopmentDataSeeder>(scope.ServiceProvider);
        await initializer.SeedAsync(CancellationToken.None);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Giống Development: phát hiện sớm service thiếu đăng ký hoặc singleton phụ thuộc scoped.
        builder.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = true;
            options.ValidateScopes = true;
        });

        // UseSetting (không phải ConfigureAppConfiguration) để giá trị có hiệu lực ngay khi Program.cs đọc cấu hình.
        builder.UseSetting("ConnectionStrings:Postgres", _postgres?.GetConnectionString() ?? "Host=localhost;Database=unused");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", DockerAvailable ? "true" : "false");
    }
}
