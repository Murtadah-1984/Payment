using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
using OpenTelemetry.Exporter.Otlp;

namespace Payment.API.Extensions;

/// <summary>
/// Extension methods for configuring OpenTelemetry with Jaeger and Zipkin exporters.
/// Follows Clean Architecture by separating infrastructure concerns from application startup.
/// </summary>
public static class OpenTelemetryExtensions
{
    /// <summary>
    /// Adds OpenTelemetry with distributed tracing and metrics for production environments.
    /// Supports Jaeger, Zipkin, and OTLP exporters with proper resource attributes and sampling.
    /// </summary>
    public static IServiceCollection AddPaymentOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Get configuration values with defaults
        var useConsoleExporter = configuration.GetValue<bool>("OpenTelemetry:UseConsoleExporter", false);
        var jaegerHost = configuration["OpenTelemetry:Jaeger:Host"];
        var jaegerPort = configuration.GetValue<int>("OpenTelemetry:Jaeger:Port", 6831);
        var zipkinEndpoint = configuration["OpenTelemetry:Zipkin:Endpoint"];
        var otlpEndpoint = configuration["OpenTelemetry:Otlp:Endpoint"];
        var serviceName = configuration["OpenTelemetry:ServiceName"] ?? "Payment.API";
        var serviceVersion = configuration["OpenTelemetry:ServiceVersion"] ?? "1.0.0";
        var environmentName = environment.EnvironmentName;
        
        // Sampling configuration for production
        var samplingRatio = configuration.GetValue<double>("OpenTelemetry:SamplingRatio", 1.0);
        if (environment.IsProduction() && samplingRatio > 0.1)
        {
            // Cap sampling at 10% in production to reduce overhead
            samplingRatio = 0.1;
        }

