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

// Logger tạm chỉ để ghi lỗi khởi động; logger chính được cấu hình trong AddDevAgentObservability.
// Không dùng CreateBootstrapLogger: logger reloadable chỉ "đóng băng" được một lần mỗi tiến trình (hỏng khi test chạy nhiều host).
Log.Logger = new LoggerConfiguration().WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture).CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ---- Composition root: Domain ← Application ← Infrastructure ← Api ----
    builder.AddDevAgentObservability(
        "devagent-api",
        tracing => tracing.AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)),
        metrics => metrics.AddAspNetCoreInstrumentation());

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
    builder.Services.AddDatabaseInitializer();
    builder.Services.AddRealtimeListener<SignalRTaskEventSink>();

    builder.Services.AddDevAgentAuth(builder.Configuration, builder.Environment);
    builder.Services.AddDevAgentRateLimiting(builder.Configuration);
    builder.Services.AddSignalR();
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddOpenApi();

    var cors = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new();
    builder.Services.AddCors(options => options.AddPolicy(CorsOptions.PolicyName, policy => policy
        .WithOrigins(cors.AllowedOrigins)
        .AllowAnyHeader()
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

    // Liveness: tiến trình còn sống. Readiness: kết nối được PostgreSQL.
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
    app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") }).AllowAnonymous();

    var api = app.MapGroup("/api/v1").RequireAuthorization();
    api.MapProjectsEndpoints()
        .MapTasksEndpoints()
        .MapApprovalsEndpoints()
        .MapExecutionsEndpoints();

    app.MapHub<TasksHub>(TasksHub.Path);

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "API dừng do lỗi khởi động");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>Mở cho WebApplicationFactory trong integration test.</summary>
public partial class Program;
