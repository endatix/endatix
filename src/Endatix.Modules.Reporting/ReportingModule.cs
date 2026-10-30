using Microsoft.Extensions.DependencyInjection.Extensions;
using Endatix.Modules.Audience.Contracts;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Endatix.Api.Infrastructure;
using Endatix.Framework.FeatureFlags;
using Endatix.Framework.Modules;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Modules.Reporting.Configuration;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.BackgroundJobs;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Features.Export.Capabilities;
using Endatix.Modules.Reporting.Features.Export.FormSchema;
using Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;
using Endatix.Modules.Reporting.Features.Export.Tabular;
using Endatix.Modules.Reporting.Features.ExportFormats;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Features.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.Personalization;
using Endatix.Modules.Reporting.Features.FormSchema.SubmitterProfile;
using Endatix.Modules.Reporting.Features.Outbox;
using Endatix.Modules.Reporting.Features.SubmissionViews;
using Endatix.Modules.Reporting.Persistence;
using Endatix.Modules.Reporting.Infrastructure;
using FastEndpoints;

namespace Endatix.Modules.Reporting;

/// <summary>
/// Reporting module (export read model, flattening, export configuration).
/// </summary>
public sealed class ReportingModule : IEndatixModule, IHasFeatureFlag, IHasDbMigrations, IHasFastEndpoints
{
    public static readonly ReportingModule Instance = new();

    private ReportingModule() { }

    public Assembly Assembly => typeof(ReportingModule).Assembly;

    public string FeatureFlag => FeatureFlags.ReportingModule;

    /// <summary>
    /// Applies reporting-specific FastEndpoints configuration (serializers, OpenAPI tags).
    /// Invoked by the host through <see cref="IHasFastEndpoints"/> when the module registers.
    /// </summary>
    public void ConfigureFastEndpoints(Config config) =>
        ReportingModuleEndpointConfiguration.Configure(config);

    public void ConfigureServices(EndatixModuleBuilder builder)
    {
        AddPersistence(builder);
        AddRepositories(builder.Services);
        AddExports(builder.Services);
        AddFormSchemaCompiler(builder.Services);
        builder.Services.AddScoped<IFormSchemaProcessor, FormSchemaProcessor>();
        builder.Services.AddScoped<FormSchemaCoverage>();
        builder.Services.AddScoped<IFormSchemaProvider, FormSchemaProvider>();
        builder.Services.AddScoped<IFormSchemaPersonalization, FormSchemaPersonalization>();
        builder.Services.AddScoped<ISubmissionFlatteningProcessor, SubmissionFlatteningProcessor>();
        builder.Services.AddScoped<ISubmissionBackfillProcessor, SubmissionBackfillProcessor>();
        builder.Services.AddReportingOutboxWork();
        builder.AddOptions<ReportingOptions>(ReportingOptions.SECTION_NAME);
    }

    private static void AddRepositories(IServiceCollection services)
    {
        services.AddScoped<IReportingUnitOfWork, ReportingUnitOfWork>();
        services.AddScoped<IFormSchemaRepository, FormSchemaRepository>();
        services.AddScoped<IFlattenedSubmissionRepository, FlattenedSubmissionRepository>();
        services.AddScoped<IReportingExportRepository, ReportingExportRepository>();
        services.AddScoped<ISubmissionViewRepository, SubmissionViewRepository>();
        services.AddScoped<SubmissionViewReader>();
    }

    // The configured submitter profile fields; the flatten appends their columns, and copies only those fields.
    private static void AddFormSchemaCompiler(IServiceCollection services)
    {
        services.AddSingleton(sp =>
            SubmitterProfileFields.From(sp.GetRequiredService<IOptions<ReportingOptions>>().Value));
        services.AddSingleton(sp =>
            new FormSchemaCompiler(submitterProfileFields: sp.GetRequiredService<SubmitterProfileFields>()));
    }

    private static void AddExports(IServiceCollection services)
    {
        services.AddScoped<IExportFormatRepository, ExportFormatRepository>();
        services.AddScoped<IExportMappingRepository, ExportMappingRepository>();
        services.AddScoped<IDefaultExportFormatsSeeder, DefaultExportFormatsSeeder>();
        services.AddSingleton<IExportCapabilityRegistry, ExportCapabilityRegistry>();
        services.AddSingleton<IColumnAliasTransformer>(_ => NativeColumnAliasTransformer._instance);
        services.AddSingleton<IColumnAliasTransformer>(_ => QuestionIndexAliasTransformer._instance);
        services.AddSingleton<IColumnAliasTransformerRegistry, ColumnAliasTransformerRegistry>();
        services.AddSingleton<ExportFormatSettingsParser>();
        services.AddSingleton<ExportFormatSettingsWriter>();
        // Exports read audience names here; the Audience module replaces the default when it is on.
        services.TryAddScoped<IAudiencePropertyCatalog, NoAudiencePropertyCatalog>();
        services.AddScoped<ExportSchemas>();
        services.AddScoped<IExportDataSource, TabularExportDataSource>();
        services.AddScoped<IExportDataSource, ShojiCodebookExportDataSource>();
        services.AddScoped<IExportDataSource, FormSchemaCodebookExportDataSource>();
    }

    private static void AddPersistence(EndatixModuleBuilder builder)
    {
        if (DatabaseProviderResolver.IsPostgreSql(builder.Configuration))
        {
            AddDbContext<ReportingPostgreSqlDbContext>(builder);
        }
        else
        {
            AddDbContext<ReportingSqlServerDbContext>(builder);
        }

        builder.Services.AddScoped<IReportingDbContext>(sp => sp.GetRequiredService<ReportingDbContextBase>());
    }

    // Consumers see the context only as IReportingDbContext or the base, so nothing downstream branches on the
    // provider. The base is the one registration that names the provider's context; ReportingUnitOfWork needs the
    // base because EfUnitOfWorkBase is constrained to DbContext.
    private static void AddDbContext<TContext>(EndatixModuleBuilder builder)
        where TContext : ReportingDbContextBase
    {
        builder.AddDbContextWithMigrations<TContext>(
            ReportingPersistence.ConfigureDbContextOptions,
            shouldMigrate: sp => sp.GetRequiredService<IOptions<ReportingOptions>>().Value.ApplyMigrationsAtStartup);
        builder.Services.AddScoped<ReportingDbContextBase>(sp => sp.GetRequiredService<TContext>());
    }
}
