using DevAgent.Domain.Common;
using DevAgent.Domain.Executions;
using DevAgent.Domain.Executions.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DevAgent.Infrastructure.Persistence.Repositories;

internal sealed class ExecutionRepository(DevAgentDbContext context) : IExecutionRepository
{
    public Task<AgentExecution?> GetByIdAsync(ExecutionId id, CancellationToken cancellationToken) =>
        context.Executions.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AgentExecution>> ListRunningByTaskAsync(TaskId taskId, CancellationToken cancellationToken) =>
        await context.Executions.Where(e => e.TaskId == taskId && e.Status == ExecutionStatus.Running).ToListAsync(cancellationToken);

    public Task<int> CountAttemptsAsync(TaskId taskId, AgentStep step, CancellationToken cancellationToken) =>
        context.Executions.CountAsync(e => e.TaskId == taskId && e.Step == step, cancellationToken);

    public Task<int> CountFailuresSinceAsync(TaskId taskId, AgentStep step, DateTimeOffset since, CancellationToken cancellationToken) =>
        context.Executions.CountAsync(
            e => e.TaskId == taskId && e.Step == step && e.Status == ExecutionStatus.Failed && e.StartedAt >= since,
            cancellationToken);

    public async Task<TaskUsageTotals> GetTaskUsageAsync(TaskId taskId, CancellationToken cancellationToken)
    {
        var totals = await context.Executions
            .Where(e => e.TaskId == taskId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Input = g.Sum(e => e.Usage.InputTokens),
                Output = g.Sum(e => e.Usage.OutputTokens),
                CacheRead = g.Sum(e => e.Usage.CacheReadTokens),
                Cost = g.Sum(e => e.Usage.EstimatedCostUsd),
                Calls = g.Sum(e => e.ModelCalls),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return totals is null
            ? TaskUsageTotals.Zero
            : new TaskUsageTotals(new TokenUsage(totals.Input, totals.Output, totals.CacheRead, totals.Cost), totals.Calls);
    }

    public void Add(AgentExecution execution) => context.Executions.Add(execution);
}
