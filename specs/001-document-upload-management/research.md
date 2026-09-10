# Research: Asynchronous Malware Scanning for Document Uploads

## Decision: Azure Queue Storage plus an Azure Functions queue trigger

**Decision:** Publish a compact scan-job message to Azure Queue Storage and
process it with an Azure Function queue trigger. Store the binary in private
quarantine storage; never place bytes in the queue.

**Rationale:** Queue delivery is durable and naturally supports visibility
timeouts, retry/dequeue counts, poison queues, and horizontal worker scale.
Functions provides a small independently deployable consumer while the web app
can acknowledge uploads without waiting for a scanner.

**Alternatives considered:** Service Bus was not selected because sessions,
topics, and enterprise messaging are unnecessary for this small workflow.
Synchronous scanning in the request was rejected because it increases timeout
risk and makes upload availability depend on a scanner. A background thread in
the web process was rejected because it is not durable across restarts.

## Decision: Outbox-backed, at-least-once delivery

**Decision:** Persist a `ScanJob` outbox row with the pending document in one
transaction, then dispatch it to the queue. Treat delivery and Function
execution as at-least-once and make result application idempotent.

**Rationale:** A database commit followed by a queue call can lose work if the
process stops between calls. The outbox closes that gap without requiring a
distributed transaction. An idempotency key plus document version/hash prevents
duplicate messages from releasing a file twice or applying a stale result.

**Alternatives considered:** Exactly-once processing was rejected because Azure
Queue Storage does not provide it end-to-end. A queue-only design was rejected
because a successful upload could exist without a durable job.

## Decision: Fail closed with automated bounded retries

**Decision:** Retry transient exceptions with queue visibility timeout and
bounded exponential backoff. After the dequeue limit, poison the message and
keep the document quarantined/`ScanFailed`; only an explicitly automated
rescan can create a new attempt.

**Rationale:** Security failures must not accidentally become availability
successes. Poison handling makes operational failures visible while preserving
the invariant that no unverified bytes are released.

**Alternatives considered:** Auto-release after a timeout was rejected because
absence of a result is not evidence of cleanliness. Infinite retries were
rejected because they hide systemic failures and exhaust resources.

## Decision: Provider-neutral scanner and local deterministic adapter

**Decision:** Use `IScanEngine`, `IScanJobQueue`, and `IScanResultService`
contracts. Local development uses an in-memory queue and deterministic fake
scanner/test fixtures; cloud deployments inject an Azure Queue client and
approved scanner implementation.

**Rationale:** The constitution requires offline operation and explicit
abstractions. The local adapter permits repeatable tests for clean, flagged,
timeout, malformed, and duplicate results without malware samples or a network.

**Alternatives considered:** Requiring a live Azure subscription locally was
rejected because it violates the offline-first constraint. Treating filename
extension validation as malware scanning was rejected because it cannot detect
content threats.

## Decision: State machine and authorization are deny-by-default

**Decision:** Upload creates `PendingScan` + uploader-only visibility. A clean
result for the current version is the only automatic transition to `Active`.
Flagged, failed, deleted, stale, and pending states cannot be downloaded,
previewed, searched by other users, shared, or included in project lists.

**Rationale:** This directly preserves the clarified behavior and provides one
central predicate for every access path, preventing a UI-only quarantine check.

**Alternatives considered:** A boolean `IsScanned` flag was rejected because it
cannot represent retries, stale versions, or a flagged file that later receives
a clean rescan.

## Azure and local operational assumptions

- Azure storage accounts, queues, private blobs, managed identity, and
  monitoring are deployment concerns; no live Azure resource is required for
  local validation.
- Function retry/dequeue configuration must be explicit and covered by
  contract tests. Poison queue alerts are required in production.
- The scanner result is treated as untrusted input until schema, job key,
  content hash, and current version are validated.
- Retention cleanup must delete quarantined bytes only after audit/retention
  rules permit it; cleanup cannot make an unsafe file visible.

All research decisions are resolved; no clarification items remain.
