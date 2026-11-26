using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DevAgent.Infrastructure.Options;

/// <summary>
/// Ràng buộc chéo giữa các trường và theo môi trường. Chạy lúc khởi động (ValidateOnStart):
/// cấu hình sai làm tiến trình dừng ngay thay vì hỏng giữa chừng khi xử lý task.
/// </summary>
internal sealed class AiOptionsValidator(IHostEnvironment environment) : IValidateOptions<AiOptions>
{
    public ValidateOptionsResult Validate(string? name, AiOptions options)
    {
        var failures = new List<string>();
        if (options.Provider == AiProvider.Anthropic && string.IsNullOrWhiteSpace(options.Anthropic.ApiKey))
        {
            failures.Add("Ai:Anthropic:ApiKey là bắt buộc khi Ai:Provider = Anthropic (cấp qua env Ai__Anthropic__ApiKey).");
        }

        if (options.Provider == AiProvider.Fake && environment.IsProduction())
        {
            failures.Add("Ai:Provider = Fake không được phép ở Production.");
        }

        foreach (var model in options.Models.Values.Append(options.DefaultModel).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (options.Provider != AiProvider.Fake && !options.Pricing.ContainsKey(model))
            {
                failures.Add($"Thiếu Ai:Pricing cho model '{model}' — cần để ước tính chi phí và chặn theo ngân sách.");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

internal sealed class GitHubOptionsValidator(IHostEnvironment environment) : IValidateOptions<GitHubOptions>
{
    public ValidateOptionsResult Validate(string? name, GitHubOptions options)
    {
        var failures = new List<string>();
        if (options.Mode == GitHubMode.Fake && environment.IsProduction())
        {
            failures.Add("GitHub:Mode = Fake không được phép ở Production.");
        }

        if (options.Mode == GitHubMode.App)
        {
            if (options.AppId is null or <= 0)
            {
                failures.Add("GitHub:AppId là bắt buộc khi GitHub:Mode = App.");
            }

            if (string.IsNullOrWhiteSpace(options.PrivateKeyPath))
            {
                failures.Add("GitHub:PrivateKeyPath là bắt buộc khi GitHub:Mode = App.");
            }

            if (string.IsNullOrWhiteSpace(options.WebhookSecret))
            {
                failures.Add("GitHub:WebhookSecret là bắt buộc khi GitHub:Mode = App.");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

internal sealed class DiscordOptionsValidator : IValidateOptions<DiscordOptions>
{
    public ValidateOptionsResult Validate(string? name, DiscordOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.BotToken))
        {
            failures.Add("Discord:BotToken là bắt buộc khi Discord:Enabled = true (cấp qua env Discord__BotToken).");
        }

        if (string.IsNullOrWhiteSpace(options.NotificationChannelId))
        {
            failures.Add("Discord:NotificationChannelId là bắt buộc khi Discord:Enabled = true.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

internal sealed class SecurityOptionsValidator(IHostEnvironment environment) : IValidateOptions<SecurityOptions>
{
    public ValidateOptionsResult Validate(string? name, SecurityOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.EncryptionKey))
        {
            return environment.IsProduction()
                ? ValidateOptionsResult.Fail("Security:EncryptionKey là bắt buộc ở Production.")
                : ValidateOptionsResult.Success;
        }

        try
        {
            return Convert.FromBase64String(options.EncryptionKey).Length == 32
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail("Security:EncryptionKey phải là 32 byte mã hóa base64.");
        }
        catch (FormatException)
        {
            return ValidateOptionsResult.Fail("Security:EncryptionKey không phải base64 hợp lệ.");
        }
    }
}
