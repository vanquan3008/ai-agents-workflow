using DevAgent.Api.Auth;
using DevAgent.Api.Common;
using DevAgent.Api.Endpoints;
using DevAgent.Api.Realtime;
using DevAgent.Application;
using DevAgent.Infrastructure;
using DevAgent.Infrastructure.Observability;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;

// Temporary logger for startup failures only; the real logger is configured by AddDevAgentObservability.
// CreateBootstrapLogger is avoided because a reloadable logger can be frozen only once per process, which breaks multi-host tests.
Log.Logger = new LoggerConfiguration().WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture).CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ---- Composition root: Domain ← Application ← Infrastructure ← Api ----
    builder.AddDevAgentObservability(
        tracing => tracing.AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)),
        metrics => metrics.AddAspNetCoreInstrumentation());

    builder.Services.AddApplication();
    builder.Services.AddAuthModule();
    builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
    builder.Services.AddDatabaseInitializer();
    builder.Services.AddRealtimeListener<SignalRTaskEventSink, SignalRAccessSink>(builder.Configuration);
    builder.Services.AddSingleton<RealtimeConnectionRegistry>();
    builder.Services.AddSingleton<RealtimeConnectionSweeper>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<RealtimeConnectionSweeper>());

    builder.Services.AddDevAgentAuth(builder.Configuration);
    builder.Services.AddDevAgentRateLimiting(builder.Configuration);
    builder.Services.AddSignalR();
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddOpenApi();

    var cors = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new();
    builder.Services.AddCors(options => options.AddPolicy(CorsOptions.PolicyName, policy => policy
        .WithOrigins(cors.AllowedOrigins)
        .WithHeaders("Authorization", "Content-Type", "Idempotency-Key", "X-DevAgent-Client", "X-Requested-With", "x-signalr-user-agent")
        .AllowAnyMethod()
        .AllowCredentials()
        .WithExposedHeaders("Location")));

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }
    else
    {
        app.UseHsts();
    }

    app.UseCors(CorsOptions.PolicyName);
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    // Liveness: the process is up. Readiness: PostgreSQL is reachable.
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
    app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") }).AllowAnonymous();

    app.MapGroup("/api/v1").MapAuthEndpoints();

    var api = app.MapGroup("/api/v1").RequireAuthorization().RequirePasswordChanged();
    api.MapAdminEndpoints()
        .MapProjectsEndpoints()
        .MapTasksEndpoints()
        .MapApprovalsEndpoints()
        .MapExecutionsEndpoints()
        .MapWorkflowsEndpoints()
        .MapDepartmentsEndpoints();

    app.MapHub<TasksHub>(TasksHub.Path);

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "API stopped because of a startup failure");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>Exposed for WebApplicationFactory in integration tests.</summary>
public partial class Program;
