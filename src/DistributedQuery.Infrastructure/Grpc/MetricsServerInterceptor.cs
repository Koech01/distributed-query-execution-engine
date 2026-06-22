using System.Diagnostics;
using DistributedQuery.Infrastructure.Observability;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace DistributedQuery.Infrastructure.Grpc;

public sealed class MetricsServerInterceptor : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var stopwatch = Stopwatch.StartNew();
        var status = "OK";

        try
        {
            return await continuation(request, context).ConfigureAwait(false);
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
            RecordServerDuration(context.Method, status, stopwatch.Elapsed.TotalSeconds);
        }
    }

    public override Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation) =>
        ObserveAsync(continuation(request, responseStream, context), context.Method);

    private static async Task ObserveAsync(Task task, string method)
    {
        var stopwatch = Stopwatch.StartNew();
        var status = "OK";

        try
        {
            await task.ConfigureAwait(false);
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
            RecordServerDuration(method, status, stopwatch.Elapsed.TotalSeconds);
        }
    }

    private static void RecordServerDuration(string method, string status, double seconds)
    {
        var methodName = ExtractMethodName(method);
        DqeeMetrics.GrpcServerDurationSeconds.Record(
            seconds,
            new KeyValuePair<string, object?>("method", methodName),
            new KeyValuePair<string, object?>("status", status));
    }

    private static string ExtractMethodName(string fullMethod)
    {
        var separator = fullMethod.LastIndexOf('/');
        return separator < 0 ? fullMethod : fullMethod[(separator + 1)..];
    }
}
