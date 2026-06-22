using DistributedQuery.Core.Models;
using DistributedQuery.Infrastructure.Auth;
using FluentAssertions;

namespace DistributedQuery.UnitTests.Infrastructure;

public sealed class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void HashPassword_ThenVerifyPassword_ReturnsTrue()
    {
        var hash = _hasher.HashPassword("correct-horse-battery-staple");

        _hasher.VerifyPassword("correct-horse-battery-staple", hash).Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WithWrongPassword_ReturnsFalse()
    {
        var hash = _hasher.HashPassword("correct-horse-battery-staple");

        _hasher.VerifyPassword("wrong-password", hash).Should().BeFalse();
    }
}

public sealed class UserAccountTests
{
    [Fact]
    public void WithExternalLogin_AddsProviderWithoutDuplicates()
    {
        var user = UserAccount.CreateEmailUser(
            "user-1",
            "reader@example.com",
            "Reader",
            "hash",
            ["query:read"],
            DateTimeOffset.UtcNow);

        var linked = user.WithExternalLogin(new ExternalLogin("Google", "google-subject"), DateTimeOffset.UtcNow);
        var relinked = linked.WithExternalLogin(new ExternalLogin("Google", "google-subject"), DateTimeOffset.UtcNow);

        linked.ExternalLogins.Should().ContainSingle(login => login.Provider == "Google");
        relinked.ExternalLogins.Should().HaveCount(1);
    }

    [Fact]
    public void WithFailedLoginAttempt_LocksAccountWhenThresholdReached()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var user = UserAccount.CreateEmailUser(
            "user-1",
            "reader@example.com",
            "Reader",
            "hash",
            ["query:read"],
            timestamp);

        UserAccount updated = user;
        for (var attempt = 0; attempt < 4; attempt++)
        {
            updated = updated.WithFailedLoginAttempt(5, TimeSpan.FromMinutes(15), timestamp);
        }

        updated.FailedLoginAttempts.Should().Be(4);
        updated.IsLockedOut.Should().BeFalse();

        updated = updated.WithFailedLoginAttempt(5, TimeSpan.FromMinutes(15), timestamp);

        updated.FailedLoginAttempts.Should().Be(5);
        updated.IsLockedOut.Should().BeTrue();
        updated.LockedUntilUtc.Should().NotBeNull();
    }

    [Fact]
    public void SoftDelete_SetsDeletedAt()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var user = UserAccount.CreateEmailUser(
            "user-1",
            "reader@example.com",
            "Reader",
            "hash",
            ["query:read"],
            timestamp);

        var deleted = user.SoftDelete(timestamp);

        deleted.IsDeleted.Should().BeTrue();
        deleted.DeletedAt.Should().Be(timestamp);
    }

    [Fact]
    public void WithDisplayName_UpdatesDisplayNameAndTimestamp()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var user = UserAccount.CreateEmailUser(
            "user-1",
            "reader@example.com",
            "Reader",
            "hash",
            ["query:read"],
            timestamp);

        var updated = user.WithDisplayName("Updated Reader", timestamp.AddMinutes(1));

        updated.DisplayName.Should().Be("Updated Reader");
        updated.UpdatedAt.Should().BeAfter(timestamp);
    }

    [Fact]
    public void WithEmail_UpdatesEmailAndTimestamp()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var user = UserAccount.CreateEmailUser(
            "user-1",
            "reader@example.com",
            "Reader",
            "hash",
            ["query:read"],
            timestamp);

        var updated = user.WithEmail("updated@example.com", timestamp.AddMinutes(1));

        updated.Email.Should().Be("updated@example.com");
        updated.UpdatedAt.Should().BeAfter(timestamp);
    }

    [Fact]
    public void UserProfile_FromAccount_MapsLinkedProvidersAndPasswordFlag()
    {
        var user = UserAccount.CreateOAuthUser(
            "user-1",
            "reader@example.com",
            "Reader",
            AuthProviderKind.Google,
            "google-subject",
            ["query:read"],
            DateTimeOffset.UtcNow);

        var profile = UserProfile.FromAccount(user);

        profile.HasPasswordLogin.Should().BeFalse();
        profile.LinkedProviders.Should().ContainSingle("Google");
        profile.Email.Should().Be("reader@example.com");
    }
}
