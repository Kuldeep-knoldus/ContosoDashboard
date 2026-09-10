using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Data;
using ContosoDashboard.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;
using ContosoDashboard.Services.Documents;
using ContosoDashboard.Services.Storage;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddOptions<DocumentManagementOptions>()
    .Bind(builder.Configuration.GetSection("DocumentManagement"))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<DocumentManagementOptions>, ScanOptionsValidator>();

// Add authentication state provider for Blazor
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();

// Configure Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure Mock Authentication (Cookie-based for training purposes)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

// Add authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Employee", policy => policy.RequireRole("Employee", "TeamLead", "ProjectManager", "Administrator"));
    options.AddPolicy("TeamLead", policy => policy.RequireRole("TeamLead", "ProjectManager", "Administrator"));
    options.AddPolicy("ProjectManager", policy => policy.RequireRole("ProjectManager", "Administrator"));
    options.AddPolicy("Administrator", policy => policy.RequireRole("Administrator"));
});

// Register application services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IDocumentAuthorization, DocumentAuthorization>();
builder.Services.AddScoped<IScanResultService, ScanResultService>();
builder.Services.AddScoped<IScanJobDispatcher, ScanJobDispatcher>();
builder.Services.AddScoped<ScanWorkflowService>();
builder.Services.AddHostedService<ScanOutboxBackgroundService>();
builder.Services.AddSingleton<IScanEngine, FakeScanEngine>();
builder.Services.AddSingleton<IScanJobQueue>(sp =>
{
    var options = sp.GetRequiredService<IOptions<DocumentManagementOptions>>().Value;
    return string.Equals(options.QueueProvider, "Azure", StringComparison.OrdinalIgnoreCase)
        ? new AzureQueueScanJobQueue(Options.Create(options))
        : new InMemoryScanJobQueue();
});
builder.Services.AddSingleton<IFileStorageService>(sp =>
{
    var options = sp.GetRequiredService<IOptions<DocumentManagementOptions>>().Value;
    return string.Equals(options.StorageProvider, "Azure", StringComparison.OrdinalIgnoreCase)
        ? new AzureBlobStorageService(Options.Create(options))
        : new LocalFileStorageService(Options.Create(options), sp.GetRequiredService<IWebHostEnvironment>());
});

// Add HttpContextAccessor for accessing user claims
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.EnsureCreated();
        EnsureDocumentWorkflowSchema(context);
    }

    
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred creating the database.");
    }
}

static void EnsureDocumentWorkflowSchema(ApplicationDbContext context)
{
    context.Database.ExecuteSqlRaw("""
            IF OBJECT_ID(N'[Documents]', N'U') IS NULL
            BEGIN
                CREATE TABLE [Documents] (
                    [DocumentId] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Documents] PRIMARY KEY,
                    [Title] nvarchar(255) NOT NULL,
                    [Category] nvarchar(100) NOT NULL,
                    [Description] nvarchar(2000) NULL,
                    [Tags] nvarchar(1000) NULL,
                    [UploaderId] int NOT NULL,
                    [ProjectId] int NULL,
                    [TaskId] int NULL,
                    [OriginalFileName] nvarchar(255) NOT NULL,
                    [StoragePath] nvarchar(512) NOT NULL,
                    [ContentType] nvarchar(150) NOT NULL,
                    [FileSizeBytes] bigint NOT NULL,
                    [ContentHash] nvarchar(64) NOT NULL,
                    [VersionNumber] int NOT NULL,
                    [LifecycleStatus] int NOT NULL,
                    [Visibility] int NOT NULL,
                    [ScanStatus] int NOT NULL,
                    [UploadedUtc] datetime2 NOT NULL,
                    [UpdatedUtc] datetime2 NOT NULL,
                    [ReleasedUtc] datetime2 NULL,
                    [LastAccessedUtc] datetime2 NULL
                );
                CREATE INDEX [IX_Documents_LifecycleStatus_ScanStatus]
                    ON [Documents] ([LifecycleStatus], [ScanStatus]);
                CREATE INDEX [IX_Documents_DocumentId_VersionNumber]
                    ON [Documents] ([DocumentId], [VersionNumber]);
            END
            """);

        context.Database.ExecuteSqlRaw("""
            IF OBJECT_ID(N'[ScanJobs]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ScanJobs] (
                    [ScanJobId] uniqueidentifier NOT NULL CONSTRAINT [PK_ScanJobs] PRIMARY KEY,
                    [DocumentId] int NOT NULL,
                    [VersionNumber] int NOT NULL,
                    [ContentHash] nvarchar(64) NOT NULL,
                    [StoragePath] nvarchar(512) NOT NULL,
                    [IdempotencyKey] nvarchar(128) NOT NULL,
                    [Attempt] int NOT NULL,
                    [Status] int NOT NULL,
                    [CreatedUtc] datetime2 NOT NULL,
                    [DispatchedUtc] datetime2 NULL,
                    [LeaseUtc] datetime2 NULL,
                    [CompletedUtc] datetime2 NULL,
                    [LastError] nvarchar(max) NULL,
                    CONSTRAINT [UQ_ScanJobs_IdempotencyKey] UNIQUE ([IdempotencyKey]),
                    CONSTRAINT [FK_ScanJobs_Documents] FOREIGN KEY ([DocumentId])
                        REFERENCES [Documents] ([DocumentId])
                );
            END
            """);

        context.Database.ExecuteSqlRaw("""
            IF OBJECT_ID(N'[ScanAttempts]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ScanAttempts] (
                    [ScanAttemptId] uniqueidentifier NOT NULL CONSTRAINT [PK_ScanAttempts] PRIMARY KEY,
                    [ScanJobId] uniqueidentifier NOT NULL,
                    [AttemptNumber] int NOT NULL,
                    [StartedUtc] datetime2 NOT NULL,
                    [CompletedUtc] datetime2 NULL,
                    [Result] int NOT NULL,
                    [ScannerCode] nvarchar(100) NULL,
                    [ScannerVersion] nvarchar(100) NULL,
                    [ResultRecordedUtc] datetime2 NULL,
                    [ErrorSummary] nvarchar(2000) NULL,
                    [ResultDigest] nvarchar(max) NULL,
                    CONSTRAINT [UQ_ScanAttempts_JobAttempt]
                        UNIQUE ([ScanJobId], [AttemptNumber]),
                    CONSTRAINT [FK_ScanAttempts_ScanJobs] FOREIGN KEY ([ScanJobId])
                        REFERENCES [ScanJobs] ([ScanJobId])
                );
            END
            """);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    // Use HSTS even in development for training purposes
    app.UseHsts();
}

// Add security headers
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    
    // Content Security Policy for Blazor Server
    context.Response.Headers["Content-Security-Policy"] = 
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
        "font-src 'self' https://cdn.jsdelivr.net; " +
        "img-src 'self' data: https:; " +
        "connect-src 'self' wss: ws:;";
    
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Enable authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