        // Build resource attributes for better trace identification
        var resourceBuilder = ResourceBuilder
            .CreateDefault()
            .AddService(
                serviceName: serviceName,
                serviceVersion: serviceVersion)
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = environmentName,
                ["service.namespace"] = configuration["OpenTelemetry:ServiceNamespace"] ?? "payment",
                ["k8s.pod.name"] = Environment.GetEnvironmentVariable("HOSTNAME") ?? "unknown",
                ["k8s.namespace"] = Environment.GetEnvironmentVariable("KUBERNETES_NAMESPACE") ?? "payment"
            });

        services.AddOpenTelemetry()
            .WithTracing(tracerProviderBuilder =>
            {
                tracerProviderBuilder
                    .SetResourceBuilder(resourceBuilder)
                    .SetSampler(new TraceIdRatioBasedSampler(samplingRatio))
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        // Filter out health checks and metrics endpoints from tracing
                        options.Filter = context => !context.Request.Path.StartsWithSegments("/health") &&
                                                   !context.Request.Path.StartsWithSegments("/metrics") &&
                                                   !context.Request.Path.StartsWithSegments("/ready") &&
                                                   !context.Request.Path.StartsWithSegments("/live");
                        
                        // Enrich spans with HTTP request details
                        options.EnrichWithHttpRequest = (activity, request) =>
                        {
                            activity.SetTag("http.request.id", request.HttpContext.TraceIdentifier);
                            activity.SetTag("http.request.method", request.Method);
                            activity.SetTag("http.request.path", request.Path.Value);
                            activity.SetTag("http.request.query_string", request.QueryString.Value);
                            activity.SetTag("http.request.user_agent", request.Headers["User-Agent"].ToString());
                            
                            // Add client IP if available
                            var clientIp = request.HttpContext.Connection.RemoteIpAddress?.ToString();
                            if (!string.IsNullOrEmpty(clientIp))
                            {
                                activity.SetTag("http.request.client_ip", clientIp);
                            }
                        };
                        
                        // Enrich spans with HTTP response details
                        options.EnrichWithHttpResponse = (activity, response) =>
                        {
                            activity.SetTag("http.response.status_code", response.StatusCode);
                            activity.SetTag("http.response.content_length", response.ContentLength ?? 0);
                        };
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.FilterHttpRequestMessage = request =>
                        {
                            // Filter out health check requests
                            return !request.RequestUri?.AbsolutePath.Contains("/health") ?? true;
                        };
                        
                        // Enrich spans with HTTP client request details
                        options.EnrichWithHttpRequestMessage = (activity, request) =>
                        {
                            activity.SetTag("http.client.request.method", request.Method?.ToString());
                            activity.SetTag("http.client.request.uri", request.RequestUri?.ToString());
                            activity.SetTag("http.client.request.scheme", request.RequestUri?.Scheme);
                            activity.SetTag("http.client.request.host", request.RequestUri?.Host);
                        };
                        
                        // Enrich spans with HTTP client response details
                        options.EnrichWithHttpResponseMessage = (activity, response) =>
                        {
                            activity.SetTag("http.client.response.status_code", (int)response.StatusCode);
                            activity.SetTag("http.client.response.reason_phrase", response.ReasonPhrase);
                        };
                    })
                    .AddEntityFrameworkCoreInstrumentation(options =>
                    {
                        options.SetDbStatementForText = true;
                        options.EnrichWithIDbCommand = (activity, command) =>
                        {
                            activity.SetTag("db.command.type", command.CommandType.ToString());
                            activity.SetTag("db.command.timeout", command.CommandTimeout);
                        };
                    })
                    // Add custom ActivitySources for application and infrastructure layers
                    .AddSource("Payment.Application")
                    .AddSource("Payment.Infrastructure");

                // Add exporters based on configuration and environment
                
                // Console exporter for development
                if (environment.IsDevelopment() || useConsoleExporter)
                {
                    tracerProviderBuilder.AddConsoleExporter();
                }

                // Jaeger exporter (UDP agent)
                if (!string.IsNullOrEmpty(jaegerHost))
                {
                    tracerProviderBuilder.AddJaegerExporter(options =>
                    {
                        options.AgentHost = jaegerHost;
                        options.AgentPort = jaegerPort;
                        options.ExportProcessorType = ExportProcessorType.Batch;
                        options.BatchExportProcessorOptions = new BatchExportProcessorOptions<Activity>
                        {
                            MaxQueueSize = 2048,
                            ScheduledDelayMilliseconds = 5000,
                            ExporterTimeoutMilliseconds = 30000,
                            MaxExportBatchSize = 512
                        };
                    });
                }

                // Zipkin exporter (HTTP)
                if (!string.IsNullOrEmpty(zipkinEndpoint))
                {
                    try
                    {
                        var zipkinUri = new Uri(zipkinEndpoint);
                        tracerProviderBuilder.AddZipkinExporter(options =>
                        {
                            options.Endpoint = zipkinUri;
                            options.ExportProcessorType = ExportProcessorType.Batch;
                            options.BatchExportProcessorOptions = new BatchExportProcessorOptions<Activity>
                            {
                                MaxQueueSize = 2048,
                                ScheduledDelayMilliseconds = 5000,
                                ExporterTimeoutMilliseconds = 30000,
                                MaxExportBatchSize = 512
                            };
                        });
                    }
                    catch (UriFormatException)
                    {
                        // Log error but don't fail startup - tracing is non-critical
                        System.Diagnostics.Debug.WriteLine($"Invalid Zipkin endpoint format: {zipkinEndpoint}");
                    }
                }

                // OTLP exporter (modern standard, supports both gRPC and HTTP)
                if (!string.IsNullOrEmpty(otlpEndpoint))
                {
                    try
                    {
                        var otlpUri = new Uri(otlpEndpoint);
                        tracerProviderBuilder.AddOtlpExporter(options =>
                        {
                            options.Endpoint = otlpUri;
                            options.Protocol = OtlpExportProtocol.Grpc;
                        });
                    }
                    catch (UriFormatException)
                    {
                        // Log error but don't fail startup - tracing is non-critical
                        System.Diagnostics.Debug.WriteLine($"Invalid OTLP endpoint format: {otlpEndpoint}");
                    }
                }
            })
            .WithMetrics(metricsProviderBuilder =>
            {
                metricsProviderBuilder
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.Filter = context => !context.Request.Path.StartsWithSegments("/health") &&
                                                    !context.Request.Path.StartsWithSegments("/metrics");
                    })
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddPrometheusExporter();
            });

        return services;
    }
}

