---
description: "Dependency-ordered implementation tasks for document upload management"
---

# Tasks: Document Upload and Management

**Input**: `specs/001-document-upload-management/`

**Stack**: ASP.NET Core 8, C# 12, Blazor Server, EF Core, SQL Server LocalDB,
local filesystem storage, Azure Queue Storage, Azure Functions isolated worker,
xUnit, and bUnit.

## Phase 1: Setup

- [X] T001 Add Azure.Storage.Queues and Azure Functions isolated-worker dependencies to `ContosoDashboard/ContosoDashboard.csproj` and create `DocumentScanFunction/DocumentScanFunction.csproj`.
- [X] T002 [P] Create the unit and integration test projects and add them to `ContosoDashboard.sln`.
- [ ] T065 [P] Create the component and contract test projects and add them to `ContosoDashboard.sln`.
- [X] T003 [P] Add document storage, scan queue, retry, poison queue, and provider settings to `ContosoDashboard/appsettings.json` and `appsettings.Development.json`.
- [X] T004 [P] Add Azure Function host, local queue, and local settings templates under `DocumentScanFunction/`.
- [X] T005 Document local/offline setup, LocalDB prerequisites, Azure emulator options, and test commands in `docs/document-management-development.md`.

## Phase 2: Foundational Infrastructure

- [X] T006 Create the `Document` entity with lifecycle, visibility, scan status, version, hash, and audit fields in `ContosoDashboard/Models/`.
- [X] T007 Create `ScanJob` and `ScanAttempt` entities with attempt, retry, idempotency, and result fields in `ContosoDashboard/Models/`.
- [X] T008 Configure EF Core relationships, constraints, unique idempotency keys, and indexes in `ContosoDashboard/Data/ApplicationDbContext.cs`.
- [X] T009 Create and validate the EF Core migration for document and scan persistence in `ContosoDashboard/Data/Migrations/`.
- [X] T010 Define provider-neutral upload and document DTO contracts in `ContosoDashboard/Services/Documents/DocumentContracts.cs`.
- [X] T011 Define scan-job, scan-result, verdict, queue, scanner, and result-application contracts in `ContosoDashboard/Services/Documents/DocumentContracts.cs`.
- [X] T012 Define `IDocumentService`, `IFileStorageService`, and document authorization interfaces without leaking provider types.
- [X] T013 Define `IScanJobQueue`, `IScanEngine`, `IScanResultService`, and dispatcher interfaces without leaking Azure SDK types.
- [X] T014 Implement generated private storage keys, filename/content validation, and path-traversal protection in `ContosoDashboard/Services/Storage/`.
- [X] T015 Implement streaming copy, SHA-256 hashing, and storage cleanup in `ContosoDashboard/Services/Storage/`.
- [X] T016 Implement centralized deny-by-default authorization predicates for lifecycle visibility and uploader/project/share access.
- [X] T017 Add authorization checks for list, detail, download, preview, and status operations using the centralized predicates.
- [X] T018 Add strongly typed scan/storage options and startup validation.
- [X] T019 Register local filesystem storage, in-memory queue, and deterministic fake scanner providers.
- [X] T020 Add Azure queue/blob provider selection and clear startup errors for incomplete cloud configuration.
- [X] T021 Implement transactional scan-job outbox creation in `DocumentService`.
- [X] T022 Implement retrying outbox dispatch in `ScanJobDispatcher`.
- [X] T023 Add structured audit event constants and recording for queued, started, clean, and flagged results.
- [X] T024 Add retry, poison, and ignored-result audit events and structured logging.
- [ ] T025 [P] Add the LocalDB and seeded-user/project integration fixture.
- [ ] T026 [P] Add fake authentication, storage, queue, and scanner fixtures.

## Phase 3: User Story 1 - Upload and Scan (P1 MVP)

**Goal**: Upload supported files into private quarantine, enqueue asynchronous scanning,
and release only current versions with a clean scan.

### Tests

