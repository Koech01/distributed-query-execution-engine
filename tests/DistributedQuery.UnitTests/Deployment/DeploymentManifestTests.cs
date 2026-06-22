using FluentAssertions;

namespace DistributedQuery.UnitTests.Deployment;

public sealed class DeploymentManifestTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void DockerCompose_DefinesRequiredApplicationAndInfrastructureServices()
    {
        var compose = ReadRepositoryFile("docker-compose.yml");

        compose.Should().Contain("api:");
        compose.Should().Contain("coordinator:");
        compose.Should().Contain("worker:");
        compose.Should().Contain("redis:");
        compose.Should().Contain("rabbitmq:");
        compose.Should().Contain("consul:");
        compose.Should().Contain("jaeger:");
        compose.Should().Contain("prometheus:");
        compose.Should().Contain("grafana:");

        compose.Should().Contain("redis:7-alpine");
        compose.Should().Contain("rabbitmq:3.13-management-alpine");
        compose.Should().Contain("hashicorp/consul:1.19");
        compose.Should().Contain("jaegertracing/all-in-one:1.60");
        compose.Should().Contain("prom/prometheus:v2.54.0");
        compose.Should().Contain("grafana/grafana:11.2.0");

        compose.Should().Contain("healthcheck:");
        compose.Should().Contain("http://localhost:8080/health/ready");
        compose.Should().Contain("http://localhost:5101/health/ready");
        compose.Should().Contain("stop_grace_period: 35s");
        compose.Should().Contain("rabbitmq_prometheus");
        compose.Should().Contain("./infra/prometheus.yml:/etc/prometheus/prometheus.yml:ro");
        compose.Should().Contain("./infra/grafana/provisioning:/etc/grafana/provisioning:ro");
    }

    [Theory]
    [InlineData("src/DistributedQuery.Api/Dockerfile", "DistributedQuery.Api.dll", "8080", false)]
    [InlineData("src/DistributedQuery.Coordinator/Dockerfile", "DistributedQuery.Coordinator.dll", "8080", false)]
    [InlineData("src/DistributedQuery.Worker/Dockerfile", "DistributedQuery.Worker.dll", "5100 5101", true)]
    public void Dockerfiles_UseMultiStagePublishAndNonRootRuntime(
        string dockerfilePath,
        string entryPoint,
        string exposedPorts,
        bool usesEntrypointScript)
    {
        var dockerfile = ReadRepositoryFile(dockerfilePath);

        dockerfile.Should().Contain("FROM mcr.microsoft.com/dotnet/sdk:8.0 AS restore");
        dockerfile.Should().Contain("FROM restore AS publish");
        dockerfile.Should().Contain("FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime");
        dockerfile.Should().Contain("--configuration Release");
        dockerfile.Should().Contain("/p:UseAppHost=false");
        dockerfile.Should().Contain($"EXPOSE {exposedPorts}");

        if (usesEntrypointScript)
        {
            dockerfile.Should().Contain("docker-entrypoint.sh");
            dockerfile.Should().Contain("ENTRYPOINT [\"/app/docker-entrypoint.sh\"]");

            var entrypointScript = ReadRepositoryFile("src/DistributedQuery.Worker/docker-entrypoint.sh");
            entrypointScript.Should().Contain($"dotnet /app/{entryPoint}");
            entrypointScript.Should().Contain("runuser -u app");
        }
        else
        {
            dockerfile.Should().Contain("USER app");
            dockerfile.Should().Contain($"ENTRYPOINT [\"dotnet\", \"{entryPoint}\"]");
        }
    }

    [Fact]
    public void PrometheusConfig_ScrapesApplicationAndInfrastructureMetrics()
    {
        var prometheus = ReadRepositoryFile("infra/prometheus.yml");

        prometheus.Should().Contain("job_name: dqee-api");
        prometheus.Should().Contain("api:8080");
        prometheus.Should().Contain("job_name: dqee-coordinator");
        prometheus.Should().Contain("coordinator:8080");
        prometheus.Should().Contain("job_name: dqee-worker");
        prometheus.Should().Contain("worker:5101");
        prometheus.Should().Contain("job_name: rabbitmq");
        prometheus.Should().Contain("rabbitmq:15692");
    }

    [Fact]
    public void DockerCompose_ConfiguresRuntimeBoundariesWithoutCrossServiceReferences()
    {
        var compose = ReadRepositoryFile("docker-compose.yml");

        compose.Should().Contain("CoordinatorClient__BaseUrl: http://coordinator:8080");
        compose.Should().Contain("Consul__Address: http://consul:8500");
        compose.Should().Contain("Redis__ConnectionString: redis:6379,abortConnect=false");
        compose.Should().Contain("RabbitMq__Host: rabbitmq");
        compose.Should().Contain("OpenTelemetry__OtlpEndpoint: http://jaeger:4317");
        compose.Should().Contain("Worker__Address: worker");
        compose.Should().Contain("Worker__Consul__Enabled: \"true\"");
    }

    [Fact]
    public void DockerIgnore_ExcludesBuildOutputAndLocalState()
    {
        var dockerIgnore = ReadRepositoryFile(".dockerignore");

        dockerIgnore.Should().Contain("**/bin");
        dockerIgnore.Should().Contain("**/obj");
        dockerIgnore.Should().Contain("**/TestResults");
        dockerIgnore.Should().Contain("**/.git");
        dockerIgnore.Should().Contain("**/*.log");
    }

    private static string ReadRepositoryFile(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, relativePath));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DistributedQuery.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
