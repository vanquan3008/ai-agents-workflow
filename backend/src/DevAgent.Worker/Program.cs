using DevAgent.Application;
using DevAgent.Application.Abstractions.Security;
using DevAgent.Application.Orchestration;
using DevAgent.Infrastructure;
using DevAgent.Infrastructure.Observability;
using DevAgent.Worker;
using Serilog;

// Logger tạm chỉ để ghi lỗi khởi động; logger chính được cấu hình trong AddDevAgentObservability.
// Không dùng CreateBootstrapLogger: logger reloadable chỉ "đóng băng" được một lần mỗi tiến trình (hỏng khi test chạy nhiều host).
Log.Logger = new LoggerConfiguration().WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture).CreateLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.AddDevAgentObservability("devagent-worker");

    builder.Services.AddApplication();
    builder.Services.AddAgentOrchestration();
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
    builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(45));

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Worker dừng do lỗi khởi động");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
