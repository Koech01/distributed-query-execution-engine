using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Grpc.Core;
using Grpc.Core.Interceptors;
using DistributedQuery.Infrastructure.Grpc;

namespace DistributedQuery.UnitTests.Infrastructure;

public class TracingInterceptorsTests
{
    [Fact]
    public void TracingClientInterceptor_InsertsTraceContextHeaders_ForServerStreamingCall()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;

        var interceptor = new TracingClientInterceptor();
        Metadata? capturedHeaders = null;
        Activity? currentActivity = null;

        var method = new Method<string, string>(MethodType.ServerStreaming, "queryexecution.QueryExecution", "ExecuteSubQuery", Marshallers.StringMarshaller, Marshallers.StringMarshaller);
        var context = new ClientInterceptorContext<string, string>(method, "https://localhost:5100", new CallOptions());

        AsyncServerStreamingCall<string> Continuation(string request, ClientInterceptorContext<string, string> ctx)
        {
            capturedHeaders = ctx.Options.Headers;
            currentActivity = Activity.Current;
            var reader = new TestAsyncStreamReader<string>(Array.Empty<string>());
            return new AsyncServerStreamingCall<string>(reader, Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => new Metadata(), () => { });
        }

        var response = interceptor.AsyncServerStreamingCall("payload", context, Continuation);

        response.ResponseStream.Should().NotBeNull();
        capturedHeaders.Should().NotBeNull();
        capturedHeaders!.Should().ContainSingle(header => header.Key == "traceparent");
        currentActivity.Should().NotBeNull();
        currentActivity!.Tags.Should().Contain(pair => pair.Key == "rpc.system" && pair.Value == "grpc");
        currentActivity.Tags.Should().Contain(pair => pair.Key == "rpc.method" && pair.Value == "ExecuteSubQuery");
    }

    [Fact]
    public async Task TracingClientInterceptor_InsertsTraceContextHeaders_ForUnaryCall()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;

        var interceptor = new TracingClientInterceptor();
        Metadata? capturedHeaders = null;
        Activity? currentActivity = null;

        var method = new Method<string, string>(MethodType.Unary, "queryexecution.QueryExecution", "Check", Marshallers.StringMarshaller, Marshallers.StringMarshaller);
        var context = new ClientInterceptorContext<string, string>(method, "https://localhost:5100", new CallOptions());

        AsyncUnaryCall<string> Continuation(string request, ClientInterceptorContext<string, string> ctx)
        {
            capturedHeaders = ctx.Options.Headers;
            currentActivity = Activity.Current;
            return new AsyncUnaryCall<string>(Task.FromResult("ok"), Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => new Metadata(), () => { });
        }

        var call = interceptor.AsyncUnaryCall("payload", context, Continuation);
        var responseValue = await call.ResponseAsync;
        responseValue.Should().Be("ok");

        capturedHeaders.Should().NotBeNull();
        capturedHeaders!.Should().ContainSingle(header => header.Key == "traceparent");
        currentActivity.Should().NotBeNull();
        currentActivity!.Tags.Should().Contain(pair => pair.Key == "rpc.system" && pair.Value == "grpc");
        currentActivity.Tags.Should().Contain(pair => pair.Key == "rpc.method" && pair.Value == "Check");
    }

    [Fact]
    public async Task TracingServerInterceptor_UsesIncomingTraceContext_ForUnaryHandler()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;

        using var parentActivity = new Activity("root");
        parentActivity.SetIdFormat(ActivityIdFormat.W3C);
        parentActivity.Start();

        var traceparent = parentActivity.Id!;
        var requestHeaders = new Metadata { { "traceparent", traceparent } };
        var context = new TestServerCallContext("/queryexecution.QueryExecution/ExecuteSubQuery", requestHeaders);

        var interceptor = new TracingServerInterceptor();
        Activity? currentActivity = null;

        Task<string> Continuation(string request, ServerCallContext ctx)
        {
            currentActivity = Activity.Current;
            return Task.FromResult("ok");
        }

        var result = await interceptor.UnaryServerHandler("payload", context, Continuation);

        result.Should().Be("ok");
        currentActivity.Should().NotBeNull();
        currentActivity!.Tags.Should().Contain(pair => pair.Key == "rpc.system" && pair.Value == "grpc");
        currentActivity.Tags.Should().Contain(pair => pair.Key == "rpc.method" && pair.Value == "/queryexecution.QueryExecution/ExecuteSubQuery");
    }

    private sealed class TestAsyncStreamReader<T> : IAsyncStreamReader<T>
    {
        private readonly IEnumerator<T> _enumerator;

        public TestAsyncStreamReader(IEnumerable<T> items)
        {
            _enumerator = items.GetEnumerator();
        }

        public T Current => _enumerator.Current;

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            return Task.FromResult(_enumerator.MoveNext());
        }
    }

    private sealed class TestServerCallContext : ServerCallContext
    {
        private readonly Metadata _requestHeaders;

        private readonly string _method;

        public TestServerCallContext(string method, Metadata requestHeaders)
        {
            _method = method;
            _requestHeaders = requestHeaders;
        }

        protected override string MethodCore => _method;
        protected override string HostCore => "localhost";
        protected override string PeerCore => "127.0.0.1";
        protected override DateTime DeadlineCore => DateTime.UtcNow.AddMinutes(1);
        protected override Metadata RequestHeadersCore => _requestHeaders;
        protected override CancellationToken CancellationTokenCore => CancellationToken.None;
        protected override Metadata ResponseTrailersCore => new Metadata();
        protected override Status StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore => new AuthContext("test", new Dictionary<string, List<AuthProperty>>());
        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) => throw new NotImplementedException();
        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    }
}
