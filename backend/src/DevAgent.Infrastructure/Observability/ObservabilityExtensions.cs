using System.Globalization;
using DevAgent.Application.Common;
using DevAgent.Infrastructure.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Json;

namespace DevAgent.Infrastructure.Observability;

public static class ObservabilityExtensions
{
    /// <summary>
    /// Log có cấu trúc bằng Serilog (cấu hình trong section "Serilog") và trace/metric bằng OpenTelemetry
    /// (checklist 1.5). Dữ liệu nhạy cảm được che trước khi ghi.
    /// </summary>
    public static IHostApplicationBuilder AddDevAgentObservability(
        this IHostApplicationBuilder builder,
        string serviceName,
        Action<TracerProviderBuilder>? configureTracing = null,
        Action<MeterProviderBuilder>? configureMetrics = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var observability = builder.Configuration.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>() ?? new();

        builder.Services.AddSerilog((services, logger) =>
        {
            logger
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("Service", serviceName)
            .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
            .Enrich.With<SensitiveDataMaskingEnricher>()
            .Enrich.With<TraceContextEnricher>();

            // Sink console chọn bằng cấu hình thay vì mảng WriteTo (mảng bị gộp theo chỉ số giữa các file appsettings).
            if (observability.ConsoleFormat == "text")
            {
                logger.WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}{NewLine}  {Message:lj} {Properties:j}{NewLine}{Exception}",
                    formatProvider: CultureInfo.InvariantCulture);
            }
            else
            {
                logger.WriteTo.Console(new JsonFormatter(renderMessage: true, formatProvider: CultureInfo.InvariantCulture));
            }
        });
        var version = typeof(ObservabilityExtensions).Assembly.GetName().Version?.ToString() ?? "0.0.0";

        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName, observability.ServiceNamespace, version)
                .AddAttributes([new("deployment.environment.name", builder.Environment.EnvironmentName)]))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(DevAgentTelemetry.SourceName)
                    .AddHttpClientInstrumentation()
                    .AddNpgsql();
                configureTracing?.Invoke(tracing);
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(DevAgentTelemetry.SourceName)
                    .AddRuntimeInstrumentation()
                    .AddHttpClientInstrumentation();
                configureMetrics?.Invoke(metrics);
            });

        if (!string.IsNullOrWhiteSpace(observability.OtlpEndpoint))
        {
            otel.UseOtlpExporter(
                observability.OtlpProtocol == "grpc" ? OtlpExportProtocol.Grpc : OtlpExportProtocol.HttpProtobuf,
                new Uri(observability.OtlpEndpoint));
        }

        return builder;
    }
}
