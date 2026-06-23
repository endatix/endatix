using System.Reflection;
using Microsoft.AspNetCore.Authorization;
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
/// Assembly anchor for the Reporting module (export read model, flattening, export configuration).
/// </summary>
public static class ReportingModule
{
    public static readonly IEndatixModule Instance = new ReportingModuleRegistration();

    public static Assembly Assembly => typeof(ReportingModule).Assembly;

    public const string ReportingPolicy = "ReportingModule";

    public static AuthorizationPolicyBuilder RequireReportingAccess(this AuthorizationPolicyBuilder builder) =>
        builder.RequireAuthenticatedUser();

    private sealed class ReportingModuleRegistration : IEndatixModule, IHasFeatureFlag
    {
        public Assembly Assembly => ReportingModule.Assembly;

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
}
