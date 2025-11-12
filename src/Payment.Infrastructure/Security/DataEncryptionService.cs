using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Payment.Domain.Interfaces;

namespace Payment.Infrastructure.Security;

/// <summary>
/// AES-256 encryption service for encrypting sensitive data at rest.
/// Implements PCI DSS compliance requirements for data encryption.
/// </summary>
public class DataEncryptionService : IDataEncryptionService
{
    private readonly byte[] _encryptionKey;
    private const int KeySize = 256; // AES-256
    private const int IvSize = 128; // 16 bytes for AES IV

    public DataEncryptionService(IConfiguration configuration)
    {
        var encryptionKeyBase64 = configuration["DataEncryption:Key"] 
            ?? throw new InvalidOperationException("DataEncryption:Key not configured. This is required for PCI DSS compliance.");

        try
        {
            _encryptionKey = Convert.FromBase64String(encryptionKeyBase64);
            
            // Validate key size (AES-256 requires 32 bytes = 256 bits)
            if (_encryptionKey.Length != 32)
            {
                throw new InvalidOperationException(
                    $"Encryption key must be 32 bytes (256 bits) for AES-256. Current length: {_encryptionKey.Length} bytes. " +
                    "Generate a key using: Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))");
            }
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "DataEncryption:Key must be a valid base64-encoded string. " +
                "Generate a key using: Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))", ex);
        }
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = _encryptionKey;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Prepend IV to ciphertext (IV is not secret, but must be unique per encryption)
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
            return encryptedText;

        try
        {
            var fullCipher = Convert.FromBase64String(encryptedText);

            // Extract IV (first 16 bytes) and ciphertext (remaining bytes)
            if (fullCipher.Length < 16)
            {
                throw new CryptographicException("Encrypted data is too short to contain IV");
            }

            var iv = new byte[16];
            var cipher = new byte[fullCipher.Length - 16];
            
            Buffer.BlockCopy(fullCipher, 0, iv, 0, 16);
            Buffer.BlockCopy(fullCipher, 16, cipher, 0, fullCipher.Length - 16);

            using var aes = Aes.Create();
            aes.KeySize = KeySize;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = _encryptionKey;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("Invalid encrypted data format", ex);
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicException("Decryption failed. The encryption key may be incorrect or the data may be corrupted.", ex);
        }
    }
}

