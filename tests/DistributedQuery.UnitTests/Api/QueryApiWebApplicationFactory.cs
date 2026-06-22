using DistributedQuery.Api;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace DistributedQuery.UnitTests.Api;

public sealed class QueryApiWebApplicationFactory : WebApplicationFactory<Program>
{
    public IQueryCoordinatorClient CoordinatorClient { get; } = Substitute.For<IQueryCoordinatorClient>();

    public IQueryCache QueryCache { get; } = Substitute.For<IQueryCache>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IQueryCoordinatorClient>();
            services.AddSingleton(CoordinatorClient);

            services.RemoveAll<IQueryCache>();
            services.AddSingleton(QueryCache);
        });

        builder.UseSetting("Authentication:Enabled", "false");
        builder.UseSetting("Redis:ConnectionString", "localhost:6379,abortConnect=false,connectTimeout=100");
    }
}
