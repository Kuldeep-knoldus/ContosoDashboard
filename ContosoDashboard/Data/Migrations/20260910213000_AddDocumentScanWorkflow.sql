-- Apply with the normal EF migration pipeline in environments that use migrations.
-- Development uses EnsureCreated; this script documents the US1 schema additions.
CREATE TABLE Documents (
    DocumentId int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Title nvarchar(255) NOT NULL,
    Category nvarchar(100) NOT NULL,
    Description nvarchar(2000) NULL,
    Tags nvarchar(1000) NULL,
    UploaderId int NOT NULL,
    ProjectId int NULL,
    TaskId int NULL,
    OriginalFileName nvarchar(255) NOT NULL,
    StoragePath nvarchar(512) NOT NULL,
    ContentType nvarchar(150) NOT NULL,
    FileSizeBytes bigint NOT NULL,
    ContentHash nvarchar(64) NOT NULL,
    VersionNumber int NOT NULL,
    LifecycleStatus int NOT NULL,
    Visibility int NOT NULL,
    ScanStatus int NOT NULL,
    UploadedUtc datetime2 NOT NULL,
    UpdatedUtc datetime2 NOT NULL,
    ReleasedUtc datetime2 NULL,
    LastAccessedUtc datetime2 NULL
);
CREATE TABLE ScanJobs (
    ScanJobId uniqueidentifier NOT NULL PRIMARY KEY,
    DocumentId int NOT NULL,
    VersionNumber int NOT NULL,
    ContentHash nvarchar(64) NOT NULL,
    StoragePath nvarchar(512) NOT NULL,
    IdempotencyKey nvarchar(128) NOT NULL,
    Attempt int NOT NULL,
    Status int NOT NULL,
    CreatedUtc datetime2 NOT NULL,
    DispatchedUtc datetime2 NULL,
    LeaseUtc datetime2 NULL,
    CompletedUtc datetime2 NULL,
    LastError nvarchar(max) NULL,
    CONSTRAINT UQ_ScanJobs_IdempotencyKey UNIQUE (IdempotencyKey),
    CONSTRAINT FK_ScanJobs_Documents FOREIGN KEY (DocumentId) REFERENCES Documents(DocumentId)
);
CREATE TABLE ScanAttempts (
    ScanAttemptId uniqueidentifier NOT NULL PRIMARY KEY,
    ScanJobId uniqueidentifier NOT NULL,
    AttemptNumber int NOT NULL,
    StartedUtc datetime2 NOT NULL,
    CompletedUtc datetime2 NULL,
    Result int NOT NULL,
    ScannerCode nvarchar(100) NULL,
    ScannerVersion nvarchar(100) NULL,
    ResultRecordedUtc datetime2 NULL,
    ErrorSummary nvarchar(2000) NULL,
    ResultDigest nvarchar(max) NULL,
    CONSTRAINT UQ_ScanAttempts_JobAttempt UNIQUE (ScanJobId, AttemptNumber),
    CONSTRAINT FK_ScanAttempts_ScanJobs FOREIGN KEY (ScanJobId) REFERENCES ScanJobs(ScanJobId)
);
