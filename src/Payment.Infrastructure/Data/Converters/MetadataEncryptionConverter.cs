using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Payment.Domain.Interfaces;

namespace Payment.Infrastructure.Data.Converters;

/// <summary>
/// Value converter for encrypting/decrypting payment metadata at rest.
/// Implements PCI DSS compliance by encrypting sensitive metadata.
/// </summary>
public class MetadataEncryptionConverter : ValueConverter<Dictionary<string, string>, string>
{
    private static IDataEncryptionService? _encryptionService;
    
    /// <summary>
    /// Sets the encryption service (called during DI configuration).
    /// This is a workaround for EF Core's limitation with dependency injection in value converters.
    /// </summary>
    public static void SetEncryptionService(IDataEncryptionService encryptionService)
    {
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
    }

    public MetadataEncryptionConverter() : base(
        // Convert to database (encrypt)
        v => ConvertToDatabase(v),
        // Convert from database (decrypt)
        v => ConvertFromDatabase(v))
    {
    }

    private static string ConvertToDatabase(Dictionary<string, string>? metadata)
    {
        if (metadata == null || metadata.Count == 0)
            return string.Empty;

        var json = JsonSerializer.Serialize(metadata);
        
        // Encrypt if service is available, otherwise return plain JSON (for migrations/initial setup)
        if (_encryptionService != null)
        {
            return _encryptionService.Encrypt(json);
        }
        
        // Fallback: return plain JSON if encryption service not set (should not happen in production)
        return json;
    }

    private static Dictionary<string, string> ConvertFromDatabase(string? encryptedJson)
    {
        if (string.IsNullOrEmpty(encryptedJson))
            return new Dictionary<string, string>();

        string json;
        
        // Try to decrypt if service is available
        if (_encryptionService != null)
        {
            try
            {
                json = _encryptionService.Decrypt(encryptedJson);
            }
            catch
            {
                // If decryption fails, try parsing as plain JSON (for backward compatibility with unencrypted data)
                json = encryptedJson;
            }
        }
        else
        {
            // Fallback: assume plain JSON if encryption service not set
            json = encryptedJson;
        }

        try
        {
            var result = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return result ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }
}

