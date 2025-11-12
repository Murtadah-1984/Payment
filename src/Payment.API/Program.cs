using AspNetCoreRateLimit;
using HotChocolate.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.FeatureManagement;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Payment.API.Extensions;
using Payment.API.GraphQL.Mutations;
using Payment.API.GraphQL.Queries;
using Payment.API.Middleware;
using Payment.Application;
using Payment.Infrastructure;
using Payment.Infrastructure.Data;
using Prometheus;
using Serilog;
using System.Diagnostics;
using System.IO;

// Check for --generate-report command line argument BEFORE building the app
// This allows early exit for CronJob execution
var commandLineArgs = Environment.GetCommandLineArgs();
if (commandLineArgs.Contains("--generate-report"))
{
    // Build minimal services for report generation
    var reportBuilder = WebApplication.CreateBuilder(commandLineArgs);
    reportBuilder.Configuration.AddSecretsManagement(reportBuilder.Configuration);
    
    // Configure Serilog for command-line execution
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(reportBuilder.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/payment-.txt", rollingInterval: RollingInterval.Day)
        .CreateLogger();
    
    reportBuilder.Host.UseSerilog();
    reportBuilder.Services.AddApplication();
    reportBuilder.Services.AddInfrastructure(reportBuilder.Configuration);
    
    var reportApp = reportBuilder.Build();
    
    using var scope = reportApp.Services.CreateScope();
    var scheduler = scope.ServiceProvider.GetRequiredService<Payment.Application.Services.IPaymentReportingScheduler>();
    
    // Generate report for previous month (runs on 1st of month, so generate for previous month)
    var reportMonth = DateTime.UtcNow.AddMonths(-1);
    
    Log.Information("Generating monthly report for {Year}-{Month:D2} via command line", 
        reportMonth.Year, reportMonth.Month);
    
    try
    {
        var (reportId, reportUrl, pdfUrl, csvUrl) = await scheduler.GenerateMonthlyReportAsync(reportMonth);
        Log.Information("Report generated successfully. ReportId: {ReportId}, ReportUrl: {ReportUrl}", 
            reportId, reportUrl);
        Log.CloseAndFlush();
        return;
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Failed to generate monthly report");
        Log.CloseAndFlush();
        throw;
    }
}

var builder = WebApplication.CreateBuilder(args);

// Configure secrets management BEFORE building configuration
// This allows secrets to be loaded into configuration
builder.Configuration.AddSecretsManagement(builder.Configuration);

// Configure Serilog with correlation IDs and OpenTelemetry trace context
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.With(new Payment.API.Extensions.TraceIdentifierEnricher()) // Adds TraceId and SpanId from Activity.Current
    .Enrich.WithProperty("ServiceName", "Payment.API")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{TraceId}] [{SpanId}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/payment-.txt", rollingInterval: RollingInterval.Day, 
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{TraceId}] [{SpanId}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// Add HttpContextAccessor for audit logging (Audit Logging #7)
builder.Services.AddHttpContextAccessor();

// Add services
builder.Services.AddControllers();

// API Versioning (API Versioning #13)
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new Microsoft.AspNetCore.Mvc.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = Microsoft.AspNetCore.Mvc.Versioning.ApiVersionReader.Combine(
        new Microsoft.AspNetCore.Mvc.Versioning.UrlSegmentApiVersionReader(),
        new Microsoft.AspNetCore.Mvc.Versioning.HeaderApiVersionReader("X-Version"),
        new Microsoft.AspNetCore.Mvc.Versioning.QueryStringApiVersionReader("version"));
});

builder.Services.AddVersionedApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Configure Swagger for API versioning
    // Note: BuildServiceProvider is required here to access IApiVersionDescriptionProvider during Swagger configuration
    // This is a known pattern for Swagger with API versioning and is safe in this context
#pragma warning disable ASP0000 // Calling 'BuildServiceProvider' from application code
    var apiVersionDescriptionProvider = builder.Services.BuildServiceProvider()
        .GetRequiredService<Microsoft.AspNetCore.Mvc.ApiExplorer.IApiVersionDescriptionProvider>();
#pragma warning restore ASP0000

    foreach (var description in apiVersionDescriptionProvider.ApiVersionDescriptions)
    {
        c.SwaggerDoc(description.GroupName, new OpenApiInfo
        {
            Title = "Payment Microservice API",
            Version = description.ApiVersion.ToString(),
            Description = "A production-ready Payment microservice built with Clean Architecture",
            Contact = new OpenApiContact
            {
                Name = "Payment API Support",
                Email = "support@payment.example.com"
            }
        });
    }

    // Include XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    // JWT Authentication in Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// JWT Authentication - External Identity Microservice
// Validates tokens against the Identity Microservice's JWKS endpoint
var authority = builder.Configuration["Auth:Authority"] 
    ?? throw new InvalidOperationException("Auth:Authority not configured");
var audience = builder.Configuration["Auth:Audience"] 
    ?? throw new InvalidOperationException("Auth:Audience not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = authority;
    options.Audience = audience;
    options.RequireHttpsMetadata = true;
    // Token validation is performed against the Identity Microservice's JWKS endpoint
    // No local secret key is required - tokens are validated using public keys from the authority
});

