using Microsoft.Extensions.Options;

namespace ContosoDashboard.Services.Documents;

public sealed class ScanOptionsValidator : IValidateOptions<DocumentManagementOptions>
{
    public ValidateOptionsResult Validate(string? name, DocumentManagementOptions options)
    {
        if (options.MaxFileSizeBytes <= 0 ||
            options.ScanMaxAttempts <= 0 ||
            options.ScanVisibilityTimeoutSeconds <= 0 ||
            string.IsNullOrWhiteSpace(options.FileStorageRoot))
        {
            return ValidateOptionsResult.Fail("DocumentManagement settings must specify positive limits and a storage root.");
        }

        if (string.Equals(options.QueueProvider, "Azure", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(options.AzureStorageConnectionString) &&
            string.IsNullOrWhiteSpace(options.AzureStorageAccountUri))
        {
            return ValidateOptionsResult.Fail("DocumentManagement Azure providers require AzureStorageConnectionString or AzureStorageAccountUri.");
        }

        if (string.Equals(options.StorageProvider, "Azure", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(options.AzureStorageConnectionString) &&
            string.IsNullOrWhiteSpace(options.AzureStorageAccountUri))
        {
            return ValidateOptionsResult.Fail("DocumentManagement Azure providers require AzureStorageConnectionString or AzureStorageAccountUri.");
        }

        return ValidateOptionsResult.Success;
    }
}
