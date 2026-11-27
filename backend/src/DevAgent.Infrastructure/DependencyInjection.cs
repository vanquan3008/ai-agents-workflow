using System.Net.Http.Headers;
using DevAgent.Application.Abstractions.Ai;
using DevAgent.Application.Abstractions.Integrations;
using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Abstractions.Realtime;
using DevAgent.Application.Abstractions.Security;
using DevAgent.Application.Features.Admin;
using DevAgent.Application.Features.Approvals;
using DevAgent.Application.Features.Executions;
using DevAgent.Application.Features.Projects;
using DevAgent.Application.Features.Tasks;
using DevAgent.Application.Options;
using DevAgent.Domain.Agents;
using DevAgent.Domain.Agents.Repositories;
using DevAgent.Domain.Approvals;
using DevAgent.Domain.Approvals.Repositories;
using DevAgent.Domain.Auditing;
using DevAgent.Domain.Auditing.Repositories;
using DevAgent.Domain.Executions;
using DevAgent.Domain.Executions.Repositories;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Projects.Repositories;
using DevAgent.Domain.Repositories;
using DevAgent.Domain.Repositories.Repositories;
using DevAgent.Domain.Tasks;
using DevAgent.Domain.Tasks.Repositories;
using DevAgent.Domain.Users;
using DevAgent.Domain.Users.Repositories;
using DevAgent.Domain.Workflows;
using DevAgent.Domain.Workflows.Repositories;
using DevAgent.Infrastructure.Ai;
using DevAgent.Infrastructure.Events;
using DevAgent.Infrastructure.Integrations;
using DevAgent.Infrastructure.Options;
using DevAgent.Infrastructure.Persistence;
using DevAgent.Infrastructure.Persistence.Jobs;
using DevAgent.Infrastructure.Persistence.Queries;
using DevAgent.Infrastructure.Persistence.Repositories;
using DevAgent.Infrastructure.Realtime;
using DevAgent.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DevAgent.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Infrastructure shared by API and worker: options validated on startup, PostgreSQL, the durable queue,
    /// AI/GitHub/Discord/sandbox adapters and credential encryption.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddSingleton(TimeProvider.System);
        services.AddValidatedOptions(configuration);
        services.AddPersistence(configuration);
        services.AddAiProvider(configuration);
        services.AddIntegrations(configuration);

        services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddHealthChecks().AddDbContextCheck<DevAgentDbContext>("postgres", tags: ["ready"]);
        return services;
    }

    /// <summary>API only: LISTENs on PostgreSQL and forwards events to <typeparamref name="TSink"/> (SignalR).</summary>
    public static IServiceCollection AddRealtimeListener<TSink>(this IServiceCollection services)
        where TSink : class, ITaskEventSink
    {
        services.AddSingleton<ITaskEventSink, TSink>();
        services.AddHostedService<PostgresTaskEventListener>();
        return services;
    }

    /// <summary>API only: applies migrations and the bootstrap seed on startup according to the Database section.</summary>
    public static IServiceCollection AddDatabaseInitializer(this IServiceCollection services) =>
        services.AddHostedService<DatabaseInitializer>();

    internal static void ConfigureDbContext(DbContextOptionsBuilder options, string connectionString, int commandTimeoutSeconds, int maxRetryCount) =>
        options
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "devagent");
                npgsql.CommandTimeout(commandTimeoutSeconds);
                if (maxRetryCount > 0)
                {
                    npgsql.EnableRetryOnFailure(maxRetryCount);
                }
            })
            .UseSnakeCaseNamingConvention();

    private static void AddValidatedOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(DatabaseOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<JobQueueOptions>().Bind(configuration.GetSection(JobQueueOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<AiOptions>().Bind(configuration.GetSection(AiOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<GitHubOptions>().Bind(configuration.GetSection(GitHubOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<DiscordOptions>().Bind(configuration.GetSection(DiscordOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SandboxOptions>().Bind(configuration.GetSection(SandboxOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SecurityOptions>().Bind(configuration.GetSection(SecurityOptions.SectionName)).ValidateOnStart();
        services.AddOptions<ObservabilityOptions>().Bind(configuration.GetSection(ObservabilityOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<AgentLimitsOptions>().Bind(configuration.GetSection(AgentLimitsOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<AuthPolicyOptions>().Bind(configuration.GetSection(AuthPolicyOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<BootstrapOptions>().Bind(configuration.GetSection(BootstrapOptions.SectionName)).ValidateDataAnnotations();
        services.AddOptions<PublicUrlsOptions>().Bind(configuration.GetSection(PublicUrlsOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();

        services.AddSingleton<IValidateOptions<AiOptions>, AiOptionsValidator>();
        services.AddSingleton<IValidateOptions<GitHubOptions>, GitHubOptionsValidator>();
        services.AddSingleton<IValidateOptions<DiscordOptions>, DiscordOptionsValidator>();
        services.AddSingleton<IValidateOptions<SecurityOptions>, SecurityOptionsValidator>();
    }

    private static void AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Thiếu ConnectionStrings:Postgres. Cấp qua biến môi trường ConnectionStrings__Postgres hoặc dotnet user-secrets.");
        }

        services.AddDbContext<DevAgentDbContext>((sp, options) =>
        {
            var db = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            ConfigureDbContext(options, connectionString, db.CommandTimeoutSeconds, db.MaxRetryCount);
            if (db.EnableSensitiveDataLogging)
            {
                options.EnableSensitiveDataLogging();
            }
        });

        services.AddScoped<DomainEventDispatcher>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<ICodeRepositoryRepository, CodeRepositoryRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IExecutionRepository, ExecutionRepository>();
        services.AddScoped<IApprovalRepository, ApprovalRepository>();
        services.AddScoped<IAgentInstanceRepository, AgentInstanceRepository>();
        services.AddScoped<IWorkflowRevisionRepository, WorkflowRevisionRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUserQueries, UserQueries>();
        services.AddScoped<IAuditLog, AuditLog>();

        services.AddScoped<IProjectQueries, ProjectQueries>();
        services.AddScoped<ITaskQueries, TaskQueries>();
        services.AddScoped<IApprovalQueries, ApprovalQueries>();
        services.AddScoped<IExecutionQueries, ExecutionQueries>();

        services.AddScoped<PostgresJobQueue>();
        services.AddScoped<IJobQueue>(sp => sp.GetRequiredService<PostgresJobQueue>());
        services.AddScoped<IJobStore>(sp => sp.GetRequiredService<PostgresJobQueue>());

        services.AddScoped<ITaskEventPublisher, PostgresTaskEventPublisher>();
    }

    private static void AddAiProvider(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration.GetSection(AiOptions.SectionName).GetValue(nameof(AiOptions.Provider), AiProvider.Fake);
        switch (provider)
        {
            case AiProvider.Anthropic:
                services.AddSingleton<IModelClient, AnthropicModelClient>();
                break;
            default:
                services.AddSingleton<IModelClient, FakeModelClient>();
                break;
        }
    }

    private static void AddIntegrations(this IServiceCollection services, IConfiguration configuration)
    {
        var github = configuration.GetSection(GitHubOptions.SectionName).Get<GitHubOptions>() ?? new();
        switch (github.Mode)
        {
            case GitHubMode.Fake:
                services.AddSingleton<ISourceControlGateway, FakeSourceControlGateway>();
                break;
            case GitHubMode.App:
                services.AddSingleton<ISourceControlGateway>(
                    new UnavailableSourceControlGateway("Adapter GitHub App chưa được triển khai (Phase 2A)."));
                break;
            default:
                services.AddSingleton<ISourceControlGateway>(
                    new UnavailableSourceControlGateway("GitHub chưa được cấu hình (GitHub:Mode = Disabled)."));
                break;
        }

        var discord = configuration.GetSection(DiscordOptions.SectionName).Get<DiscordOptions>() ?? new();
        if (discord.Enabled)
        {
            // No HttpClient retry policy: the durable queue owns retries (backoff, Retry-After, dead-letter).
            services.AddHttpClient<IChatNotifier, DiscordChatNotifier>((sp, http) =>
            {
                var options = sp.GetRequiredService<IOptions<DiscordOptions>>().Value;
                http.BaseAddress = new Uri(options.ApiBaseUrl.TrimEnd('/') + "/");
                http.Timeout = TimeSpan.FromSeconds(15);
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", options.BotToken);
            });
        }
        else
        {
            services.AddScoped<IChatNotifier, LoggingChatNotifier>();
        }

        services.AddSingleton<ISandboxManager, UnavailableSandboxManager>();
    }
}
