namespace DistributedQuery.Infrastructure.Observability;

public sealed class OpenTelemetryOptions
{
    public const string SectionName = "OpenTelemetry";

    public string ServiceName { get; init; } = "DistributedQuery";

    public string ServiceVersion { get; init; } = "1.0.0";

    public string? OtlpEndpoint { get; init; }

    public bool EnablePrometheusEndpoint { get; init; } = true;

    public int PrometheusScrapePort { get; init; }
}
