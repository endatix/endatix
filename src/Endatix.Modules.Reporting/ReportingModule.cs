using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Endatix.Framework.FeatureFlags;
using Endatix.Framework.Modules;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Reporting.Configuration;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Persistence;

namespace Endatix.Modules.Reporting;

/// <summary>
/// Reporting module (export read model, flattening, export configuration).
/// </summary>
public sealed class ReportingModule : IEndatixModule, IHasFeatureFlag
{
    public static readonly ReportingModule Instance = new();

    private ReportingModule() { }

    public Assembly Assembly => typeof(ReportingModule).Assembly;

    public string FeatureFlag => FeatureFlags.ReportingModule;

    public void ConfigureServices(EndatixModuleBuilder builder)
    {
        builder.AddDbContextWithMigrations<ReportingDbContext>(
            opts =>
            {
                opts.Schema = ReportingPersistence.Schema;
                opts.MigrationsAssembly = typeof(ReportingDbContext).Assembly.GetName().Name!;
                opts.PostgreSqlMigrationsNamespace = ReportingPersistence.PostgreSqlMigrationsNamespace;
                opts.SqlServerMigrationsNamespace = ReportingPersistence.SqlServerMigrationsNamespace;
            },
            shouldMigrate: sp =>
            {
                var options = sp.GetService<IOptions<ReportingOptions>>();
                return options is null || options.Value.ApplyMigrationsAtStartup;
            });

        builder.Services.AddScoped<IReportingUnitOfWork, ReportingUnitOfWork>();
        builder.AddOptions<ReportingOptions>(ReportingOptions.SECTION_NAME);
    }
}
