using DistributedQuery.Api.Services;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using DistributedQuery.Infrastructure.Auth;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DistributedQuery.UnitTests.Api;

public sealed class DevelopmentAccountSeederTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();

    [Fact]
    public async Task StartAsync_WhenDevelopmentAndEnabled_CreatesMissingAccounts()
    {
        _passwordHasher.HashPassword(Arg.Any<string>()).Returns("hashed");
        _userRepository.FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((UserAccount?)null);

        var seeder = CreateSeeder(isDevelopment: true, enabled: true);

        await seeder.StartAsync(CancellationToken.None);

        await _userRepository.Received(2).CreateAsync(Arg.Any<UserAccount>(), Arg.Any<CancellationToken>());
        await _userRepository.Received(1).CreateAsync(
            Arg.Is<UserAccount>(account =>
                account.Email == "admin@example.com" &&
                account.Scopes.SequenceEqual(new[] { "query:read", "query:admin" })),
            Arg.Any<CancellationToken>());
        await _userRepository.Received(1).CreateAsync(
            Arg.Is<UserAccount>(account =>
                account.Email == "user@example.com" &&
                account.Scopes.SequenceEqual(new[] { "query:read" })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenAccountAlreadyExists_SkipsCreation()
    {
        var existing = UserAccount.CreateEmailUser(
            "existing",
            "admin@example.com",
            "Admin Account",
            "hashed",
            ["query:read", "query:admin"],
            DateTimeOffset.UtcNow);

        _userRepository.FindByEmailAsync("admin@example.com", Arg.Any<CancellationToken>())
            .Returns(existing);
        _userRepository.FindByEmailAsync("user@example.com", Arg.Any<CancellationToken>())
            .Returns((UserAccount?)null);
        _passwordHasher.HashPassword(Arg.Any<string>()).Returns("hashed");

        var seeder = CreateSeeder(isDevelopment: true, enabled: true);

        await seeder.StartAsync(CancellationToken.None);

        await _userRepository.Received(1).CreateAsync(Arg.Any<UserAccount>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenNotDevelopment_DoesNotSeed()
    {
        var seeder = CreateSeeder(isDevelopment: false, enabled: true);

        await seeder.StartAsync(CancellationToken.None);

        await _userRepository.DidNotReceive().CreateAsync(Arg.Any<UserAccount>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenDisabled_DoesNotSeed()
    {
        var seeder = CreateSeeder(isDevelopment: true, enabled: false);

        await seeder.StartAsync(CancellationToken.None);

        await _userRepository.DidNotReceive().CreateAsync(Arg.Any<UserAccount>(), Arg.Any<CancellationToken>());
    }

    private DevelopmentAccountSeeder CreateSeeder(bool isDevelopment, bool enabled)
    {
        var environment = new HostEnvironment(isDevelopment ? Environments.Development : Environments.Production);
        var options = Options.Create(new AuthSeedOptions
        {
            Enabled = enabled,
            Admin = new SeedAccountOptions
            {
                Email = "admin@example.com",
                Password = "ChangeMe-Admin-12",
                DisplayName = "Admin Account",
                Scopes = ["query:read", "query:admin"],
            },
            User = new SeedAccountOptions
            {
                Email = "user@example.com",
                Password = "ChangeMe-User-12",
                DisplayName = "Standard User",
                Scopes = ["query:read"],
            },
        });

        return new DevelopmentAccountSeeder(
            environment,
            options,
            _userRepository,
            _passwordHasher,
            NullLogger<DevelopmentAccountSeeder>.Instance);
    }

    private sealed class HostEnvironment : IHostEnvironment
    {
        public HostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "DistributedQuery.Api";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
