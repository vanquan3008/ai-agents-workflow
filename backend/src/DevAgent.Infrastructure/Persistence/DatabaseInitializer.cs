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
/// Áp migration (nếu bật) và bảo đảm hệ thống có Super Admin khi khởi động. Chạy trong StartAsync
/// nên ứng dụng chỉ nhận request sau khi DB sẵn sàng.
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Đang áp migration cơ sở dữ liệu")]
    private static partial void LogMigrating(ILogger logger);
}

/// <summary>
/// Seed duy nhất: tài khoản Super Admin đầu tiên, lấy từ cấu hình <c>Bootstrap:SuperAdmin</c> (secret qua biến môi trường).
/// Chỉ chạy khi hệ thống chưa có Super Admin nào. Mật khẩu seed là mật khẩu tạm: bắt buộc đổi ở lần đăng nhập đầu.
/// Không seed project, repo hay người dùng mẫu.
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
            // Tài khoản đã có (vd tạo trước đó): nâng quyền thay vì tạo trùng email.
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Đã tạo Super Admin {Email}; bắt buộc đổi mật khẩu ở lần đăng nhập đầu")]
    private static partial void LogSeeded(ILogger logger, string email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Chưa có Super Admin và chưa cấu hình Bootstrap:SuperAdmin:Email/Password; không ai đăng nhập được để quản trị")]
    private static partial void LogMissingBootstrap(ILogger logger);
}
