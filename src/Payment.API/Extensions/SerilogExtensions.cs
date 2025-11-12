using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;

namespace Payment.API.Extensions;

/// <summary>
/// Serilog enricher that adds OpenTelemetry trace identifiers (TraceId and SpanId) to log events.
/// This enables correlation between logs and distributed traces.
/// </summary>
public class TraceIdentifierEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            // Add TraceId (W3C format)
            var traceId = activity.TraceId.ToString();
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", traceId));

            // Add SpanId (W3C format)
            var spanId = activity.SpanId.ToString();
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SpanId", spanId));

            // Add ParentSpanId if available
            if (activity.ParentSpanId != default)
            {
                var parentSpanId = activity.ParentSpanId.ToString();
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ParentSpanId", parentSpanId));
            }
        }
    }
}

