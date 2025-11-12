namespace Payment.Domain.Interfaces;

/// <summary>
/// Interface for file storage operations (S3, MinIO, Azure Blob Storage).
/// Follows Interface Segregation Principle - focused on storage operations only.
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Uploads a file to storage and returns the public URL.
    /// </summary>
    /// <param name="fileName">Name of the file (e.g., "reports/2025-10.pdf")</param>
    /// <param name="content">File content as byte array</param>
    /// <param name="contentType">MIME type (e.g., "application/pdf", "text/csv")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Public URL to access the uploaded file</returns>
    Task<string> UploadAsync(string fileName, byte[] content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file from storage.
    /// </summary>
    /// <param name="fileName">Name of the file to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task DeleteAsync(string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a file exists in storage.
    /// </summary>
    /// <param name="fileName">Name of the file to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if file exists, false otherwise</returns>
    Task<bool> ExistsAsync(string fileName, CancellationToken cancellationToken = default);
}

