---
description: "Dependency-ordered implementation tasks for document upload management"
---

# Tasks: Document Upload and Management

**Input**: `specs/001-document-upload-management/`

**Stack**: ASP.NET Core 8, C# 12, Blazor Server, EF Core, SQL Server LocalDB,
local filesystem storage, Azure Queue Storage, Azure Functions isolated worker,
xUnit, and bUnit.

## Phase 1: Setup

- [ ] T001 Add Azure.Storage.Queues and Azure Functions isolated-worker dependencies to `ContosoDashboard/ContosoDashboard.csproj` and create `DocumentScanFunction/DocumentScanFunction.csproj`.
- [ ] T002 [P] Create unit, integration, component, and contract test projects and add them to `ContosoDashboard.sln`.
- [ ] T003 [P] Add document storage, scan queue, retry, poison queue, and provider settings to `ContosoDashboard/appsettings.json` and `appsettings.Development.json`.
- [ ] T004 [P] Add Azure Function host, local queue, and local settings templates under `DocumentScanFunction/`.
- [ ] T005 Document local/offline setup, LocalDB prerequisites, Azure emulator options, and test commands in `docs/document-management-development.md`.

## Phase 2: Foundational Infrastructure

- [ ] T006 Create `Document`, `ScanJob`, and `ScanAttempt` entities with lifecycle, visibility, scan status, version, hash, and audit fields in `ContosoDashboard/Models/`.
- [ ] T007 Configure EF Core relationships, constraints, unique idempotency keys, indexes, and migrations in `ContosoDashboard/Data/`.
- [ ] T008 Define provider-neutral upload, scan-job, scan-result, verdict, and document DTO contracts in `ContosoDashboard/Services/Documents/DocumentContracts.cs`.
- [ ] T009 Define `IDocumentService`, `IScanJobQueue`, `IScanEngine`, `IScanResultService`, `IFileStorageService`, and dispatcher interfaces without leaking Azure SDK types.
- [ ] T010 Implement generated private storage keys, filename/content validation, path-traversal protection, SHA-256 hashing, streaming, and cleanup in `ContosoDashboard/Services/Storage/`.
- [ ] T011 Implement centralized deny-by-default authorization for uploader-only, active, project-member, share-recipient, download, preview, list, and detail operations.
- [ ] T012 Add strongly typed scan/storage options, startup validation, local filesystem storage, in-memory queue, and deterministic fake scanner registrations.
- [ ] T013 Add Azure queue/blob adapters behind explicit provider configuration and fail startup clearly when required cloud configuration is incomplete.
- [ ] T014 Implement transactional scan-job outbox creation and retrying dispatch in `ScanJobOutboxService` and `ScanJobDispatcher`.
- [ ] T015 Add structured audit events for queued, started, clean, flagged, retry, poison, and ignored scan results.
- [ ] T016 [P] Add shared integration fixtures with LocalDB, seeded users/projects/memberships, fake authentication, storage, queue, and scanner.

## Phase 3: User Story 1 - Upload and Scan (P1 MVP)

**Goal**: Upload supported files into private quarantine, enqueue asynchronous scanning,
and release only current versions with a clean scan.

### Tests

- [ ] T017 [P] [US1] Test required metadata, supported PDF/DOC/DOCX/XLS/XLSX/PPT/PPTX formats, content types, unsafe names, path traversal, and the 25 MB limit.
- [ ] T018 [P] [US1] Test pending, processing, clean, active, flagged, quarantined, retrying, failed, deleted, and replaced state transitions.
- [ ] T019 [P] [US1] Test that only a current clean result changes visibility to active/authorized.
- [ ] T020 [P] [US1] Test duplicate idempotency keys, mismatched hash/version, stale results, deleted documents, replaced versions, and malformed messages.
- [ ] T021 [P] [US1] Test transactional upload/outbox persistence, compensating storage cleanup, and dispatcher retries.
- [ ] T022 [P] [US1] Test schema-versioned queue envelopes contain no bytes, secrets, or user tokens and remain within message limits.

### Implementation

- [ ] T023 [US1] Implement upload validation, metadata normalization, authorization, private quarantine storage, hashing, `Document` creation, and `ScanJob` outbox creation.
- [ ] T024 [US1] Implement idempotent scan-result application with hash/version/job/provenance validation and fail-closed state transitions.
- [ ] T025 [US1] Implement bounded retries, visibility timeout, poison-message handling, uploader-only status, and terminal failure auditing.
- [ ] T026 [US1] Implement the Azure Queue Storage adapter that publishes compact schema-versioned `ScanJobMessage` JSON.
- [ ] T027 [US1] Implement the private Azure Blob Storage adapter using generated keys and managed identity; never expose public blob URLs.
- [ ] T028 [US1] Implement `DocumentScanFunction/ScanJobFunction.cs` as an Azure Functions isolated-worker Queue Storage trigger that reads a scan job, loads private content, invokes `IScanEngine`, applies the result, and acknowledges only successful or terminal messages.
- [ ] T029 [US1] Configure Function retry/dequeue limits, visibility timeout, poison queue routing, and non-secret structured logging.
- [ ] T030 [US1] Add Azure adapter and Function contract tests for transient retry, terminal failure, poison routing, acknowledgement, idempotency, and no-secret/no-bytes guarantees.
- [ ] T031 [US1] Add upload UI with accessible metadata labels, per-file validation, pending/flagged/failed status messaging, and no download controls before release.
- [ ] T032 [US1] Add uploader-only document status/detail components that never expose quarantined bytes, storage paths, or download links.
- [ ] T033 [US1] Add end-to-end integration coverage for pending privacy, clean release, flagged files, unavailable scanner, retries, poison messages, duplicate delivery, stale delivery, and interrupted multi-file uploads.

