# Implementation Plan: Document Upload and Management

**Branch**: `[001-document-upload-management]` | **Date**: `2026-09-10` | **Spec**: [spec.md](./spec.md)

## Summary

Extend the existing Blazor Server document feature with an asynchronous malware
scanning pipeline. Uploads are accepted into private quarantine/pending-scan
state, then a scan job is placed on Azure Queue Storage for an Azure Function
consumer. A clean result releases the document automatically; flagged, failed,
expired, or otherwise unsafe results never become downloadable or visible to
other users. Manual administrator release is explicitly out of scope.

The application remains offline-first: the domain service depends on queue and
scanner interfaces, with an in-process/local deterministic adapter for training
and tests. Azure Functions and Azure Queue Storage are production-oriented
adapters, not required to run the dashboard locally.

## Technical Context

**Language/Version**: ASP.NET Core 8.0 / C# 12 / Blazor Server  
**Primary Dependencies**: Entity Framework Core, SQL Server LocalDB, Bootstrap 5.3, Bootstrap Icons; Azure.Storage.Queues and Azure Functions isolated worker only in cloud adapter projects  
**Storage**: SQL Server LocalDB for metadata and scan state; local filesystem through `IFileStorageService` for bytes; Azure Blob/queue adapters are replaceable providers  
**Testing**: xUnit service/domain tests; bUnit component checks; contract/integration tests with a fake queue, fake scanner, and local storage; manual seeded-user validation  
**Target Platform**: Local Windows development on .NET 8 with LocalDB; optional Azure-hosted worker deployment  
**Project Type**: Web application with Blazor Server UI, domain services, persistence, and separately deployable scan worker  
**Performance Goals**: Upload acknowledgment within 10 seconds for normal files; enqueue within the same transaction boundary as the pending record; scan completion is asynchronous and target latency is documented as operational, not a request-time guarantee  
**Constraints**: Offline training environment; 25 MB maximum; only PDF/DOC/DOCX/XLS/XLSX/PPT/PPTX; deny-by-default for pending, flagged, failed, and deleted records; no manual admin release; no secrets committed  
**Scale/Scope**: A few hundred users and thousands of documents; at-least-once queue delivery and idempotent scan result handling are required  

## Architecture and flow

1. `IDocumentService.UploadAsync` authenticates and authorizes the caller,
   validates metadata and extension/size, streams bytes to private quarantine
   storage under a generated key, and creates a `Document` with
   `Visibility=UploaderOnly`, `LifecycleStatus=PendingScan`, and
   `ScanStatus=Pending`.
2. A durable `ScanJob` outbox record is written with the document ID, immutable
   storage key, content hash, scan attempt, and idempotency key. A dispatcher
   publishes a small message to Azure Queue Storage (not file bytes).
3. The Azure Function queue trigger claims the job, reads the quarantined
   object through a managed identity, invokes the scanner adapter, and writes a
   signed/validated result through `IScanResultService`.
4. Result handling checks the document ID, content hash/version, and idempotency
   key. A **clean** result from the current pending job atomically changes the
   document to `Active`, `Visibility=AuthorizedUsers`, `ScanStatus=Clean`, and
   records `ReleasedUtc`. A **flagged** result changes it to
   `Quarantined`, `Visibility=UploaderOnly`, and blocks download. No result
   grants access to a stale or replaced version.
5. Transient worker/storage/scanner failures are retried by queue visibility
   timeout and bounded exponential backoff. After the configured dequeue limit,
   the message moves to a poison queue and the document becomes
   `ScanFailed`/quarantined (never active). A later automated rescan may create a
   new attempt; there is no UI or administrative bypass.
6. Downloads, previews, searches, project lists, shares, and notifications all
   call the same service-level authorization predicate. Only the uploader (and
   authorized security/audit paths that do not expose bytes) can see pending or
   quarantined status; only `Active` documents can be downloaded or previewed.

### Failure, retry, and idempotency rules

- Upload metadata and the outbox job use one database transaction. If storage
  succeeds but the transaction fails, a compensating cleanup task removes the
  orphaned object; if enqueue fails, the dispatcher retries from the outbox.
- Queue messages are at-least-once. `ScanJob.IdempotencyKey` and
  `ScanAttempt.ResultRecordedUtc` make duplicate clean/flagged results no-ops.
- Results for deleted documents, mismatched content hashes, old versions, or
  unknown job IDs are recorded for diagnostics but cannot alter access state.
- Scanner timeouts, unavailable quarantine storage, malformed results, and
  function exceptions are retryable. Repeated failure is fail-closed in the
  poison path; monitoring must alert operators.
- Clean is never inferred from timeout, missing result, queue completion, or
  local filename/extension checks. Automatic release requires a subsequent
  authenticated clean result for the current bytes.

## Security and configuration

- Quarantine storage is private, outside the web root, and addressed only by
  generated keys; original names are display metadata.
- Queue messages contain IDs and hashes, never file content, tokens, or
  connection strings. Azure production access uses managed identity and
  least-privilege queue/blob roles; secrets are environment/secret-store
  configuration.
- Validate extension, size, content type, and (where available) magic bytes
  before enqueueing; normalize metadata and protect against path traversal.
- Use authorization at page and service boundaries, ownership/project/share
  checks for every ID, and deny-by-default queries to prevent IDOR.
- Configuration includes `ScanQueueName`, `ScanPoisonQueueName`,
  `ScanMaxAttempts`, `ScanVisibilityTimeoutSeconds`, `ScanResultTimeout`,
  `QuarantineRetentionDays`, `FileStorageRoot`, and provider selection.
  Local defaults use filesystem + in-memory queue/scanner; cloud configuration
  is opt-in and must fail startup with a clear non-secret error if incomplete.
- Mock cookie authentication and seeded users are training-only. Production
  deployment additionally requires a real identity provider, TLS, password
  protection, MFA, audit retention, malware-vendor governance, and compliance
  review.

## Constitution Check

*GATE: Must pass before and after design.*

- **Offline-First, Abstraction-Ready Architecture: PASS.** Azure queue/function
  integration is behind `IScanJobQueue`, `IScanEngine`, and storage interfaces;
  local adapters run without network access.
- **Defense-in-Depth Authorization: PASS.** Pending/quarantined records are
  uploader-only; all reads/downloads/results validate identity, ownership,
  current version, and authorization at the service boundary.
- **Separation of Concerns and Explicit Contracts: PASS.** UI, domain,
  persistence, queue publisher, and Function worker communicate via explicit
  DTOs and contracts; no page calls Azure SDKs.
- **Specification- and Test-Driven Change: PASS.** This plan adds acceptance
  tests for state transitions, retries, duplicate messages, authorization,
  poison handling, and provider contracts.
- **Simplicity, Accessibility, and Maintainability: PASS.** Cloud services are
  optional adapters, the local path is deterministic, and no new UI workflow
  is required beyond clear scan status and accessible messages.

No violations or unresolved clarifications remain.

## Project Structure

```text
specs/001-document-upload-management/
├── plan.md
├── research.md
├── data-model.md
├── contracts/
│   └── document-service-contract.md
└── quickstart.md
```

Implementation should extend the existing `ContosoDashboard/Models`,
`Data`, `Services`, and `Pages` layers. The Azure Function/queue adapter may be
a separate worker project only if the repository adds one; the domain must not
reference Function runtime types.

## Complexity Tracking

No constitution exceptions required. Azure is an adapter because asynchronous
malware scanning is a security boundary; it is not a second business workflow.
