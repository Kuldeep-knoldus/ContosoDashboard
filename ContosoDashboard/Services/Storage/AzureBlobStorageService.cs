using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Identity;
using Microsoft.Extensions.Options;
using ContosoDashboard.Services.Documents;

namespace ContosoDashboard.Services.Storage;

public sealed class AzureBlobStorageService : IFileStorageService
{
    private readonly BlobContainerClient _container;

    public AzureBlobStorageService(IOptions<DocumentManagementOptions> options)
    {
        if (string.IsNullOrWhiteSpace(options.Value.AzureStorageConnectionString) &&
            string.IsNullOrWhiteSpace(options.Value.AzureStorageAccountUri))
        {
            throw new InvalidOperationException("Azure blob storage requires AzureStorageConnectionString or AzureStorageAccountUri.");
        }

        _container = string.IsNullOrWhiteSpace(options.Value.AzureStorageConnectionString)
            ? new BlobContainerClient(new Uri($"{options.Value.AzureStorageAccountUri.TrimEnd('/')}/{options.Value.AzureBlobContainerName}"),
                new DefaultAzureCredential())
            : new BlobContainerClient(options.Value.AzureStorageConnectionString, options.Value.AzureBlobContainerName);
    }

    public async Task<StoredFileResult> StoreAsync(Stream content, string originalFileName, CancellationToken ct = default)
    {
        var safeName = Path.GetFileName(originalFileName);
        if (safeName != originalFileName || string.IsNullOrWhiteSpace(safeName))
        {
            throw new InvalidDataException("The file name is unsafe.");
        }

        var path = $"{DateTime.UtcNow:yyyyMMdd}/{Guid.NewGuid():N}{Path.GetExtension(safeName).ToLowerInvariant()}";
        await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
        var client = _container.GetBlobClient(path);
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        hash.AppendData(bytes);
        buffer.Position = 0;
        await client.UploadAsync(buffer, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "application/octet-stream" } }, ct);
        return new StoredFileResult(path, safeName, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(), bytes.LongLength);
    }

    public async Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct = default)
    {
        var response = await _container.GetBlobClient(storagePath).DownloadStreamingAsync(cancellationToken: ct);
        return response.Value.Content;
    }

    public async Task DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        await _container.DeleteBlobIfExistsAsync(storagePath, DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);
    }
}
