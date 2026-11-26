using System.Globalization;
using DevAgent.Application.Abstractions.Ai;
using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Options;
using DevAgent.Domain.Common;
using Microsoft.Extensions.Options;

namespace DevAgent.Application.Orchestration;

/// <summary>Kết quả xin hạn mức cho lần gọi model kế tiếp.</summary>
public sealed record BudgetGrant(bool Allowed, int MaxOutputTokens, string? Reason)
{
    public static BudgetGrant Deny(string reason) => new(false, 0, reason);
}

/// <summary>
/// Bảo đảm trần ngân sách mỗi task (checklist 0.7, 2B). Trước <b>mỗi</b> lần gọi model:
/// kiểm tra số lần gọi, rồi tính trần token đầu ra sao cho chi phí đầu vào ước tính + đầu ra tối đa
/// không vượt ngân sách còn lại (USD và token). Không đủ cho một lần gọi tối thiểu thì từ chối.
/// Thiếu đơn giá model thì từ chối (fail-closed) vì không thể bảo đảm trần chi phí.
/// </summary>
public sealed class BudgetGuard(IExecutionRepository executions, IModelClient modelClient, IOptions<AgentLimitsOptions> limits)
{
    /// <summary>Dưới mức này một lần gọi khó cho ra kết quả hữu ích; dừng thay vì gọi rồi bị cắt.</summary>
    public const int MinimumOutputTokens = 1024;

    private const decimal Million = 1_000_000m;

    /// <summary>
    /// Ước tính token đầu vào thận trọng (~2 ký tự/token, vì tiếng Việt có dấu và mã nguồn tách token dày hơn tiếng Anh).
    /// Ước tính cao hơn thực tế chỉ làm giảm trần đầu ra, không làm vượt ngân sách.
    /// </summary>
    public static int EstimateInputTokens(ModelRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var chars = request.SystemPrompt.Length + request.Messages.Sum(m => m.Content.Length);
        return (int)Math.Ceiling(chars / 2.0) + 64;
    }

    public async Task<BudgetGrant> ReserveAsync(TaskId taskId, string model, int estimatedInputTokens, int requestedMaxOutputTokens, CancellationToken cancellationToken)
    {
        var options = limits.Value;
        var used = await executions.GetTaskUsageAsync(taskId, cancellationToken);

        if (used.ModelCalls >= options.MaxModelCallsPerTask)
        {
            return BudgetGrant.Deny($"Đã dùng hết {options.MaxModelCallsPerTask} lần gọi model cho task.");
        }

        var price = modelClient.GetPrice(model);
        if (price is null)
        {
            return BudgetGrant.Deny($"Chưa cấu hình đơn giá cho model '{model}' nên không thể bảo đảm trần ngân sách.");
        }

        long maxOutput = Math.Min(requestedMaxOutputTokens, options.MaxOutputTokensPerCall);

        var remainingTokens = options.MaxTokensPerTask - used.Usage.TotalTokens - estimatedInputTokens;
        maxOutput = Math.Min(maxOutput, remainingTokens);

        var remainingUsd = options.MaxCostUsdPerTask - used.Usage.EstimatedCostUsd;
        var inputCost = estimatedInputTokens * price.InputPerMillion / Million;
        var afterInput = remainingUsd - inputCost;
        if (afterInput <= 0m && (price.InputPerMillion > 0m || price.OutputPerMillion > 0m))
        {
            return BudgetGrant.Deny(BudgetMessage(used.Usage.EstimatedCostUsd, options.MaxCostUsdPerTask));
        }

        if (price.OutputPerMillion > 0m)
        {
            var affordable = Math.Floor(afterInput * Million / price.OutputPerMillion);
            maxOutput = (long)Math.Min(maxOutput, affordable);
        }

        if (maxOutput < MinimumOutputTokens)
        {
            return BudgetGrant.Deny(
                remainingTokens < MinimumOutputTokens
                    ? $"Chạm trần token: đã dùng {used.Usage.TotalTokens} / {options.MaxTokensPerTask} token."
                    : BudgetMessage(used.Usage.EstimatedCostUsd, options.MaxCostUsdPerTask));
        }

        return new BudgetGrant(true, (int)maxOutput, null);
    }

    private static string BudgetMessage(decimal used, decimal cap) =>
        string.Create(CultureInfo.InvariantCulture, $"Chạm trần ngân sách: đã dùng {used:0.0000} USD / {cap:0.00} USD, không đủ cho lần gọi model tiếp theo.");
}
