using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace DistributedQuery.Infrastructure.Observability;

public static class OpenTelemetryServiceCollectionExtensions
{
    public static IServiceCollection AddDistributedQueryObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        ObservabilityHostRole hostRole)
    {
        var options = configuration.GetSection(OpenTelemetryOptions.SectionName).Get<OpenTelemetryOptions>()
            ?? new OpenTelemetryOptions();

        var serviceName = string.IsNullOrWhiteSpace(options.ServiceName)
            ? hostRole switch
            {
                ObservabilityHostRole.Api => "DistributedQuery.Api",
                ObservabilityHostRole.Coordinator => "DistributedQuery.Coordinator",
                ObservabilityHostRole.Worker => "DistributedQuery.Worker",
                _ => "DistributedQuery"
            }
            : options.ServiceName;

        services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName, serviceVersion: options.ServiceVersion))
            .WithTracing(tracing =>
            {
                var builder = tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddGrpcClientInstrumentation()
                    .AddSource("DistributedQuery.*");

                if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
                {
                    builder.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(options.OtlpEndpoint));
                }
            })
            .WithMetrics(metrics =>
            {
                var builder = metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(DqeeMetrics.Api.Name)
                    .AddMeter(DqeeMetrics.Coordinator.Name)
                    .AddMeter(DqeeMetrics.Worker.Name)
                    .AddMeter(DqeeMetrics.Grpc.Name);

                if (options.EnablePrometheusEndpoint)
                {
                    builder.AddPrometheusExporter();
                }

                if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
                {
                    builder.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(options.OtlpEndpoint));
                }
            });

        return services;
    }
}
