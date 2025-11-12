using Payment.Application.DTOs;

namespace Payment.Application.Services;

/// <summary>
/// Implementation of metadata enrichment service.
/// Single Responsibility: Only enriches metadata with request information.
/// </summary>
public class MetadataEnrichmentService : IMetadataEnrichmentService
{
    public Dictionary<string, string> EnrichMetadata(CreatePaymentDto request, Dictionary<string, string>? existingMetadata = null)
    {
        var metadata = existingMetadata ?? new Dictionary<string, string>();

        // Add customer information if provided
        if (!string.IsNullOrEmpty(request.CustomerEmail))
        {
            metadata["customer_email"] = request.CustomerEmail;
        }

        if (!string.IsNullOrEmpty(request.CustomerPhone))
        {
            metadata["customer_phone"] = request.CustomerPhone;
        }

        // Add callback URL if provided
        if (!string.IsNullOrEmpty(request.CallbackUrl))
        {
            metadata["callback_url"] = request.CallbackUrl;
        }

        // Add project and request tracking information
        metadata["project_code"] = request.ProjectCode;
        metadata["request_id"] = request.RequestId.ToString();

        return metadata;
    }
}

