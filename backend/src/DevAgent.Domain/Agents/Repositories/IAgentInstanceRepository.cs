using DevAgent.Domain.Agents;

namespace DevAgent.Domain.Agents.Repositories;

public interface IAgentInstanceRepository
{
    Task<AgentInstance?> GetByWorkerIdAsync(string workerId, CancellationToken cancellationToken);

    void Add(AgentInstance instance);
}
