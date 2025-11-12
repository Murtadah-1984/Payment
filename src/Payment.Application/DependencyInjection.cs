using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Payment.Application.Behaviors;
using Payment.Application.Services;
using Payment.Application.Validators;
using Payment.Domain.Interfaces;
using System.Reflection;

namespace Payment.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        
        // Register MediatR pipeline behaviors (Audit Logging #7)
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditingBehavior<,>));
        
        // Register application services (following SOLID principles - each service has a single responsibility)
        services.AddScoped<ISplitPaymentService, SplitPaymentService>();
        services.AddScoped<IPaymentProviderFactory, PaymentProviderFactory>();
        services.AddScoped<IMetadataEnrichmentService, MetadataEnrichmentService>();
        services.AddScoped<IIdempotencyService, IdempotencyService>();
        services.AddScoped<IRequestHashService, RequestHashService>();
        services.AddScoped<IPaymentFactory, PaymentFactory>();
        services.AddScoped<IPaymentProcessingService, PaymentProcessingService>();
        services.AddScoped<IPaymentStatusUpdater, PaymentStatusUpdater>();
        services.AddScoped<IPaymentOrchestrator, PaymentOrchestrator>();
        services.AddScoped<IPaymentReportingService, PaymentReportingService>();
        services.AddScoped<IPaymentReportingScheduler, PaymentReportingScheduler>();
        services.AddScoped<IPaymentWebhookNotifier, PaymentWebhookNotifier>(); // Webhook Retry Mechanism #20
        services.AddScoped<IIncidentResponseService, IncidentResponseService>();
        services.AddScoped<IRefundService, RefundService>();
        services.AddScoped<ISecurityIncidentResponseService, SecurityIncidentResponseService>();
        services.AddScoped<Domain.Interfaces.IIncidentReportGenerator, IncidentReportGenerator>();
        
        return services;
    }
}

