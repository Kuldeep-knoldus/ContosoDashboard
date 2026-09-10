using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ContosoDashboard.Data;
using ContosoDashboard.Services.Documents;
using ContosoDashboard.Services.Storage;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddOptions<DocumentManagementOptions>()
            .Bind(context.Configuration.GetSection("DocumentManagement"))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<DocumentManagementOptions>, ScanOptionsValidator>();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(context.Configuration.GetConnectionString("DefaultConnection")));
        services.AddScoped<IScanResultService, ScanResultService>();
        services.AddScoped<ScanWorkflowService>();
        services.AddSingleton<IScanEngine, FakeScanEngine>();
        services.AddSingleton<IFileStorageService>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DocumentManagementOptions>>();
            return string.Equals(options.Value.StorageProvider, "Azure", StringComparison.OrdinalIgnoreCase)
                ? new AzureBlobStorageService(options)
                : new LocalFileStorageService(options, new FunctionEnvironment(context.HostingEnvironment.ContentRootPath));
        });
    })
    .Build();

await host.RunAsync();

internal sealed class FunctionEnvironment(string root) : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "DocumentScanFunction";
    public string EnvironmentName { get; set; } = "Production";
    public string WebRootPath { get; set; } = root;
    public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    public string ContentRootPath { get; set; } = root;
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
}
