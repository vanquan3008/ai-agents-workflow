using DevAgent.Domain.Repositories;
using DevAgent.Domain.Tasks;
using DevAgent.Domain.Workflows;

namespace DevAgent.Application.AgentRuntime.Steps;

/// <summary>Everything one advancement needs: the loaded task, its repository, the effective workflow snapshot and the worker.</summary>
public sealed record WorkflowContext(AgentTask Task, CodeRepository Repository, WorkflowDefinition Workflow, string WorkerId);
