using DistributedQuery.Api.Middleware;
using DistributedQuery.Core.Exceptions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace DistributedQuery.UnitTests.Api;

public sealed class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_MapsQueryParseExceptionTo400()
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new QueryParseException("bad sql", "hash", ["error"]),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task InvokeAsync_MapsInsufficientNodesTo503()
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InsufficientNodesException(4, 1),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task InvokeAsync_MapsAuthenticationExceptionTo401()
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new AuthenticationException("Invalid email address or password."),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_MapsEmailAlreadyExistsTo409()
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw AuthenticationException.EmailAlreadyExists(),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }
}
