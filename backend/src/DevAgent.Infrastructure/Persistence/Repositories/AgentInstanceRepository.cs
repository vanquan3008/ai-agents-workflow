using DevAgent.Domain.Agents;
using DevAgent.Domain.Agents.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DevAgent.Infrastructure.Persistence.Repositories;

internal sealed class AgentInstanceRepository(DevAgentDbContext context) : IAgentInstanceRepository
{
    public Task<AgentInstance?> GetByWorkerIdAsync(string workerId, CancellationToken cancellationToken) =>
        context.AgentInstances.FirstOrDefaultAsync(i => i.WorkerId == workerId, cancellationToken);

    public void Add(AgentInstance instance) => context.AgentInstances.Add(instance);
}
