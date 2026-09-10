using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using ContosoDashboard.Services.Documents;

namespace ContosoDashboard.Services.Storage;

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _root;

    public LocalFileStorageService(IOptions<DocumentManagementOptions> options, IWebHostEnvironment environment)
    {
        var configuredRoot = options.Value.FileStorageRoot;
        _root = Path.GetFullPath(Path.IsPathRooted(configuredRoot)
            ? configuredRoot
            : Path.Combine(environment.ContentRootPath, configuredRoot));
        Directory.CreateDirectory(_root);
    }

    public async Task<StoredFileResult> StoreAsync(Stream content, string originalFileName, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var safeName = Path.GetFileName(originalFileName);
        if (string.IsNullOrWhiteSpace(safeName) || safeName != originalFileName ||
            safeName is "." or ".." || safeName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidDataException("The file name is unsafe.");
        }

        var storagePath = $"{DateTime.UtcNow:yyyyMMdd}/{Guid.NewGuid():N}{Path.GetExtension(safeName).ToLowerInvariant()}";
        var fullPath = GetFullPath(storagePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var target = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long length = 0;
        int read;
        while ((read = await content.ReadAsync(buffer, ct)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), ct);
            hasher.AppendData(buffer, 0, read);
            length += read;
        }

        return new StoredFileResult(storagePath, safeName, Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant(), length);
    }

    public Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct = default)
    {
        var fullPath = GetFullPath(storagePath);
        Stream result = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult(result);
    }

    public Task DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        var fullPath = GetFullPath(storagePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private string GetFullPath(string storagePath)
    {
        var normalized = storagePath.Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The storage path is invalid.");
        }

        var fullPath = Path.GetFullPath(Path.Combine(_root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(_root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The storage path escapes quarantine.");
        }

        return fullPath;
    }
}
