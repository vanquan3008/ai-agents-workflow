using DevAgent.Application.Abstractions.Events;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Approvals.EventHandlers;
using DevAgent.Domain.Approvals;
using Microsoft.Extensions.DependencyInjection;

namespace DevAgent.Application.Features.Approvals;

internal static class ApprovalsModule
{
    public static IServiceCollection AddApprovalsModule(this IServiceCollection services) => services
        .AddScoped<ICommandHandler<DecideApprovalCommand, ApprovalDto>, DecideApprovalHandler>()
        .AddScoped<IQueryHandler<ListApprovalsQuery, PagedResult<ApprovalDto>>, ListApprovalsHandler>()
        .AddScoped<IQueryHandler<GetApprovalQuery, ApprovalDto>, GetApprovalHandler>()
        .AddScoped<IDomainEventHandler<ApprovalDecided>, ApprovalDecidedHandler>();
}
