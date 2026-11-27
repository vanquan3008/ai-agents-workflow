using DevAgent.Application.Abstractions.Security;
using DevAgent.Application.Features.Auth;
using DevAgent.Application.Options;
using DevAgent.Domain.Users;
using DevAgent.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevAgent.Infrastructure.Persistence;

/// <summary>
/// Applies migrations (when enabled) and ensures a super admin exists. Runs in StartAsync so the app only
/// serves requests once the database is ready.
/// </summary>
internal sealed partial class DatabaseInitializer(
    IServiceProvider services,
    IOptions<DatabaseOptions> options,
    ILogger<DatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();

        if (options.Value.ApplyMigrationsOnStartup)
        {
            LogMigrating(logger);
            await scope.ServiceProvider.GetRequiredService<DevAgentDbContext>().Database.MigrateAsync(cancellationToken);
        }

        await ActivatorUtilities.CreateInstance<SuperAdminSeeder>(scope.ServiceProvider).SeedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying database migrations")]
    private static partial void LogMigrating(ILogger logger);
}

/// <summary>
/// The only seed: the first super admin from <c>Bootstrap:SuperAdmin</c> (secrets via environment variables).
/// Runs only while no super admin exists. The seeded password is temporary and must be changed on first sign-in.
/// No sample projects, repositories or users are created.
/// </summary>
public sealed partial class SuperAdminSeeder(
    DevAgentDbContext context,
    IPasswordHasher hasher,
    IOptions<BootstrapOptions> bootstrap,
    IOptions<AuthPolicyOptions> policy,
    TimeProvider clock,
    ILogger<SuperAdminSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (await context.Users.AnyAsync(u => u.SystemRole == SystemRole.SuperAdmin, cancellationToken))
        {
            return;
        }

        var admin = bootstrap.Value.SuperAdmin;
        if (string.IsNullOrWhiteSpace(admin.Email) || string.IsNullOrWhiteSpace(admin.Password))
        {
            LogMissingBootstrap(logger);
            return;
        }

        var email = User.NormalizeEmail(admin.Email);
        var errors = PasswordPolicy.Validate(admin.Password, email, policy.Value);
        if (errors.Length > 0)
        {
            throw new InvalidOperationException($"Bootstrap:SuperAdmin:Password chưa đạt chính sách mật khẩu: {string.Join(" ", errors)}");
        }

        var existing = await context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (existing is not null)
        {
            // The account already exists: promote it instead of creating a duplicate email.
            existing.ChangeRole(SystemRole.SuperAdmin);
            existing.Activate();
        }
        else
        {
            var user = User.Create(email, admin.DisplayName, SystemRole.SuperAdmin, clock.GetUtcNow());
            user.SetPassword(hasher.Hash(admin.Password), mustChange: true);
            context.Users.Add(user);
        }

        await context.SaveChangesAsync(cancellationToken);
        LogSeeded(logger, email);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded super admin {Email}; password change required on first sign-in")]
    private static partial void LogSeeded(ILogger logger, string email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No super admin exists and Bootstrap:SuperAdmin:Email/Password is not configured; nobody can sign in to administer the system")]
    private static partial void LogMissingBootstrap(ILogger logger);
}
