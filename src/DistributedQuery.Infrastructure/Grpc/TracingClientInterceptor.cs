using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace DistributedQuery.Infrastructure.Grpc;

public sealed class TracingClientInterceptor : Interceptor
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Infrastructure.Grpc.TracingClientInterceptor");

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var headers = CreateHeaders(context.Options.Headers);
        var activity = StartActivity(context.Method, request);
        InjectTraceContext(headers, activity);
        var updatedContext = new ClientInterceptorContext<TRequest, TResponse>(
            context.Method, context.Host, context.Options.WithHeaders(headers));

        var call = base.AsyncUnaryCall(request, updatedContext, continuation);
        return new AsyncUnaryCall<TResponse>(
            ObserveAsync(call.ResponseAsync, activity),
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            () =>
            {
                activity?.Dispose();
                call.Dispose();
            });
    }

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var headers = CreateHeaders(context.Options.Headers);
        var activity = StartActivity(context.Method, request);
        InjectTraceContext(headers, activity);
        var updatedContext = new ClientInterceptorContext<TRequest, TResponse>(
            context.Method, context.Host, context.Options.WithHeaders(headers));

        var call = base.AsyncServerStreamingCall(request, updatedContext, continuation);
        return new AsyncServerStreamingCall<TResponse>(
            call.ResponseStream,
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            () =>
            {
                activity?.Dispose();
                call.Dispose();
            });
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
    }

    private static Metadata CreateHeaders(Metadata? existing)
    {
        var headers = new Metadata();
        if (existing is not null)
        {
            foreach (var entry in existing)
            {
                headers.Add(entry.Key, entry.Value ?? string.Empty);
            }
        }

        return headers;
    }

    private static Activity StartActivity<TRequest, TResponse>(Method<TRequest, TResponse> method, TRequest request)
    {
        var activityName = $"grpc.client:{method.ServiceName}/{method.Name}";
        var activity = ActivitySource.StartActivity(activityName, ActivityKind.Client);
        if (activity is null)
        {
            activity = new Activity(activityName);
            activity.SetIdFormat(ActivityIdFormat.W3C);
            activity.Start();
        }

        activity.SetTag("rpc.system", "grpc");
        activity.SetTag("rpc.service", method.ServiceName);
        activity.SetTag("rpc.method", method.Name);

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

    private static void InjectTraceContext(Metadata headers, Activity activity)
    {
        if (!string.IsNullOrEmpty(activity.Id))
        {
            headers.Add("traceparent", activity.Id);
        }

        if (!string.IsNullOrWhiteSpace(activity.TraceStateString))
        {
            headers.Add("tracestate", activity.TraceStateString);
        }
    }
}
