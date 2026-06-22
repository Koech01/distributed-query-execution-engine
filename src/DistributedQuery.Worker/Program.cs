using DistributedQuery.Infrastructure.Grpc;
using DistributedQuery.Infrastructure.Observability;
using DistributedQuery.Worker;
using DistributedQuery.Worker.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWorkerServices(builder.Configuration);
builder.Services.AddDistributedQueryObservability(builder.Configuration, ObservabilityHostRole.Worker);

builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<TracingServerInterceptor>();
    options.Interceptors.Add<MetricsServerInterceptor>();
});

var workerOptions = builder.Configuration.GetSection(WorkerOptions.SectionName).Get<WorkerOptions>()
    ?? new WorkerOptions();

builder.WebHost.UseShutdownTimeout(TimeSpan.FromSeconds(
    Math.Max(1, workerOptions.Execution.DrainTimeoutSeconds) + 5));

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(workerOptions.GrpcPort, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });

    options.ListenAnyIP(workerOptions.HealthPort, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1;
    });
});

var app = builder.Build();

app.UseTraceContextLogging();
app.MapGrpcService<WorkerGrpcService>();
app.MapWorkerHealthEndpoints();
app.MapPrometheusScrapingEndpoint();

app.Run();
