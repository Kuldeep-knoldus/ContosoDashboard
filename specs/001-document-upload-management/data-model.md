# Data Model: Document Upload and Asynchronous Scan

## Document

Represents metadata and the security lifecycle of one logical document version.

- `DocumentId` (int, PK)
- `Title` (required, max 255), `Category` (required, max 100),
  `Description` (optional, max 2000), normalized `Tags`
- `ProjectId`/`TaskId` (optional FKs), `UploaderId` (required FK)
- `OriginalFileName` (sanitized display value only), `StoredFileName`/`StoragePath`
  (generated private-storage key), `ContentType`, `FileSizeBytes` (1..25 MB)
- `ContentHash` (required SHA-256 of stored bytes), `VersionNumber` (positive)
- `LifecycleStatus`: `PendingScan`, `Active`, `Quarantined`, `ScanFailed`,
  `Deleted`, `Replaced`
- `Visibility`: `UploaderOnly`, `AuthorizedUsers`, `Hidden`
- `ScanStatus`: `Pending`, `Processing`, `Clean`, `Flagged`, `Failed`
- `UploadedUtc`, `UpdatedUtc`, `ReleasedUtc?`, `LastAccessedUtc?`

Rules: only `Active` + `Clean` + `AuthorizedUsers` may be downloaded,
previewed, searched by other users, shared, or listed in a project. The
uploader can see metadata/status for their own pending or quarantined record,
but never its bytes. No admin-release transition exists.

## ScanJob (outbox and delivery state)

- `ScanJobId` (GUID, PK), `DocumentId` (FK), `VersionNumber`, `ContentHash`
- `IdempotencyKey` (unique), `StoragePath` (private key), `Attempt`
- `Status`: `PendingDispatch`, `Dispatched`, `Processing`, `Completed`,
  `Retrying`, `Poisoned`, `Superseded`
- `CreatedUtc`, `DispatchedUtc?`, `LeaseUtc?`, `CompletedUtc?`, `LastError?`

The unique idempotency key is stable for a scan attempt and is included in the
queue message. The job is superseded when a replacement creates a new version.

## ScanAttempt

- `ScanAttemptId` (GUID, PK), `ScanJobId` (FK), `AttemptNumber`,
  `StartedUtc`, `CompletedUtc?`
- `Result`: `Clean`, `Flagged`, `TransientFailure`, `PermanentFailure`,
  `Malformed`
- `ScannerCode?`, `ScannerVersion?`, `ResultRecordedUtc?`, `ErrorSummary?`
- `ResultDigest?` for integrity/audit correlation

Results are append-only. A duplicate result with the same job/idempotency key
is a no-op; a result with a mismatched hash/version is rejected and audited.

## DocumentShare and DocumentAuditEntry

Existing `DocumentShare` and immutable `DocumentAuditEntry` remain as described
in the original model. Add scan events (`ScanQueued`, `ScanStarted`,
`ScanCleanReleased`, `ScanFlagged`, `ScanRetry`, `ScanPoisoned`,
`ScanResultIgnored`) with actor `System` where no human actor exists.

## Relationships and indexes

- `User` 1--* `Document`; `Project` 1--* `Document`; `TaskItem` 1--* `Document`
- `Document` 1--* `ScanJob`; `ScanJob` 1--* `ScanAttempt`
- `Document` 1--* `DocumentShare`; `Document` 1--* `DocumentAuditEntry`
- Unique index on `ScanJob.IdempotencyKey`; indexes on
  `(DocumentId, VersionNumber)`, `(LifecycleStatus, ScanStatus)`, and
  `ContentHash` as appropriate for operational queries.

## State transitions

```text
Upload -> PendingScan/Pending/UploaderOnly
PendingScan -> Processing (worker claim)
Processing -> Active/Clean/AuthorizedUsers (current clean result)
Processing -> Quarantined/Flagged/UploaderOnly (unsafe result)
Processing -> Retrying/PendingScan (transient failure, attempts remain)
Retrying -> ScanFailed/Quarantined/UploaderOnly (poison or permanent failure)
Active -> Replaced (new version starts at PendingScan)
Any non-deleted -> Deleted (authorized delete; bytes become retention-managed)
```

There is no `Quarantined -> Active` manual transition. An automated subsequent
scan creates a new attempt and only its clean result can release the current
bytes.

## Storage responsibilities

- Database: metadata, state, content hash, outbox, attempts, permissions, audit.
- Private file/blob storage: bytes keyed by generated path; quarantine and active
  areas are not web-addressable.
- Queue: transient job envelope only; no content or secrets.
- UI/service: status messaging and authorization; service owns state changes.
