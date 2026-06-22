using System.Text;
using DistributedQuery.Api.Middleware;
using DistributedQuery.Api.Options;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace DistributedQuery.UnitTests.Api;

public sealed class RequestValidationMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_RejectsTooManyParameters()
    {
        var options = Options.Create(new ApiOptions { MaxParameters = 1 });
        var invoked = false;
        var middleware = new RequestValidationMiddleware(_ =>
        {
            invoked = true;
            return Task.CompletedTask;
        }, options);

        var context = CreatePostQueriesContext("""
            {
              "sql": "SELECT 1",
              "parameters": [
                { "name": "@a", "type": "int", "value": "1" },
                { "name": "@b", "type": "int", "value": "2" }
              ]
            }
            """);

        await middleware.InvokeAsync(context);

        invoked.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task InvokeAsync_AllowsValidRequest()
    {
        var options = Options.Create(new ApiOptions());
        var invoked = false;
        var middleware = new RequestValidationMiddleware(_ =>
        {
            invoked = true;
            return Task.CompletedTask;
        }, options);

        var context = CreatePostQueriesContext("""{ "sql": "SELECT 1" }""");

        await middleware.InvokeAsync(context);

        invoked.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_AllowsStringFailurePolicy()
    {
        var options = Options.Create(new ApiOptions());
        var invoked = false;
        var middleware = new RequestValidationMiddleware(_ =>
        {
            invoked = true;
            return Task.CompletedTask;
        }, options);

        var context = CreatePostQueriesContext("""
            {
              "sql": "SELECT 1",
              "failurePolicy": "BestEffort"
            }
            """);

        await middleware.InvokeAsync(context);

        invoked.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_RejectsExplicitZeroMaxNodes()
    {
        var options = Options.Create(new ApiOptions { MaxNodes = 10 });
        var invoked = false;
        var middleware = new RequestValidationMiddleware(_ =>
        {
            invoked = true;
            return Task.CompletedTask;
        }, options);

        var context = CreatePostQueriesContext("""{ "sql": "SELECT 1", "maxNodes": 0 }""");

        await middleware.InvokeAsync(context);

        invoked.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task InvokeAsync_RejectsMaxNodesAboveCeiling()
    {
        var options = Options.Create(new ApiOptions { MaxNodes = 10 });
        var invoked = false;
        var middleware = new RequestValidationMiddleware(_ =>
        {
            invoked = true;
            return Task.CompletedTask;
        }, options);

        var context = CreatePostQueriesContext("""{ "sql": "SELECT 1", "maxNodes": 11 }""");

        await middleware.InvokeAsync(context);

        invoked.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    private static DefaultHttpContext CreatePostQueriesContext(string json)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/queries";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        context.Response.Body = new MemoryStream();
        return context;
    }
}
