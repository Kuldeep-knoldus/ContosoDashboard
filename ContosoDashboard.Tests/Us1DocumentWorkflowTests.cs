using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services.Documents;

namespace ContosoDashboard.Tests;

public sealed class Us1DocumentWorkflowTests
{
    [Theory]
    [InlineData("file.pdf", "application/pdf")]
    [InlineData("file.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("file.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("file.pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation")]
    public async Task Upload_accepts_supported_file_types(string fileName, string contentType)
    {
        var storage = new TestStorage();
        var service = CreateService(storage);
        var result = await service.UploadAsync(
            new DocumentUploadRequest("Quarterly report", "Reports", null, null, null, null,
                new MemoryStream("content"u8.ToArray()), fileName, contentType, 7),
            Principal(4));

        Assert.True(result.Success);
        Assert.Equal(nameof(DocumentScanStatus.Pending), result.ScanStatus);
        Assert.Single(storage.Files);
    }

    [Theory]
    [InlineData("file.exe", "application/octet-stream")]
    [InlineData("../file.pdf", "application/pdf")]
    public async Task Upload_rejects_unsafe_or_unsupported_file(string fileName, string contentType)
    {
        var storage = new TestStorage();
        var result = await CreateService(storage).UploadAsync(
            new DocumentUploadRequest("Report", "Reports", null, null, null, null,
                new MemoryStream("content"u8.ToArray()), fileName, contentType, 7),
            Principal(4));

        Assert.False(result.Success);
        Assert.Empty(storage.Files);
    }

    [Fact]
    public async Task Upload_rejects_missing_metadata_and_files_over_25_mb()
    {
        var storage = new TestStorage();
        var service = CreateService(storage);

        var missingTitle = await service.UploadAsync(
            new DocumentUploadRequest("", "Reports", null, null, null, null,
                new MemoryStream("content"u8.ToArray()), "file.pdf", "application/pdf", 7),
            Principal(4));
        var tooLarge = await service.UploadAsync(
            new DocumentUploadRequest("Report", "Reports", null, null, null, null,
                new MemoryStream("content"u8.ToArray()), "file.pdf", "application/pdf", 25 * 1024 * 1024 + 1),
            Principal(4));

        Assert.False(missingTitle.Success);
        Assert.False(tooLarge.Success);
        Assert.Empty(storage.Files);
    }

    [Fact]
    public async Task Clean_result_releases_document_and_duplicate_is_noop()
    {
        var databaseName = Guid.NewGuid().ToString();
        var document = new Document
        {
            UploaderId = 4, Title = "Report", Category = "Reports",
            OriginalFileName = "report.pdf", StoragePath = "2026/report.pdf", ContentHash = "abc", ContentType = "application/pdf"
        };
        var job = new ScanJob
        {
            ScanJobId = Guid.NewGuid(), Document = document, VersionNumber = 1,
            ContentHash = "abc", StoragePath = document.StoragePath, IdempotencyKey = "job-1"
        };
        await using (var seedDb = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName).Options))
        {
            seedDb.Documents.Add(document);
            seedDb.ScanJobs.Add(job);
            await seedDb.SaveChangesAsync();
        }

        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName).Options);
        job = await db.ScanJobs.AsNoTracking().SingleAsync();
        var service = new ScanResultService(db, Options.Create(new DocumentManagementOptions()), NullLogger<ScanResultService>.Instance);
        var message = new ScanResultMessage(job.ScanJobId, job.DocumentId, 1, "abc", "job-1", ScanVerdict.Clean, "fake-1");

        var first = await service.ApplyAsync(message);
        var second = await service.ApplyAsync(message);

        Assert.True(first.Applied);
        Assert.True(second.Ignored);
        var current = await db.Documents.SingleAsync();
        Assert.Equal(DocumentLifecycleStatus.Active, current.LifecycleStatus);
        Assert.Equal(DocumentVisibility.AuthorizedUsers, current.Visibility);
    }

    [Fact]
    public async Task Mismatched_hash_result_is_ignored()
    {
        await using var db = CreateDb();
        var document = new Document
        {
            UploaderId = 4, Title = "Report", Category = "Reports",
            OriginalFileName = "report.pdf", StoragePath = "2026/report.pdf", ContentHash = "abc", ContentType = "application/pdf"
        };
        var job = new ScanJob
        {
            ScanJobId = Guid.NewGuid(), Document = document, VersionNumber = 1,
            ContentHash = "abc", StoragePath = document.StoragePath, IdempotencyKey = "job-stale"
        };
        db.Documents.Add(document);
        db.ScanJobs.Add(job);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var service = new ScanResultService(db, Options.Create(new DocumentManagementOptions()), NullLogger<ScanResultService>.Instance);

        var result = await service.ApplyAsync(new ScanResultMessage(job.ScanJobId, job.DocumentId, 1, "different", "job-stale", ScanVerdict.Clean, "fake-1"));

        Assert.True(result.Ignored);
        Assert.Equal(DocumentLifecycleStatus.PendingScan, (await db.Documents.SingleAsync()).LifecycleStatus);
    }

    [Fact]
    public async Task Fake_scanner_flags_eicar_marker()
    {
        var scanner = new FakeScanEngine();
        await using var content = new MemoryStream("EICAR test marker"u8.ToArray());

        var verdict = await scanner.ScanAsync(content);

        Assert.Equal(ScanVerdict.Flagged, verdict);
    }

    [Fact]
    public void Pending_document_is_private_to_uploader()
    {
        var document = new Document
        {
            UploaderId = 4, Title = "Pending", Category = "Reports",
            LifecycleStatus = DocumentLifecycleStatus.PendingScan,
            Visibility = DocumentVisibility.UploaderOnly,
            ScanStatus = DocumentScanStatus.Pending
        };
        var authorization = new DocumentAuthorization();

        Assert.True(authorization.CanView(document, Principal(4)));
        Assert.False(authorization.CanView(document, Principal(5)));
        Assert.False(authorization.CanDownload(document, Principal(4)));
    }

    [Fact]
    public async Task Flagged_result_quarantines_document_and_keeps_it_uploader_only()
    {
        await using var db = CreateDb();
        var document = new Document
        {
            UploaderId = 4, Title = "Report", Category = "Reports",
            OriginalFileName = "report.pdf", StoragePath = "2026/report.pdf", ContentHash = "abc", ContentType = "application/pdf"
        };
        var job = new ScanJob
        {
            ScanJobId = Guid.NewGuid(), Document = document, VersionNumber = 1,
            ContentHash = "abc", StoragePath = document.StoragePath, IdempotencyKey = "job-flagged"
        };
        db.Documents.Add(document);
        db.ScanJobs.Add(job);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var service = new ScanResultService(db, Options.Create(new DocumentManagementOptions()), NullLogger<ScanResultService>.Instance);

        await service.ApplyAsync(new ScanResultMessage(job.ScanJobId, job.DocumentId, 1, "abc", "job-flagged", ScanVerdict.Flagged, "fake-1"));

        var current = await db.Documents.SingleAsync();
        Assert.Equal(DocumentLifecycleStatus.Quarantined, current.LifecycleStatus);
        Assert.Equal(DocumentVisibility.UploaderOnly, current.Visibility);
    }

    [Fact]
    public async Task Transient_failures_are_poisoned_after_max_attempts()
    {
        await using var db = CreateDb();
        var document = new Document
        {
            UploaderId = 4, Title = "Report", Category = "Reports",
            OriginalFileName = "report.pdf", StoragePath = "2026/report.pdf", ContentHash = "abc", ContentType = "application/pdf"
        };
        var job = new ScanJob
        {
            ScanJobId = Guid.NewGuid(), Document = document, VersionNumber = 1,
            ContentHash = "abc", StoragePath = document.StoragePath, IdempotencyKey = "job-retry"
        };
        db.Documents.Add(document);
        db.ScanJobs.Add(job);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var service = new ScanResultService(db, Options.Create(new DocumentManagementOptions { ScanMaxAttempts = 2 }), NullLogger<ScanResultService>.Instance);
        var message = new ScanResultMessage(job.ScanJobId, job.DocumentId, 1, "abc", "job-retry", ScanVerdict.TransientFailure, "fake-1");

        await service.ApplyAsync(message);
        db.ChangeTracker.Clear();
        await service.ApplyAsync(message);

        var currentJob = await db.ScanJobs.SingleAsync();
        var currentDocument = await db.Documents.SingleAsync();
        Assert.Equal(ScanJobStatus.Poisoned, currentJob.Status);
        Assert.Equal(DocumentLifecycleStatus.ScanFailed, currentDocument.LifecycleStatus);
        Assert.Equal(DocumentVisibility.UploaderOnly, currentDocument.Visibility);
    }

    [Fact]
    public void Queue_message_contains_only_compact_metadata()
    {
        var message = new ScanJobMessage(Guid.NewGuid(), 4, 1, "aabb", "2026/4", "idempotency");
        var json = System.Text.Json.JsonSerializer.Serialize(message);

        Assert.DoesNotContain("bytes", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        Assert.InRange(json.Length, 1, 64 * 1024);
    }

    [Fact]
    public async Task Transient_failure_before_limit_keeps_document_pending_for_retry()
    {
        await using var db = CreateDb();
        var document = new Document
        {
            UploaderId = 4, Title = "Report", Category = "Reports",
            OriginalFileName = "report.pdf", StoragePath = "2026/report.pdf", ContentHash = "abc",
            ContentType = "application/pdf"
        };
        var job = new ScanJob
        {
            ScanJobId = Guid.NewGuid(), Document = document, VersionNumber = 1,
            ContentHash = "abc", StoragePath = document.StoragePath, IdempotencyKey = "retry-once"
        };
        db.Documents.Add(document);
        db.ScanJobs.Add(job);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = new ScanResultService(
            db, Options.Create(new DocumentManagementOptions { ScanMaxAttempts = 3 }),
            NullLogger<ScanResultService>.Instance);
        var result = await service.ApplyAsync(new ScanResultMessage(
            job.ScanJobId, job.DocumentId, 1, "abc", "retry-once",
            ScanVerdict.TransientFailure, "fake-1"));

        var currentJob = await db.ScanJobs.SingleAsync();
        var currentDocument = await db.Documents.SingleAsync();
        Assert.True(result.Applied);
        Assert.Equal(ScanJobStatus.Retrying, currentJob.Status);
        Assert.Equal(DocumentScanStatus.Processing, currentDocument.ScanStatus);
        Assert.Equal(DocumentLifecycleStatus.PendingScan, currentDocument.LifecycleStatus);
    }

    [Fact]
    public async Task Dispatcher_marks_job_retrying_when_queue_temporarily_fails()
    {
        await using var db = CreateDb();
        var document = new Document
        {
            UploaderId = 4, Title = "Report", Category = "Reports",
            OriginalFileName = "report.pdf", StoragePath = "2026/report.pdf", ContentHash = "abc",
            ContentType = "application/pdf"
        };
        var job = new ScanJob
        {
            ScanJobId = Guid.NewGuid(), Document = document, VersionNumber = 1,
            ContentHash = "abc", StoragePath = document.StoragePath, IdempotencyKey = "dispatch-retry"
        };
        db.Documents.Add(document);
        db.ScanJobs.Add(job);
        await db.SaveChangesAsync();

        var queue = new FailingOnceQueue();
        var dispatcher = new ScanJobDispatcher(db, queue, NullLogger<ScanJobDispatcher>.Instance);

        Assert.Equal(0, await dispatcher.DispatchPendingAsync());
        Assert.Equal(ScanJobStatus.Retrying, (await db.ScanJobs.SingleAsync()).Status);

        Assert.Equal(1, await dispatcher.DispatchPendingAsync());
        Assert.Equal(ScanJobStatus.Dispatched, (await db.ScanJobs.SingleAsync()).Status);
        Assert.Single(queue.Messages);
    }

    [Fact]
    public async Task Deleted_document_ignores_clean_result_without_releasing_bytes()
    {
        await using var db = CreateDb();
        var document = new Document
        {
            UploaderId = 4, Title = "Report", Category = "Reports",
            OriginalFileName = "report.pdf", StoragePath = "2026/report.pdf", ContentHash = "abc",
            ContentType = "application/pdf", LifecycleStatus = DocumentLifecycleStatus.Deleted,
            Visibility = DocumentVisibility.Hidden
        };
        var job = new ScanJob
        {
            ScanJobId = Guid.NewGuid(), Document = document, VersionNumber = 1,
            ContentHash = "abc", StoragePath = document.StoragePath, IdempotencyKey = "deleted-result"
        };
        db.Documents.Add(document);
        db.ScanJobs.Add(job);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = new ScanResultService(
            db, Options.Create(new DocumentManagementOptions()),
            NullLogger<ScanResultService>.Instance);
        var result = await service.ApplyAsync(new ScanResultMessage(
            job.ScanJobId, job.DocumentId, 1, "abc", "deleted-result",
            ScanVerdict.Clean, "fake-1"));

        Assert.True(result.Ignored);
        Assert.Equal(DocumentLifecycleStatus.Deleted, (await db.Documents.SingleAsync()).LifecycleStatus);
    }

    private static DocumentService CreateService(TestStorage storage)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new ApplicationDbContext(options);
        return new DocumentService(db, storage, new DocumentAuthorization(),
            Options.Create(new DocumentManagementOptions()), NullLogger<DocumentService>.Instance);
    }

    private static ApplicationDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ClaimsPrincipal Principal(int userId) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "test"));

    private sealed class TestStorage : IFileStorageService
    {
        public Dictionary<string, byte[]> Files { get; } = new();

        public async Task<StoredFileResult> StoreAsync(Stream content, string originalFileName, CancellationToken ct = default)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct);
            var path = $"quarantine/{Guid.NewGuid():N}.bin";
            Files[path] = buffer.ToArray();
            return new StoredFileResult(path, originalFileName, "abc", buffer.Length);
        }

        public Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct = default) =>
            Task.FromResult<Stream>(new MemoryStream(Files[storagePath], writable: false));

        public Task DeleteAsync(string storagePath, CancellationToken ct = default)
        {
            Files.Remove(storagePath);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingOnceQueue : IScanJobQueue
    {
        private bool _failed;
        public List<ScanJobMessage> Messages { get; } = new();

        public Task EnqueueAsync(ScanJobMessage message, CancellationToken ct = default)
        {
            if (!_failed)
            {
                _failed = true;
                throw new InvalidOperationException("temporary queue failure");
            }

            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
