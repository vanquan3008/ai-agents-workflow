using DevAgent.Application.Abstractions.Events;
using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Domain.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace DevAgent.Application.Features.Notifications;

internal static class NotificationsModule
{
    /// <summary>The outbox runs in every process that changes task status (API and worker).</summary>
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services) => services
        .AddScoped<ITransactionalDomainEventHandler<TaskStatusChanged>, TaskNotificationOutbox>();

    /// <summary>Delivery runs only in the worker, which consumes the outbox jobs.</summary>
    public static IServiceCollection AddNotificationDelivery(this IServiceCollection services) => services
        .AddScoped<IJobHandler, NotificationJobHandler>();
}
