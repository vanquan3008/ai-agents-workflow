using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Application.AgentRuntime.Budget;
using DevAgent.Application.AgentRuntime.Jobs;
using DevAgent.Application.AgentRuntime.Orchestration;
using DevAgent.Application.AgentRuntime.Planner;
using DevAgent.Application.AgentRuntime.Steps;
using Microsoft.Extensions.DependencyInjection;

namespace DevAgent.Application.AgentRuntime;

/// <summary>
/// Agent runtime: the orchestrator, its pipeline steps, the agents they call and the budget guard.
/// Lives only in the worker, which provides <see cref="IWorkerIdentity"/>.
/// </summary>
internal static class AgentRuntimeModule
{
    public static IServiceCollection AddAgentRuntimeModule(this IServiceCollection services) => services
        .AddScoped<IAgentOrchestrator, AgentOrchestrator>()
        .AddScoped<BudgetGuard>()
        .AddScoped<IPlannerAgent, PlannerAgent>()

        // Registration order is irrelevant: each step declares the status it handles.
        .AddScoped<IWorkflowStep, StartTaskStep>()
        .AddScoped<IWorkflowStep, PlanningStep>()
        .AddScoped<IWorkflowStep, PendingImplementationStep>()
        .AddScoped<IJobHandler, ProcessTaskJobHandler>();
}