- [X] T066 [P] [US1] Test required metadata, supported PDF/DOC/DOCX/XLS/XLSX/PPT/PPTX formats, content types, unsafe names, path traversal, and the 25 MB limit.
- [X] T067 [P] [US1] Test pending, processing, clean, active, flagged, quarantined, retrying, failed, deleted, and replaced state transitions.
- [X] T068 [P] [US1] Test that only a current clean result changes visibility to active/authorized.
- [X] T069 [P] [US1] Test duplicate idempotency keys, mismatched hash/version, stale results, deleted documents, replaced versions, and malformed messages.
- [X] T070 [P] [US1] Test transactional upload/outbox persistence, compensating storage cleanup, and dispatcher retries.
- [X] T071 [P] [US1] Test schema-versioned queue envelopes contain no bytes, secrets, or user tokens and remain within message limits.

### Implementation

- [X] T027 [US1] Implement upload metadata and file validation, including format, size, filename, and content-type rules.
- [X] T028 [US1] Implement private quarantine storage, hashing, and cleanup for an upload.
- [X] T029 [US1] Implement `Document` creation, authorization, and scan-job outbox creation for a valid upload.
- [X] T030 [US1] Implement scan-result validation for job key, version, hash, provenance, and current document state.
- [X] T031 [US1] Implement clean, flagged, failed, duplicate, and stale scan-result state transitions.
- [X] T032 [US1] Implement bounded retry and visibility-timeout handling for scan jobs.
- [X] T033 [US1] Implement poison-message handling, uploader-only retention, and terminal failure auditing.
- [X] T034 [US1] Implement the Azure Queue Storage adapter for compact schema-versioned `ScanJobMessage` JSON.
- [X] T035 [US1] Implement the private Azure Blob Storage adapter using generated keys and managed identity.
- [X] T036 [US1] Implement the Azure Function trigger to deserialize and validate a `ScanJobMessage`.
- [X] T037 [US1] Implement Function scanner invocation, result publication/application, and successful/terminal acknowledgement behavior.
- [X] T038 [US1] Configure Function retry/dequeue limits, visibility timeout, poison queue routing, and non-secret logging.
- [X] T039 [US1] Add Azure adapter and Function contract tests for retry, terminal failure, poison routing, and acknowledgement.
- [X] T040 [US1] Add contract tests for idempotency, stale results, and no-secret/no-bytes queue guarantees.
- [X] T041 [US1] Add upload form UI with accessible metadata labels and per-file validation errors.
- [X] T042 [US1] Add upload status UI for pending, flagged, and failed states with downloads disabled.
- [X] T043 [US1] Add uploader-only document status/detail components that never expose quarantined bytes or storage paths.
- [X] T044 [US1] Add integration coverage for pending privacy, clean release, flagged files, and unavailable scanner.
- [X] T045 [US1] Add integration coverage for retries, poison messages, duplicate/stale delivery, and interrupted multi-file uploads.

**Checkpoint**: The upload and asynchronous scan workflow is independently testable
with local adapters and Azure Function/Queue Storage adapters.

## Phase 4: User Story 2 - Browse and Find Documents (P1)

- [ ] T046 [P] [US2] Test authorized filtering/search by title, description, tags, uploader, project, category, and date.
- [ ] T047 [P] [US2] Test exclusion of inaccessible lifecycle states and guessed-ID IDOR protection.
- [ ] T048 [P] [US2] Test personal/project/shared libraries, downloads, and previews.
- [ ] T072 [P] [US2] Add bUnit coverage for filters, status badges, hidden download controls, accessible empty states, and authorization-safe not-found behavior.
- [ ] T049 [US2] Implement authorized document query, sorting, date filtering, project filtering, and lifecycle filtering.
- [ ] T050 [US2] Implement title, description, tag, and uploader search over authorized documents.
- [ ] T051 [US2] Implement authorized streaming download and preview without direct storage URL exposure.
- [ ] T052 [US2] Add personal library and search/filter UI.
- [ ] T053 [US2] Add project document panel, status-safe rendering, download, and preview controls.

## Phase 5: User Story 3 - Manage Lifecycle and Access (P2)

