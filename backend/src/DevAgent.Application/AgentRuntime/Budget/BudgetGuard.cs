using System.Globalization;
using DevAgent.Application.Abstractions.Ai;
using DevAgent.Application.Options;
using DevAgent.Domain.Common;
using DevAgent.Domain.Executions;
using DevAgent.Domain.Executions.Repositories;
using Microsoft.Extensions.Options;

namespace DevAgent.Application.AgentRuntime.Budget;

/// <summary>
/// Enforces the per-task budget before <b>every</b> model call: checks the call count, then caps output tokens so the
/// estimated input cost plus the maximum output stays within the remaining USD and token budget. Denies when not even a
/// minimal call fits, and fails closed when the model has no configured price.
/// </summary>
public sealed class BudgetGuard(IExecutionRepository executions, IModelClient modelClient, IOptions<AgentLimitsOptions> limits)
{
    /// <summary>Below this a call rarely yields a useful answer; stop rather than call and get truncated.</summary>
    public const int MinimumOutputTokens = 1024;

    private const decimal Million = 1_000_000m;

    /// <summary>
    /// Conservative input estimate (~2 characters per token, since accented Vietnamese and source code tokenize densely).
    /// Overestimating only lowers the output cap; it can never exceed the budget.
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
