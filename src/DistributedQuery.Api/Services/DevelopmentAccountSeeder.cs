using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using DistributedQuery.Infrastructure.Auth;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DistributedQuery.Api.Services;

public sealed class DevelopmentAccountSeeder : IHostedService
{
    private readonly IHostEnvironment _environment;
    private readonly AuthSeedOptions _seedOptions;
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<DevelopmentAccountSeeder> _logger;

    public DevelopmentAccountSeeder(
        IHostEnvironment environment,
        IOptions<AuthSeedOptions> seedOptions,
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ILogger<DevelopmentAccountSeeder> logger)
    {
        _environment = environment;
        _seedOptions = seedOptions.Value;
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment() || !_seedOptions.Enabled)
        {
            return;
        }

        await SeedAccountAsync(_seedOptions.Admin, cancellationToken).ConfigureAwait(false);
        await SeedAccountAsync(_seedOptions.User, cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedAccountAsync(SeedAccountOptions account, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(account.Email) || string.IsNullOrWhiteSpace(account.Password))
        {
            return;
        }

        var existing = await _userRepository.FindByEmailAsync(account.Email, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            _logger.LogDebug("Development seed account {Email} already exists.", account.Email);
            return;
        }

        var timestamp = DateTimeOffset.UtcNow;
        var user = UserAccount.CreateEmailUser(
            Guid.NewGuid().ToString("N"),
            account.Email.Trim().ToLowerInvariant(),
            account.DisplayName.Trim(),
            _passwordHasher.HashPassword(account.Password),
            account.Scopes,
            timestamp);

        await _userRepository.CreateAsync(user, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Seeded development account {Email} with scopes {Scopes}.",
            account.Email,
            string.Join(", ", account.Scopes));
    }
}