**Checkpoint**: The upload and asynchronous scan workflow is independently testable
with local adapters and Azure Function/Queue Storage adapters.

## Phase 4: User Story 2 - Browse and Find Documents (P1)

- [ ] T034 [P] [US2] Test authorized filtering/search by title, description, tags, uploader, project, category, and date while excluding inaccessible lifecycle states.
- [ ] T035 [P] [US2] Test personal/project/shared libraries, downloads, previews, and IDOR resistance for guessed document IDs.
- [ ] T036 [P] [US2] Add bUnit coverage for filters, status badges, hidden download controls, accessible empty states, and authorization-safe not-found behavior.
- [ ] T037 [US2] Implement authorized query, search, sorting, date filtering, project filtering, and lifecycle filtering services.
- [ ] T038 [US2] Implement authorized streaming download/preview with no direct filesystem or blob URL exposure.
- [ ] T039 [US2] Add personal library, project document panel, search/filter controls, status-safe rendering, download, and preview components.

## Phase 5: User Story 3 - Manage Lifecycle and Access (P2)

- [ ] T040 [P] [US3] Test metadata edit authorization, replacement versioning, superseded scan jobs, sharing, notifications, deletion, audit events, and retention cleanup.
- [ ] T041 [P] [US3] Test that replacements return to pending scan and old clean results cannot release new bytes; verify no manual release transition exists.
- [ ] T042 [P] [US3] Add bUnit coverage for edit, replace, share, and delete flows with inaccessible actions suppressed.
- [ ] T043 [US3] Implement authorized metadata updates and validation.
- [ ] T044 [US3] Implement replacement uploads with new version/hash/storage key, superseded jobs, and a fresh pending scan.
- [ ] T045 [US3] Implement authorized sharing, recipient permissions, notifications, immutable audit events, deletion, and retention-aware cleanup.
- [ ] T046 [US3] Add metadata edit, replacement, share, and delete confirmation UI.

## Phase 6: User Story 4 - Project and Dashboard Workflows (P2)

- [ ] T047 [P] [US4] Test task-context uploads, project association, team authorization, activity, counts, loading states, and suppression of unsafe documents.
- [ ] T048 [US4] Implement task association validation and authorized document workflow queries.
- [ ] T049 [US4] Add task attachment/upload controls using existing project/task authorization.
- [ ] T050 [US4] Add authorized recent-document activity and summary counts to dashboard and project views.

## Phase 7: Operations, Security, Accessibility, and Release

- [ ] T051 [P] Add administrator usage/reporting queries and UI with explicit authorization.
- [ ] T052 [P] Add quarantine/deleted-object retention cleanup respecting audit and retention settings.
- [ ] T053 [P] Add scan latency, retry, poison, stale-result, and failure metrics plus alerts for the Function and queue.
- [ ] T054 [P] Add security regression tests for private storage, no public paths, no queue secrets/bytes, IDOR protection, fail-closed behavior, and training-only authentication warnings.
- [ ] T055 [P] Add accessibility tests for labels, keyboard navigation, focus handling, status announcements, and responsive layout.
- [ ] T056 Document Azure deployment, managed identity roles, private storage, queue/poison alerts, retry settings, and secret-store configuration.
- [ ] T057 Document local filesystem, in-memory queue, fake scanner, offline validation, and optional Azure integration validation.
- [ ] T058 Update `README.md` with supported formats, 25 MB limit, asynchronous scanning, quarantine behavior, clean-release behavior, and no-manual-release policy.
- [ ] T059 Run restore, build, targeted tests, full tests, and the quickstart scenarios; record failures and remediation.
- [ ] T060 Review all changed pages/services against the constitution, especially authorization, IDOR protection, layer boundaries, accessibility, offline operation, and scan fail-closed behavior.

## Dependencies and Execution Order

1. Phase 1 has no feature dependencies.
2. Phase 2 depends on Phase 1 and blocks all user stories.
3. US1 depends on Phase 2 and is the MVP.
4. US2, US3, and US4 depend on the stable US1 document, authorization, and scan contracts.
5. Operations and release tasks depend on the selected user stories.

The Azure scanning dependency chain is:
`T006-T015` -> `T023-T025` -> `T026-T030` -> `T033` -> `T053`.

Within each phase, tasks marked `[P]` may run in parallel after their stated
dependencies are available.

## MVP Strategy

Deliver US1 first: local upload/quarantine, outbox dispatch, fake scanner,
idempotent result application, Azure Queue Storage adapter, Azure Functions
queue trigger, retries/poison handling, and end-to-end security tests. Add
browsing, lifecycle management, project integration, and operational hardening
incrementally after the MVP checkpoint passes.
