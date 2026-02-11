using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Exporting.Transformers;
using Endatix.Infrastructure.Storage;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Setup for external storage.
/// </summary>
public static class StorageSetup
{
    /// <summary>
    /// Adds external storage options and Azure Blob provider options for export URL rewriting.
    /// </summary>
    public static IServiceCollection AddExternalStorage(this IServiceCollection services)
    {
        services.AddOptions<StorageOptions>()
            .BindConfiguration(StorageOptions.SectionName);

        // Add Azure Blob storage provider options. To be encapsulated with Storage provider registry later.
        services.AddOptions<AzureBlobStorageProviderOptions>()
            .BindConfiguration($"{StorageOptions.SectionName}:Providers:AzureBlob")
            .ValidateOnStart();

        services.AddSingleton<IJsonValueTransformer<SubmissionExportRow>, StorageUrlRewriteTransformer>();

        return services;
    }
}