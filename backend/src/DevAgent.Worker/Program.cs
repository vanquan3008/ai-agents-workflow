using DevAgent.Application;
using DevAgent.Application.Abstractions.Security;
using DevAgent.Application.AgentRuntime.Jobs;
using DevAgent.Infrastructure;
using DevAgent.Infrastructure.Observability;
using DevAgent.Worker.Hosting;
using DevAgent.Worker.Jobs;
using Serilog;

// Temporary logger for startup failures only; the real logger is configured by AddDevAgentObservability.
// CreateBootstrapLogger is avoided because a reloadable logger can be frozen only once per process, which breaks multi-host tests.
Log.Logger = new LoggerConfiguration().WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture).CreateLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.AddDevAgentObservability("devagent-worker");

    builder.Services.AddApplication();
    builder.Services.AddWorkerJobHandlers();
    builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

    builder.Services.AddOptions<WorkerOptions>()
        .Bind(builder.Configuration.GetSection(WorkerOptions.SectionName))
        .ValidateDataAnnotations()
        .Validate(o => o.LeaseRenewIntervalSeconds < o.LeaseDurationSeconds, "Worker:LeaseRenewIntervalSeconds phải nhỏ hơn LeaseDurationSeconds.")
        .ValidateOnStart();

    builder.Services.AddSingleton<IWorkerIdentity, WorkerIdentity>();
    builder.Services.AddScoped<ICurrentUser, NoCurrentUser>();

    builder.Services.AddSingleton<JobProcessingService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<JobProcessingService>());
    builder.Services.AddHostedService<HeartbeatService>();
    builder.Services.AddHostedService<DeadLetterReconciler>();
    builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(45));

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Worker stopped because of a startup failure");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
