# Document management development

The US1 upload and scan workflow runs offline by default:

- ASP.NET Core stores metadata in the configured SQL Server database.
- Local quarantine storage is under `App_Data\quarantine`, outside `wwwroot`.
- `InMemoryScanJobQueue` and `FakeScanEngine` are used for local development and tests.
- The fake scanner returns `Flagged` when the content contains the `EICAR` marker; otherwise it returns `Clean`.

Install SQL Server LocalDB (or point `ConnectionStrings:DefaultConnection` at a
SQL Server instance) before starting the web app. Azure Queue/Blob adapters are
optional; Azurite can be used locally by setting the Azure connection string to
`UseDevelopmentStorage=true` and selecting the Azure providers. Never commit
real connection strings or tokens.

Set `DocumentManagement:QueueProvider` or `StorageProvider` to `Azure` only when
the corresponding Azure Storage connection string and queue/container are
configured through environment variables or a secret store. Queue payloads
contain identifiers and hashes only, never file bytes or user tokens.

## Commands

```powershell
dotnet restore ContosoDashboard.slnx
dotnet test ContosoDashboard.Tests\ContosoDashboard.Tests.csproj
dotnet build DocumentScanFunction\DocumentScanFunction.csproj
```

The Azure Functions worker uses `host.json` to bound retries and route messages
that exceed `maxDequeueCount` to the platform poison queue.
