using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Exporting;
using Endatix.Infrastructure.Features.Submissions;
using Endatix.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Builders;

/// <summary>
/// Builder for configuring Endatix data infrastructure.
/// </summary>
public class InfrastructureDataBuilder
{
    private readonly InfrastructureBuilder _parentBuilder;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the InfrastructureDataBuilder class.
    /// </summary>
    /// <param name="parentBuilder">The parent infrastructure builder.</param>
    internal InfrastructureDataBuilder(InfrastructureBuilder parentBuilder)
    {
        _parentBuilder = parentBuilder;
        _logger = parentBuilder.LoggerFactory.CreateLogger<InfrastructureDataBuilder>();
    }

    internal IServiceCollection Services => _parentBuilder.Services;

    /// <summary>
    /// Configures data infrastructure with default settings.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public InfrastructureDataBuilder UseDefaults()
    {
        LogSetupInfo("Configuring data infrastructure with default settings");

        Services.AddHybridCache();
        Services.AddHttpContextAccessor();
        
        Services.AddSingleton<IIdGenerator<long>, SnowflakeIdGenerator>();
        Services.AddScoped<IUnitOfWork, AppUnitOfWork>();
        Services.AddSingleton<EfCoreValueGeneratorFactory>();
        Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        Services.AddScoped<IFormsRepository, FormsRepository>();
        Services.AddScoped<IExporterFactory, ExporterFactory>();
        Services.AddScoped<IExporter<SubmissionExportRow>, SubmissionCsvExporter>();
        Services.AddScoped<ISubmissionFileExtractor, SubmissionFileExtractor>();
        Services.AddSingleton<DataSeeder>();

        LogSetupInfo("Data infrastructure configured successfully");
        return this;
    }

    /// <summary>
    /// Configures data options.
    /// </summary>
    /// <param name="configure">Action to configure data options.</param>
    /// <returns>The builder for chaining.</returns>
    public InfrastructureDataBuilder Configure(Action<DataOptions> configure)
    {
        LogSetupInfo("Configuring data options");
        _parentBuilder.Services.Configure(configure);
        return this;
    }

    /// <summary>
    /// Builds and returns the parent infrastructure builder.
    /// </summary>
    /// <returns>The parent infrastructure builder.</returns>
    public InfrastructureBuilder Build() => _parentBuilder;

    /// <summary>
    /// Logs setup information with a consistent prefix.
    /// </summary>
    /// <param name="message">The message to log.</param>
    private void LogSetupInfo(string message)
    {
        _logger.LogDebug("[Data Setup] {Message}", message);
    }
}