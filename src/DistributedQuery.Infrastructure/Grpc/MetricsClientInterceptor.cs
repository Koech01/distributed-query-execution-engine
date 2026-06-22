using System.Diagnostics;
using DistributedQuery.Infrastructure.Observability;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace DistributedQuery.Infrastructure.Grpc;

public sealed class MetricsClientInterceptor : Interceptor
{
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var call = base.AsyncUnaryCall(request, context, continuation);
        return new AsyncUnaryCall<TResponse>(
            ObserveAsync(call.ResponseAsync, context.Method.Name),
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var call = base.AsyncServerStreamingCall(request, context, continuation);
        var stopwatch = Stopwatch.StartNew();
        var method = context.Method.Name;

        return new AsyncServerStreamingCall<TResponse>(
            call.ResponseStream,
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            () =>
            {
                var status = "OK";
                try
                {
                    status = call.GetStatus().StatusCode.ToString();
                }
                catch
                {
                    status = "error";
                }
                finally
                {
                    stopwatch.Stop();
                    RecordClientDuration(method, status, stopwatch.Elapsed.TotalSeconds);
                    call.Dispose();
                }
            });
    }

    private static async Task<TResponse> ObserveAsync<TResponse>(Task<TResponse> task, string method)
    {
        var stopwatch = Stopwatch.StartNew();
        var status = "OK";

        try
        {
            return await task.ConfigureAwait(false);
        }
        catch (RpcException rpcException)
        {
            status = rpcException.StatusCode.ToString();
            throw;
        }
        catch
        {
            status = "error";
            throw;
        }
        finally
        {
            stopwatch.Stop();
            RecordClientDuration(method, status, stopwatch.Elapsed.TotalSeconds);
        }
    }

    private static void RecordClientDuration(string method, string status, double seconds)
    {
        DqeeMetrics.GrpcClientDurationSeconds.Record(
            seconds,
            new KeyValuePair<string, object?>("method", method),
            new KeyValuePair<string, object?>("status", status));
    }
}
