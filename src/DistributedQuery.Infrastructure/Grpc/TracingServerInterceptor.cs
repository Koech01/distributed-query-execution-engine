using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace DistributedQuery.Infrastructure.Grpc;

public sealed class TracingServerInterceptor : Interceptor
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Infrastructure.Grpc.TracingServerInterceptor");

    public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var activity = StartActivity(context, request);
        return ObserveAsync(continuation(request, context), activity);
    }

    public override Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        var activity = StartActivity(context, request);
        return ObserveAsync(continuation(request, responseStream, context), activity);
    }

    private static async Task ObserveAsync(Task task, Activity? activity)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            activity?.Dispose();
        }
    }

    private static async Task<TResponse> ObserveAsync<TResponse>(Task<TResponse> task, Activity? activity)
    {
        try
        {
            return await task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            activity?.Dispose();
        }
    }

    private static Activity StartActivity<TRequest>(ServerCallContext context, TRequest request)
    {
        var parentContext = ExtractPropagationContext(context.RequestHeaders);

        var activityName = $"grpc.server:{context.Method}";
        var activity = ActivitySource.StartActivity(activityName, ActivityKind.Server, parentContext);
        if (activity is null)
        {
            activity = new Activity(activityName);
            activity.SetIdFormat(ActivityIdFormat.W3C);
            activity.Start();
        }

        activity.SetTag("rpc.system", "grpc");
        activity.SetTag("rpc.method", context.Method);

        if (request is SubQueryRequest subQuery)
        {
            if (!string.IsNullOrWhiteSpace(subQuery.ParentQueryId))
            {
                activity.SetTag("query.id", subQuery.ParentQueryId);
            }

            if (!string.IsNullOrWhiteSpace(subQuery.SubQueryId))
            {
                activity.SetTag("sub_query_id", subQuery.SubQueryId);
            }

            activity.SetTag("shard.index", subQuery.ShardIndex);
            activity.SetTag("shard.total", subQuery.TotalShards);
        }

        return activity;
    }

    private static ActivityContext ExtractPropagationContext(Metadata headers)
    {
        var traceParent = headers.FirstOrDefault(entry => string.Equals(entry.Key, "traceparent", StringComparison.OrdinalIgnoreCase))?.Value;
        var traceState = headers.FirstOrDefault(entry => string.Equals(entry.Key, "tracestate", StringComparison.OrdinalIgnoreCase))?.Value;

        if (!string.IsNullOrWhiteSpace(traceParent) && ActivityContext.TryParse(traceParent, traceState, out var context))
        {
            return context;
        }

        return default;
    }
}
