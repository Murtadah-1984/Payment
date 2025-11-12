using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Payment.Domain.Entities;
using Payment.Domain.Interfaces;
using System.Text.Json;

namespace Payment.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior for audit logging (Audit Logging #7).
/// Logs all mutating operations (commands) for compliance and security tracking.
/// Follows Single Responsibility Principle - only responsible for audit logging.
/// </summary>
public class AuditingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditingBehavior<TRequest, TResponse>> _logger;
    private readonly IUnitOfWork _unitOfWork;

    public AuditingBehavior(
        IAuditLogRepository auditLogRepository,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditingBehavior<TRequest, TResponse>> logger,
        IUnitOfWork unitOfWork)
    {
        _auditLogRepository = auditLogRepository;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Only audit commands (mutating operations), not queries
        var isCommand = request.GetType().Name.EndsWith("Command", StringComparison.OrdinalIgnoreCase);
        
        if (!isCommand)
        {
            return await next();
        }

        var response = await next();

        try
        {
            // Extract audit information
            var userId = _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value 
                        ?? _httpContextAccessor.HttpContext?.User?.FindFirst("nameid")?.Value
                        ?? "System";
            
            var action = request.GetType().Name;
            var entityType = ExtractEntityType(request, response);
            var entityId = ExtractEntityId(request, response);
            var ipAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
            var userAgent = _httpContextAccessor.HttpContext?.Request?.Headers["User-Agent"].ToString();
            
            var changes = ExtractChanges(request, response);

            // Create audit log
            var auditLog = new AuditLog(
                id: Guid.NewGuid(),
                userId: userId,
                action: action,
                entityType: entityType,
                entityId: entityId,
                ipAddress: ipAddress,
                userAgent: userAgent,
                changes: changes);

            await _auditLogRepository.AddAsync(auditLog, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Audit log created for action: {Action}, entity: {EntityType}, id: {EntityId}", 
                action, entityType, entityId);
        }
        catch (Exception ex)
        {
            // Don't fail the request if audit logging fails
            _logger.LogError(ex, "Failed to create audit log for request: {RequestType}", typeof(TRequest).Name);
        }

        return response;
    }

    private string ExtractEntityType(TRequest request, TResponse response)
    {
        // Try to extract entity type from response
        if (response != null)
        {
            var responseType = response.GetType();
            if (responseType.Name.Contains("Payment", StringComparison.OrdinalIgnoreCase))
            {
                return "Payment";
            }
        }

        // Fallback to request type
        var requestType = request.GetType().Name;
        if (requestType.Contains("Payment", StringComparison.OrdinalIgnoreCase))
        {
            return "Payment";
        }

        return "Unknown";
    }

    private Guid ExtractEntityId(TRequest request, TResponse response)
    {
        // Try to extract ID from response
        if (response != null)
        {
            var responseType = response.GetType();
            var idProperty = responseType.GetProperty("Id");
            if (idProperty != null && idProperty.GetValue(response) is Guid id)
            {
                return id;
            }
        }

        // Try to extract ID from request
        var requestType = request.GetType();
        var requestIdProperty = requestType.GetProperty("PaymentId") 
                               ?? requestType.GetProperty("Id");
        if (requestIdProperty != null && requestIdProperty.GetValue(request) is Guid requestId)
        {
            return requestId;
        }

        return Guid.Empty;
    }

    private Dictionary<string, object> ExtractChanges(TRequest request, TResponse response)
    {
        var changes = new Dictionary<string, object>();

        try
        {
            // Extract relevant fields from request
            var requestType = request.GetType();
            var requestProperties = requestType.GetProperties()
                .Where(p => p.Name != "CancellationToken" && p.GetValue(request) != null)
                .Take(10); // Limit to prevent large audit logs

            foreach (var prop in requestProperties)
            {
                var value = prop.GetValue(request);
                if (value != null)
                {
                    // Sanitize sensitive data
                    var propName = prop.Name;
                    if (propName.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
                        propName.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
                        propName.Contains("Token", StringComparison.OrdinalIgnoreCase))
                    {
                        changes[propName] = "***REDACTED***";
                    }
                    else
                    {
                        changes[propName] = value.ToString() ?? string.Empty;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract changes for audit log");
        }

        return changes;
    }
}

