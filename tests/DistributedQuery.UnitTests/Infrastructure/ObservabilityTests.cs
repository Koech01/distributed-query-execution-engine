using System.Diagnostics;
using System.Diagnostics.Metrics;
using DistributedQuery.Infrastructure.Observability;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace DistributedQuery.UnitTests.Infrastructure;

public class ObservabilityTests
{
    [Fact]
    public void TraceContextPropagator_InjectHttpHeaders_AddsW3CHeaders()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;

        using var activity = new Activity("test");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();

        using var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/internal/v1/queries/execute");
        TraceContextPropagator.InjectHttpHeaders(request, activity);

        request.Headers.GetValues("traceparent").Should().ContainSingle().Which.Should().Be(activity.Id);
    }

    [Fact]
    public void DqeeMetrics_ApiRequestsTotal_IncrementsViaMeterListener()
    {
        long? observed = null;

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == DqeeMetrics.Api.Name &&
                    instrument.Name == "dqee_api_requests_total")
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name == "dqee_api_requests_total")
            {
                observed = measurement;
            }
        });

        listener.Start();

        DqeeMetrics.ApiRequestsTotal.Add(
            1,
            new KeyValuePair<string, object?>("method", "POST"),
            new KeyValuePair<string, object?>("status_code", "200"));

        listener.RecordObservableInstruments();
        observed.Should().Be(1);
    }

    [Fact]
    public async Task ApiMetricsMiddleware_RecordsRequestMetrics()
    {
        long? requestCount = null;

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Name == "dqee_api_requests_total")
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            if (instrument.Name == "dqee_api_requests_total")
            {
                requestCount = measurement;
            }
        });

        listener.Start();

        RequestDelegate next = context =>
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        };

        var middleware = new ApiMetricsMiddleware(next);
        var context = new DefaultHttpContext
        {
            Request = { Method = "GET", Path = "/health/live" }
        };

        await middleware.InvokeAsync(context);

        listener.RecordObservableInstruments();
        requestCount.Should().Be(1);
        context.Response.StatusCode.Should().Be(StatusCodes.Status204NoContent);
    }
}
