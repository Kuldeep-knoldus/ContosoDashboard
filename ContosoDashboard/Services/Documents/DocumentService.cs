using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ContosoDashboard.Data;
using ContosoDashboard.Models;

namespace ContosoDashboard.Services.Documents;

public sealed class DocumentService : IDocumentService
{
    private static readonly IReadOnlyDictionary<string, string> AllowedContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".doc"] = "application/msword",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".xls"] = "application/vnd.ms-excel",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".ppt"] = "application/vnd.ms-powerpoint",
            [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation"
        };

    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly IDocumentAuthorization _authorization;
    private readonly DocumentManagementOptions _options;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        ApplicationDbContext db,
        IFileStorageService storage,
        IDocumentAuthorization authorization,
        IOptions<DocumentManagementOptions> options,
        ILogger<DocumentService> logger)
    {
        _db = db;
        _storage = storage;
        _authorization = authorization;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DocumentUploadResult> UploadAsync(DocumentUploadRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var userId = GetUserId(user);
        var validationError = ValidateRequest(request, userId);
        if (validationError is not null)
        {
            return Failure(validationError);
        }

        var stored = await _storage.StoreAsync(request.Content, request.OriginalFileName, ct);
        try
        {
            if (stored.Length == 0 || stored.Length > _options.MaxFileSizeBytes)
            {
                throw new InvalidDataException($"Files must be between 1 byte and {_options.MaxFileSizeBytes} bytes.");
            }

            await using var transaction = _db.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true
                ? null
                : await _db.Database.BeginTransactionAsync(ct);
            var now = DateTime.UtcNow;
            var document = new Document
            {
                Title = request.Title.Trim(),
                Category = request.Category.Trim(),
                Description = Normalize(request.Description),
                Tags = Normalize(request.Tags),
                UploaderId = userId!.Value,
                ProjectId = request.ProjectId,
                TaskId = request.TaskId,
                OriginalFileName = stored.OriginalFileName,
                StoragePath = stored.StoragePath,
                ContentType = request.ContentType,
                FileSizeBytes = stored.Length,
                ContentHash = stored.ContentHash,
                UploadedUtc = now,
                UpdatedUtc = now
            };
            var job = new ScanJob
            {
                Document = document,
                DocumentId = document.DocumentId,
                VersionNumber = 1,
                ContentHash = stored.ContentHash,
                StoragePath = stored.StoragePath,
                IdempotencyKey = $"{Guid.NewGuid():N}:1",
                CreatedUtc = now
            };
            _db.Documents.Add(document);
            _db.ScanJobs.Add(job);
            await _db.SaveChangesAsync(ct);
            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }
            _logger.LogInformation("Document {DocumentId} queued for scanning by uploader {UploaderId}.", document.DocumentId, userId);
            return new DocumentUploadResult(true, document.DocumentId, document.LifecycleStatus.ToString(), document.ScanStatus.ToString(),
                "Upload accepted and queued for security scanning.", null);
        }
        catch
        {
            await _storage.DeleteAsync(stored.StoragePath, ct);
            throw;
        }
    }

    public async Task<IReadOnlyList<DocumentSummaryDto>> GetUserLibraryAsync(DocumentQuery query, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var userId = GetUserId(user);
        if (userId is null)
        {
            return Array.Empty<DocumentSummaryDto>();
        }

        var documents = await _db.Documents.AsNoTracking()
            .Where(d => (d.UploaderId == userId.Value ||
                (d.LifecycleStatus == DocumentLifecycleStatus.Active &&
                 d.ScanStatus == DocumentScanStatus.Clean &&
                 d.Visibility == DocumentVisibility.AuthorizedUsers)) &&
                (!query.ProjectId.HasValue || d.ProjectId == query.ProjectId.Value))
            .OrderByDescending(d => d.UploadedUtc)
            .ToListAsync(ct);
        return documents.Where(d => _authorization.CanView(d, user))
            .Select(ToSummary)
            .ToList();
    }

    public async Task<DocumentDetailDto?> GetDocumentByIdAsync(int documentId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var document = await _db.Documents.AsNoTracking().SingleOrDefaultAsync(d => d.DocumentId == documentId, ct);
        return document is not null && _authorization.CanView(document, user) ? ToDetail(document) : null;
    }

    public async Task<Stream?> DownloadAsync(int documentId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var document = await _db.Documents.SingleOrDefaultAsync(d => d.DocumentId == documentId, ct);
        if (document is null || !_authorization.CanDownload(document, user))
        {
            return null;
        }

        document.LastAccessedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await _storage.OpenReadAsync(document.StoragePath, ct);
    }

    private string? ValidateRequest(DocumentUploadRequest request, int? userId)
    {
        if (userId is null)
        {
            return "You must be signed in to upload documents.";
        }
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 255)
        {
            return "A document title is required and must be 255 characters or fewer.";
        }
        if (string.IsNullOrWhiteSpace(request.Category) || request.Category.Length > 100)
        {
            return "A document category is required and must be 100 characters or fewer.";
        }
        if (request.Length <= 0 || request.Length > _options.MaxFileSizeBytes)
        {
            return $"Files must be between 1 byte and {_options.MaxFileSizeBytes} bytes.";
        }
        var extension = Path.GetExtension(request.OriginalFileName);
        if (!AllowedContentTypes.TryGetValue(extension, out var expectedType) ||
            !string.Equals(expectedType, request.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            return "Only PDF, DOC, DOCX, XLS, XLSX, PPT, and PPTX files with a matching content type are supported.";
        }
        if (Path.GetFileName(request.OriginalFileName) != request.OriginalFileName)
        {
            return "The file name must not contain a path.";
        }

        return null;
    }

    private static int? GetUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return int.TryParse(value, out var id) && id > 0 ? id : null;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static DocumentUploadResult Failure(string error) => new(false, null, nameof(DocumentLifecycleStatus.PendingScan), nameof(DocumentScanStatus.Pending), "Upload rejected.", error);
    private static DocumentSummaryDto ToSummary(Document d) => new(d.DocumentId, d.Title, d.Category, d.OriginalFileName, d.FileSizeBytes, d.LifecycleStatus, d.Visibility, d.ScanStatus, d.UploadedUtc);
    private static DocumentDetailDto ToDetail(Document d) => new(d.DocumentId, d.Title, d.Category, d.Description, d.Tags, d.OriginalFileName, d.FileSizeBytes, d.LifecycleStatus, d.Visibility, d.ScanStatus, d.UploadedUtc);
}