- [ ] T054 [P] [US3] Test metadata edit authorization, replacement versioning, and superseded scan jobs.
- [ ] T055 [P] [US3] Test sharing, notifications, deletion, audit events, and retention cleanup.
- [ ] T073 [P] [US3] Test that replacements return to pending scan and old clean results cannot release new bytes; verify no manual release transition exists.
- [ ] T074 [P] [US3] Add bUnit coverage for edit, replace, share, and delete flows with inaccessible actions suppressed.
- [ ] T058 [US3] Implement authorized metadata updates and validation.
- [ ] T059 [US3] Implement replacement uploads with a new version, hash, and storage key.
- [ ] T060 [US3] Supersede old scan jobs and enqueue a fresh pending scan for replacements.
- [ ] T061 [US3] Implement authorized sharing, recipient permissions, and notifications.
- [ ] T062 [US3] Implement immutable sharing/deletion audit events and retention-aware deletion cleanup.
- [ ] T063 [US3] Add metadata edit and replacement UI.
- [ ] T064 [US3] Add share and delete confirmation UI.

## Phase 6: User Story 4 - Project and Dashboard Workflows (P2)

- [ ] T075 [P] [US4] Test task-context uploads, project association, team authorization, activity, counts, loading states, and suppression of unsafe documents.
- [ ] T076 [US4] Implement task association validation and authorized document workflow queries.
- [ ] T077 [US4] Add task attachment/upload controls using existing project/task authorization.
- [ ] T078 [US4] Add authorized recent-document activity and summary counts to dashboard and project views.

## Phase 7: Operations, Security, Accessibility, and Release

- [ ] T079 [P] Add administrator usage/reporting queries with explicit authorization.
- [ ] T080 [P] Add administrator reporting UI for the authorized reporting queries.
- [ ] T081 [P] Add quarantine/deleted-object retention cleanup respecting audit and retention settings.
- [ ] T082 [P] Add scan latency, retry, poison, stale-result, and failure metrics.
- [ ] T083 [P] Document queue/function alerts and operational thresholds for those metrics.
- [ ] T084 [P] Add storage, queue-message, IDOR, and fail-closed security regression tests.
- [ ] T085 [P] Add training-only authentication warning coverage and security configuration checks.
- [ ] T086 [P] Add accessibility tests for labels, keyboard navigation, focus handling, and status announcements.
- [ ] T087 [P] Add responsive-layout and inaccessible-action component tests.
- [ ] T088 Document Azure deployment, managed identity roles, private storage, queue/poison alerts, retry settings, and secret-store configuration.
- [ ] T089 Document local filesystem, in-memory queue, fake scanner, offline validation, and optional Azure integration validation.
- [ ] T090 Update `README.md` with supported formats, 25 MB limit, asynchronous scanning, quarantine behavior, clean-release behavior, and no-manual-release policy.
- [ ] T091 Run restore and build validation; record failures and remediation.
- [ ] T092 Run targeted and full test suites plus the quickstart scenarios; record failures and remediation.
- [ ] T093 Review changed pages/services against the constitution, authorization, IDOR protection, layer boundaries, accessibility, offline operation, and scan fail-closed behavior.

## Dependencies and Execution Order

1. Phase 1 has no feature dependencies.
2. Phase 2 depends on Phase 1 and blocks all user stories.
3. US1 depends on Phase 2 and is the MVP.
4. US2, US3, and US4 depend on the stable US1 document, authorization, and scan contracts.
5. Operations and release tasks depend on the selected user stories.

The Azure scanning dependency chain is:
`T006-T026` -> `T027-T033` -> `T034-T040` -> `T044-T045` -> `T082-T083`.

Within each phase, tasks marked `[P]` may run in parallel after their stated
dependencies are available.

## MVP Strategy

Deliver US1 first: local upload/quarantine, outbox dispatch, fake scanner,
idempotent result application, Azure Queue Storage adapter, Azure Functions
queue trigger, retries/poison handling, and end-to-end security tests. Add
browsing, lifecycle management, project integration, and operational hardening
incrementally after the MVP checkpoint passes.
