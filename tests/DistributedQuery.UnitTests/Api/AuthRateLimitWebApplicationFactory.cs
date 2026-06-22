using DistributedQuery.Api;
using DistributedQuery.Core.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace DistributedQuery.UnitTests.Api;

public sealed class AuthRateLimitWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUserRepository>();
            services.AddSingleton(Substitute.For<IUserRepository>());
        });

        builder.UseSetting("Authentication:Enabled", "false");
        builder.UseSetting("Authentication:Email:Enabled", "true");
        builder.UseSetting("Authentication:RateLimit:PermitLimitPerIp", "3");
        builder.UseSetting("Authentication:RateLimit:WindowSeconds", "60");
        builder.UseSetting("Redis:ConnectionString", "localhost:6379,abortConnect=false,connectTimeout=100");
    }
}
