namespace Payment.Domain.Interfaces;

/// <summary>
/// Interface for encrypting and decrypting sensitive data at rest.
/// Follows Dependency Inversion Principle - Domain defines the contract.
/// </summary>
public interface IDataEncryptionService
{
    /// <summary>
    /// Encrypts plain text data using AES-256 encryption.
    /// </summary>
    /// <param name="plainText">The plain text to encrypt</param>
    /// <returns>Base64-encoded encrypted string</returns>
    string Encrypt(string plainText);

    /// <summary>
    /// Decrypts encrypted data back to plain text.
    /// </summary>
    /// <param name="encryptedText">The base64-encoded encrypted string</param>
    /// <returns>Decrypted plain text</returns>
    string Decrypt(string encryptedText);
}