// Authorization Policies for fine-grained control
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PaymentsWrite", policy =>
        policy.RequireClaim("scope", "payment.write"));
    
    options.AddPolicy("PaymentsRead", policy =>
        policy.RequireClaim("scope", "payment.read"));
    
    options.AddPolicy("PaymentsAdmin", policy =>
        policy.RequireClaim("scope", "payment.admin"));
    
    // Admin policies for incident management
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireClaim("scope", "payment.admin"));
    
    options.AddPolicy("SecurityAdminOnly", policy =>
        policy.RequireClaim("scope", "payment.admin")
              .RequireClaim("role", "SecurityAdmin"));
});

// Enhanced Health Checks (Health Checks Enhancement #14)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");

var healthChecksBuilder = builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionString: connectionString ?? throw new InvalidOperationException("DefaultConnection not configured"),
        name: "postgresql",
        tags: new[] { "db", "ready" })
    .AddDbContextCheck<PaymentDbContext>(tags: new[] { "db", "ready" });

// Add Redis health check if configured
if (!string.IsNullOrEmpty(redisConnectionString))
{
    healthChecksBuilder.AddRedis(
        redisConnectionString: redisConnectionString,
        name: "redis",
        tags: new[] { "cache", "ready" });
}

// Add custom payment provider health check
healthChecksBuilder.AddCheck<Payment.API.HealthChecks.PaymentProviderHealthCheck>(
    "payment-providers",
    tags: new[] { "provider", "live" });

// Add disk space health check
healthChecksBuilder.AddCheck<Payment.API.HealthChecks.DiskSpaceHealthCheck>(
    "disk-space",
    tags: new[] { "infrastructure", "live" });

// Rate Limiting (Rate Limiting & DDoS Protection #6)
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// Application and Infrastructure layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// OpenTelemetry for Observability & Distributed Tracing (Observability #15)
// Configured via extension method following Clean Architecture principles
builder.Services.AddPaymentOpenTelemetry(builder.Configuration, builder.Environment);


// Feature Management (Feature Flags #17)
builder.Services.AddFeatureManagement(builder.Configuration.GetSection("FeatureManagement"));

// GraphQL Support (GraphQL Support #19)
// Follows Clean Architecture - GraphQL layer delegates to Application layer via MediatR
builder.Services
    .AddGraphQLServer()
    .AddQueryType<PaymentQueries>()
    .AddMutationType<PaymentMutations>()
    .ModifyRequestOptions(options =>
    {
        options.IncludeExceptionDetails = builder.Environment.IsDevelopment();
    });

// Initialize encryption service for metadata converter (after service registration)
// This is a workaround for EF Core's limitation with DI in value converters
builder.Services.AddOptions();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Initialize encryption service for metadata converter
// This must be done after building the app to access the service provider
using (var scope = app.Services.CreateScope())
{
    var encryptionService = scope.ServiceProvider.GetRequiredService<Payment.Domain.Interfaces.IDataEncryptionService>();
    Payment.Infrastructure.Data.Converters.MetadataEncryptionConverter.SetEncryptionService(encryptionService);
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// IP Whitelisting for admin endpoints (must be before rate limiting)
app.UseMiddleware<IpWhitelistMiddleware>();

// Rate Limiting (Rate Limiting & DDoS Protection #6) - must be early in pipeline
app.UseIpRateLimiting();

// Request sanitization middleware (adds security headers - must be early in pipeline)
app.UseMiddleware<RequestSanitizationMiddleware>();

app.UseCors("AllowAll");

// Webhook signature validation middleware (must be before authentication for callback endpoints)
app.UseMiddleware<WebhookSignatureValidationMiddleware>();

app.UseAuthentication();

// JWT token blacklist middleware (must be after authentication to extract token)
app.UseMiddleware<Payment.Infrastructure.Security.JwtTokenBlacklistMiddleware>();

app.UseAuthorization();

// Metrics tracking middleware (must be after authentication/authorization to capture failures)
app.UseMiddleware<AuthenticationMetricsMiddleware>();
app.UseMiddleware<RateLimitMetricsMiddleware>();

// Admin request/response logging middleware (must be after authentication to capture user info)
app.UseMiddleware<AdminRequestLoggingMiddleware>();

app.MapControllers();

// GraphQL endpoint (GraphQL Support #19)
var graphQLOptions = new GraphQLServerOptions();
if (builder.Environment.IsDevelopment() || builder.Environment.IsStaging())
{
    graphQLOptions.Tool.Enable = true;
}
app.MapGraphQL().WithOptions(graphQLOptions);

// Prometheus metrics (exposed on /metrics endpoint - should be behind internal network in production)
app.UseMetricServer();
app.UseHttpMetrics();

// Enhanced Health check endpoints for Kubernetes (Health Checks Enhancement #14)
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
    // ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse // Commented out due to namespace issue
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
    // ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse // Commented out due to namespace issue
});

// Legacy endpoints for backward compatibility
app.MapHealthChecks("/health");
app.MapHealthChecks("/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

// Database migration (for development - use migrations in production)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    try
    {
        dbContext.Database.Migrate();
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred while migrating the database");
    }
}

try
{
    Log.Information("Payment Microservice starting up");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Payment Microservice failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// Make Program class accessible for WebApplicationFactory in tests
public partial class Program { }

