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
public sealed class ReportingModule : IEndatixModule, IHasFeatureFlag, IHasDbMigrations
{
    public static readonly ReportingModule Instance = new();

    private ReportingModule() { }

    public Assembly Assembly => typeof(ReportingModule).Assembly;

    public string FeatureFlag => FeatureFlags.ReportingModule;

    public void ConfigureServices(EndatixModuleBuilder builder)
    {
        builder.AddDbContextWithMigrations<ReportingDbContext>(
            ReportingPersistence.ConfigureDbContextOptions,
            shouldMigrate: sp =>
            {
                var options = sp.GetService<IOptions<ReportingOptions>>();
                return options is null || options.Value.ApplyMigrationsAtStartup;
            });

        builder.Services.AddScoped<IReportingUnitOfWork, ReportingUnitOfWork>();
        builder.AddOptions<ReportingOptions>(ReportingOptions.SECTION_NAME);
    }
}
