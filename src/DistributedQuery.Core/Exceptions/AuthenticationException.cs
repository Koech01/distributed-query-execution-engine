namespace DistributedQuery.Core.Exceptions;

public enum AuthenticationFailureKind
{
    Generic,
    EmailAlreadyExists,
    AccountLocked,
    AccountDeleted
}

public sealed class AuthenticationException : Exception
{
    public AuthenticationFailureKind Kind { get; }

    public DateTimeOffset? LockedUntilUtc { get; }

    public AuthenticationException(string message, AuthenticationFailureKind kind = AuthenticationFailureKind.Generic)
        : base(message)
    {
        Kind = kind;
    }

    public AuthenticationException(
        string message,
        AuthenticationFailureKind kind,
        DateTimeOffset lockedUntilUtc)
        : base(message)
    {
        Kind = kind;
        LockedUntilUtc = lockedUntilUtc;
    }

    public AuthenticationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Kind = AuthenticationFailureKind.Generic;
    }

    public static AuthenticationException InvalidCredentials() =>
        new("Invalid email address or password.");

    public static AuthenticationException EmailAlreadyExists() =>
        new(
            "An account with this email address already exists.",
            AuthenticationFailureKind.EmailAlreadyExists);

    public static AuthenticationException AccountLocked(DateTimeOffset lockedUntilUtc)
    {
        var remainingMinutes = Math.Max(1, (int)Math.Ceiling((lockedUntilUtc - DateTimeOffset.UtcNow).TotalMinutes));
        return new AuthenticationException(
            $"Account is temporarily locked. Try again in {remainingMinutes} minute(s).",
            AuthenticationFailureKind.AccountLocked,
            lockedUntilUtc);
    }

    public static AuthenticationException AccountDeleted() =>
        new("This account has been removed.", AuthenticationFailureKind.AccountDeleted);
}
