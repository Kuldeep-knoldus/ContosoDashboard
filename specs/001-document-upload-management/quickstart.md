# Quickstart Validation Guide

This guide validates the asynchronous scan design without requiring Azure.

## Prerequisites and local configuration

- .NET 8 SDK and SQL Server LocalDB
- Repository root `D:\TrainingProjects\ContosoDashboard`
- Seeded training users (mock authentication is training-only)
- Configure local providers: filesystem storage, in-memory/fake queue, and
  deterministic fake scanner. Do not set Azure credentials for the offline path.

Run:

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project ContosoDashboard\ContosoDashboard.csproj
```

## End-to-end scenarios

### 1. Pending upload is private

Upload a valid PDF or Office file under 25 MB. Before processing the fake
queue, confirm the response is successful with `PendingScan`, the uploader can
see status/metadata, and a second seeded user cannot find, preview, share, or
download it.

Expected: bytes are in private quarantine storage; no active-list result exists.

### 2. Clean result releases automatically

Process the queued message with the fake scanner returning `Clean`. Refresh as
the uploader and an authorized project/share recipient.

Expected: exactly one release, `Active/Clean`, authorized users can download;
the audit trail records queue, scan, and release events.

### 3. Flagged and failed results remain quarantined

Return `Flagged`, then separately simulate scanner timeout, storage failure, and
malformed result. Exercise configured retries and the poison threshold.

Expected: `Flagged` or `ScanFailed`, uploader-only status, blocked download and
preview, hidden from every other user, and no automatic release. Manual admin
release must not be present.

### 4. Duplicate and stale delivery is harmless

Deliver the same message twice and submit a clean result twice. Replace the
document before delivering the old result, then deliver the old clean result.

Expected: duplicate is acknowledged without a second transition; stale hash or
version cannot release the replacement.

### 5. Authorization and IDOR checks

Use an unauthorized user's guessed document ID for detail, download, project
list, search, share, and preview endpoints/components.

Expected: not-found/denied behavior with no metadata, path, or status leakage.

### 6. Validation and recovery

Attempt unsupported extensions, unsafe names, oversized files, interrupted
uploads, and multiple files where one fails. Confirm valid files are not
corrupted and failed files create no active record. Run an automated subsequent
rescan of a quarantined file with a clean result.

Expected: only the current clean scan releases; no extension-only shortcut.

## Optional Azure contract validation

When an Azure test subscription is intentionally available, configure
`ScanQueueName`, poison queue, visibility timeout, max dequeue count, private
storage, and managed identity. Publish a test envelope with no secrets or
bytes, verify Function retry/poison behavior and logs, then remove all test
resources. This is not required for local CI.

## Required automated tests

- Unit: state transitions, validation, deny-by-default predicate, no manual
  release, hash/version checks, duplicate/stale results.
- Integration: transactional outbox, dispatcher retry, fake queue delivery,
  poison handling, storage cleanup, and EF persistence.
- Component: accessible pending/flagged status messaging and hidden download
  controls for unauthorized users.
- Contract: queue JSON schema, message size/no-secret rule, Azure adapter
  acknowledgement and retry mapping.

See [plan.md](./plan.md), [data-model.md](./data-model.md), and
[contracts/document-service-contract.md](./contracts/document-service-contract.md)
for the design contracts.
