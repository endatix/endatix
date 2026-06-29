using Endatix.Infrastructure.Exporting.Transformers;
using Endatix.Infrastructure.Features.Submissions;
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

        services.AddOptions<AzureBlobStorageProviderOptions>()
            .BindConfiguration($"{StorageOptions.SectionName}:Providers:AzureBlob")
            .ValidateOnStart();

        services.AddSingleton<SubmissionFileUrlPolicy>();

        services.AddHttpClient(SubmissionFileFetchHttpClient.Name)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
            });

        services.AddExportTransformer<StorageUrlRewriteTransformer>();
        services.AddExportTransformer<LargeValuePlaceholderTransformer>();

        return services;
    }
}
