using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Payment.Domain.Interfaces;
using Payment.Infrastructure.Caching;
using Payment.Infrastructure.Data;
using Payment.Infrastructure.Monitoring;
using Payment.Infrastructure.Monitoring.Channels;
using Payment.Infrastructure.Providers;
using Payment.Infrastructure.Repositories;
using Payment.Infrastructure.Secrets;
using Payment.Infrastructure.Security;

namespace Payment.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register secrets management
        services.AddSecretsManagement(configuration);

        // Get connection string - try secrets manager first, then fallback to configuration
        var connectionString = GetConnectionString(configuration, services);

        // Register data encryption service (PCI DSS compliance)
        services.AddSingleton<IDataEncryptionService, DataEncryptionService>();

        // Register DbContext with encryption service injection
        services.AddDbContext<PaymentDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString);
            // Note: Encryption service is injected via OnModelCreating, but EF Core doesn't support
            // service injection in value converters directly. We use a static setter pattern.
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IIdempotentRequestRepository, IdempotentRequestRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>(); // Audit Logging #7
        services.AddScoped<Domain.Interfaces.IAuditLogQueryService, Auditing.AuditLogQueryService>(); // Audit Log Querying
        services.AddScoped<IOutboxMessageRepository, Repositories.OutboxMessageRepository>(); // Outbox Pattern #12
        services.AddScoped<IWebhookDeliveryRepository, Repositories.WebhookDeliveryRepository>(); // Webhook Retry Mechanism #20
        services.AddScoped<IPaymentReportRepository, PaymentReportRepository>();
        services.AddSingleton<IMetricsRecorder, Metrics.MetricsRecorder>(); // Metrics recording
        
        // Register reporting services
        services.AddScoped<IExchangeRateService, Services.ExchangeRateService>();
        services.AddScoped<IForecastingService, Services.ForecastingService>();
        services.AddScoped<IAnomalyDetectionService, Services.AnomalyDetectionService>();
        
        // Register fraud detection service (Fraud Detection #22)
        services.AddHttpClient("FraudDetection", client =>
        {
            var baseUrl = configuration["FraudDetection:BaseUrl"];
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                client.BaseAddress = new Uri(baseUrl);
            }
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });
        services.AddScoped<Domain.Interfaces.IFraudDetectionService, Services.FraudDetectionService>();
        
        // Register settlement service (Multi-Currency Settlement #21)
        services.AddScoped<Domain.Interfaces.ISettlementService, Application.Services.SettlementService>();
        
        // Register 3D Secure service (3D Secure Support #23)
        services.AddScoped<Domain.Interfaces.IThreeDSecureService, Services.ThreeDSecureService>();
        
        // Register incident response services
        services.AddScoped<Domain.Interfaces.ICircuitBreakerService, Services.CircuitBreakerService>();
        services.AddScoped<Domain.Interfaces.INotificationService, Services.NotificationService>();
        
        // Register security services
        services.AddScoped<Domain.Interfaces.IAuditLogger, Security.AuditLogger>();
        services.AddScoped<Domain.Interfaces.ICredentialRevocationService, Security.CredentialRevocationService>();
        services.AddScoped<Domain.Interfaces.ISecurityNotificationService, Security.SecurityNotificationService>();

        // Register security monitoring integrations
        RegisterSecurityMonitoringIntegrations(services, configuration);

        // Register alerting service and channels
        RegisterAlertingServices(services, configuration);
        
        // Register event publishing and storage services
        services.AddScoped<IEventPublisher, Messaging.EventPublisher>();
        services.AddScoped<IStorageService, Storage.StorageService>();
        services.AddScoped<IReportBuilderService, Reporting.ReportBuilderService>();

        // Register caching service (Caching Strategy #9)
        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
            });
            services.AddSingleton<ICacheService, RedisCacheService>();
        }
        else
        {
            // Fallback to memory cache for development
            services.AddMemoryCache();
            services.AddSingleton<ICacheService, MemoryCacheService>();
        }

        // Register webhook signature validator
        services.AddScoped<ICallbackSignatureValidator, CallbackSignatureValidator>();

        // Register webhook delivery service (Webhook Retry Mechanism #20)
        services.AddHttpClient("WebhookDelivery", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "Payment-Service/1.0");
        });
        services.AddScoped<Domain.Interfaces.IWebhookDeliveryService, Services.WebhookDeliveryService>();

        // Register HTTP clients for payment providers
        services.AddHttpClient<ZainCashPaymentProvider>(client =>
        {
            // ZainCash uses different base URLs for test and production
            // The actual URL is determined in the provider based on IsTestMode
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        services.AddHttpClient<AsiaHawalaPaymentProvider>(client =>
        {
            // TODO: Configure AsiaHawala API base URL from configuration
            client.BaseAddress = new Uri(configuration["PaymentProviders:AsiaHawala:BaseUrl"] ?? "https://api.asiahawala.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient<StripePaymentProvider>(client =>
        {
            client.BaseAddress = new Uri(configuration["PaymentProviders:Stripe:BaseUrl"] ?? "https://api.stripe.com/v1/");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        services.AddHttpClient<FibPaymentProvider>(client =>
        {
            // FIB uses different base URLs for test and production
            // The actual URL is determined in the provider based on IsTestMode
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        services.AddHttpClient<SquarePaymentProvider>(client =>
        {
            client.BaseAddress = new Uri(configuration["PaymentProviders:Square:BaseUrl"] ?? "https://connect.squareup.com/v2/");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        services.AddHttpClient<HelcimPaymentProvider>(client =>
        {
            client.BaseAddress = new Uri(configuration["PaymentProviders:Helcim:BaseUrl"] ?? "https://api.helcim.com/v2/");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        services.AddHttpClient<AmazonPaymentProvider>(client =>
        {
            client.BaseAddress = new Uri(configuration["PaymentProviders:Amazon:BaseUrl"] ?? "https://paymentservices.amazon.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        services.AddHttpClient<TelrPaymentProvider>(client =>
        {
            client.BaseAddress = new Uri(configuration["PaymentProviders:Telr:BaseUrl"] ?? "https://secure.telr.com/gateway/");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        services.AddHttpClient<CheckoutPaymentProvider>(client =>
        {
            client.BaseAddress = new Uri(configuration["PaymentProviders:Checkout:BaseUrl"] ?? "https://api.checkout.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        services.AddHttpClient<VerifonePaymentProvider>(client =>
        {
            client.BaseAddress = new Uri(configuration["PaymentProviders:Verifone:BaseUrl"] ?? "https://api.2checkout.com/rest/6.0/");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        services.AddHttpClient<PaytabsPaymentProvider>(client =>
        {
            client.BaseAddress = new Uri(configuration["PaymentProviders:Paytabs:BaseUrl"] ?? "https://secure.paytabs.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        services.AddHttpClient<TapPaymentProvider>(client =>
        {
            client.BaseAddress = new Uri(configuration["PaymentProviders:Tap:BaseUrl"] ?? "https://api.tap.company/v2/");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        services.AddHttpClient<TapToPayPaymentProvider>(client =>
        {
            client.BaseAddress = new Uri(configuration["PaymentProviders:TapToPay:BaseUrl"] ?? "https://api.tap.company/v2/");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        // Register payment providers with resilience decorator (Resilience Patterns #8)
        // Using decorator pattern to wrap providers with resilience policies
        RegisterPaymentProviderWithResilience<ZainCashPaymentProvider>(services);
        RegisterPaymentProviderWithResilience<AsiaHawalaPaymentProvider>(services);
        RegisterPaymentProviderWithResilience<StripePaymentProvider>(services);
        RegisterPaymentProviderWithResilience<FibPaymentProvider>(services);
        RegisterPaymentProviderWithResilience<SquarePaymentProvider>(services);
        RegisterPaymentProviderWithResilience<HelcimPaymentProvider>(services);
        RegisterPaymentProviderWithResilience<AmazonPaymentProvider>(services);
        RegisterPaymentProviderWithResilience<TelrPaymentProvider>(services);
        RegisterPaymentProviderWithResilience<CheckoutPaymentProvider>(services);
        RegisterPaymentProviderWithResilience<VerifonePaymentProvider>(services);
        RegisterPaymentProviderWithResilience<PaytabsPaymentProvider>(services);
        RegisterPaymentProviderWithResilience<TapPaymentProvider>(services);
        RegisterPaymentProviderWithResilience<TapToPayPaymentProvider>(services);

        // Register state machine factory and service (State Machine #18)
        services.AddScoped<IPaymentStateMachineFactory, StateMachines.PaymentStateMachineFactory>();
        services.AddScoped<Domain.Services.IPaymentStateService, Services.PaymentStateService>();

        // Register background services
        services.AddHostedService<BackgroundServices.IdempotencyCleanupService>();
        services.AddHostedService<BackgroundServices.OutboxProcessorService>(); // Outbox Pattern #12
        services.AddHostedService<BackgroundServices.WebhookRetryService>(); // Webhook Retry Mechanism #20
        services.AddHostedService<BackgroundServices.AuditLogRetentionService>(); // Audit Log Retention Policy

        return services;
    }

    private static string GetConnectionString(IConfiguration configuration, IServiceCollection services)
    {
        // Try to get connection string from secrets manager if available
        // For now, use configuration as fallback
        // In production, connection string should come from secrets manager
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        
        if (string.IsNullOrEmpty(connectionString))
        {
            // Try to get from secrets manager (requires service provider, so we'll do this later)
            // For now, throw exception
            throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        return connectionString;
    }

    // Helper method to register payment providers with resilience decorator (Resilience Patterns #8)
    // Note: We register providers directly as IPaymentProvider with decorator wrapper
    // The PaymentProviderFactory uses GetServices<IPaymentProvider> to find providers by name
    private static void RegisterPaymentProviderWithResilience<TProvider>(IServiceCollection services)
        where TProvider : class, IPaymentProvider
    {
        // Register the concrete provider (needed for dependency injection)
        services.AddScoped<TProvider>();
        
        // Register as IPaymentProvider with resilience decorator wrapper
        // This ensures PaymentProviderFactory can find it via GetServices<IPaymentProvider>
        services.AddScoped<IPaymentProvider>(serviceProvider =>
        {
            // Get the concrete provider instance
            var provider = serviceProvider.GetRequiredService<TProvider>();
            var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ResilientPaymentProviderDecorator>>();
            
            // Wrap with resilience decorator
            return new ResilientPaymentProviderDecorator(provider, logger);
        });
    }

    // Register alerting services and channels
    private static void RegisterAlertingServices(IServiceCollection services, IConfiguration configuration)
    {
        // Configure alert rules
        services.Configure<AlertRulesConfiguration>(
            configuration.GetSection("AlertRules"));

        // Register alert channels
        services.AddHttpClient("SlackAlert");
        services.AddHttpClient("PagerDutyAlert");
        services.AddHttpClient("SmsAlert");

        // Configure and register email channel
        services.Configure<EmailAlertChannelOptions>(
            configuration.GetSection("Alerting:Email"));
        services.AddSingleton<IAlertChannel, EmailAlertChannel>();

        // Configure and register Slack channel
        services.Configure<SlackAlertChannelOptions>(
            configuration.GetSection("Alerting:Slack"));
        services.AddSingleton<IAlertChannel, SlackAlertChannel>();

        // Configure and register PagerDuty channel
        services.Configure<PagerDutyAlertChannelOptions>(
            configuration.GetSection("Alerting:PagerDuty"));
        services.AddSingleton<IAlertChannel, PagerDutyAlertChannel>();

        // Configure and register SMS channel
        services.Configure<SmsAlertChannelOptions>(
            configuration.GetSection("Alerting:SMS"));
        services.AddSingleton<IAlertChannel, SmsAlertChannel>();

        // Register alerting service
        services.AddScoped<Domain.Interfaces.IAlertingService, Monitoring.AlertingService>();

        // Register Kubernetes secret rotation service (optional - requires K8s client)
        // services.AddScoped<Security.IKubernetesSecretRotationService, Security.KubernetesSecretRotationService>();
    }

    // Register security monitoring integrations
    private static void RegisterSecurityMonitoringIntegrations(IServiceCollection services, IConfiguration configuration)
    {
        // Register Generic SIEM integration if configured
        var siemOptions = configuration.GetSection(Security.Integrations.GenericSiemOptions.SectionName);
        if (siemOptions.Exists() && !string.IsNullOrWhiteSpace(siemOptions["Endpoint"]))
        {
            services.Configure<Security.Integrations.GenericSiemOptions>(siemOptions);
            services.AddHttpClient<Security.Integrations.GenericSiemIntegration>(client =>
            {
                var endpoint = siemOptions["Endpoint"];
                if (!string.IsNullOrWhiteSpace(endpoint))
                {
                    client.BaseAddress = new Uri(endpoint);
                }
                var apiKey = siemOptions["ApiKey"];
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
                }
            });
            services.AddScoped<Domain.Interfaces.ISecurityMonitoringIntegration, Security.Integrations.GenericSiemIntegration>();
        }

        // Register AWS GuardDuty integration if configured
        var guardDutyOptions = configuration.GetSection(Security.Integrations.AwsGuardDutyOptions.SectionName);
        if (guardDutyOptions.Exists() && !string.IsNullOrWhiteSpace(guardDutyOptions["DetectorId"]))
        {
            services.Configure<Security.Integrations.AwsGuardDutyOptions>(guardDutyOptions);
            // Note: Requires AWSSDK.GuardDuty package
            // services.AddAWSService<Amazon.GuardDuty.IAmazonGuardDuty>();
            // services.AddScoped<Domain.Interfaces.ISecurityMonitoringIntegration, Security.Integrations.AwsGuardDutyIntegration>();
        }

        // Register SecurityMonitoringService as the main integration facade
        // It coordinates all registered integrations
        services.AddScoped<Domain.Interfaces.ISecurityMonitoringIntegration>(serviceProvider =>
        {
            var registeredIntegrations = serviceProvider.GetServices<Domain.Interfaces.ISecurityMonitoringIntegration>().ToList();
            if (!registeredIntegrations.Any())
            {
                // Return a no-op implementation if no integrations are configured
                return new Security.Integrations.NoOpSecurityMonitoringIntegration(
                    serviceProvider.GetRequiredService<ILogger<Security.Integrations.NoOpSecurityMonitoringIntegration>>());
            }

            return new Security.Integrations.SecurityMonitoringService(
                registeredIntegrations,
                serviceProvider.GetRequiredService<ILogger<Security.Integrations.SecurityMonitoringService>>());
        });
    }
}

