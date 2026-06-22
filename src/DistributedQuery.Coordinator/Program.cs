using DistributedQuery.Coordinator;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Infrastructure;
using DistributedQuery.Infrastructure.Grpc;
using DistributedQuery.Infrastructure.Observability;
using DistributedQuery.QueryParser.Parsing;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<CoordinatorOptions>()
    .Bind(builder.Configuration.GetSection(CoordinatorOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<ShardMapOptions>()
    .Bind(builder.Configuration.GetSection("ShardMap"))
    .ValidateOnStart();

builder.Services.AddInfrastructure(builder.Configuration, InfrastructureHostRole.Coordinator);
builder.Services.AddSingleton<IQueryPlanner, SqlQueryParser>();
builder.Services.AddSingleton<QueryPlanningService>();
builder.Services.AddSingleton<WorkerRouter>();
builder.Services.AddSingleton<FanOutService>();
builder.Services.AddSingleton<IResultMerger, ResultAggregator>();
builder.Services.AddSingleton<CoordinatorService>();
builder.Services.AddSingleton<ActiveQueryRegistry>();
builder.Services.AddSingleton<CoordinatorAdminService>();
builder.Services.AddHttpClient(nameof(WorkerHealthProbe), client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddSingleton<WorkerHealthProbe>();
builder.Services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<CoordinatorService>());
builder.Services.AddSingleton<QueryBackgroundDispatcher>();
builder.Services.AddHostedService<QueryBackgroundProcessor>();
builder.Services.AddDistributedQueryObservability(builder.Configuration, ObservabilityHostRole.Coordinator);

var app = builder.Build();

app.UseTraceContextLogging();
app.MapCoordinatorHealthEndpoints();
app.MapCoordinatorQueryEndpoints();
app.MapCoordinatorAdminEndpoints();
app.MapPrometheusScrapingEndpoint();

app.Services.GetRequiredService<IOptions<CoordinatorOptions>>();

app.Run();
