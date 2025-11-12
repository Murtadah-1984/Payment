using Payment.Application.DTOs;

namespace Payment.Application.Services;

/// <summary>
/// Service responsible for enriching payment metadata with additional information.
/// Follows Single Responsibility Principle - only handles metadata enrichment.
/// </summary>
public interface IMetadataEnrichmentService
{
    /// <summary>
    /// Enriches metadata dictionary with customer information, callback URLs, and project details.
    /// </summary>
    Dictionary<string, string> EnrichMetadata(CreatePaymentDto request, Dictionary<string, string>? existingMetadata = null);
}

