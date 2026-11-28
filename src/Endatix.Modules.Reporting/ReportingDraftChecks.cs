using System.Diagnostics.CodeAnalysis;
using Endatix.Core.UseCases.FormDrafts;
using Endatix.Framework.Modules;
using Endatix.Modules.Reporting.Configuration;
using Endatix.Modules.Reporting.Features.FormSchema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Endatix.Modules.Reporting;

/// <summary>Read-only checks require no reporting database, export services or jobs.</summary>
[Experimental("ENDATIX0007")]
public static class ReportingDraftChecks
{
    /// <summary>Register once in both full and read-only reporting hosts.</summary>
    public static void Register(EndatixModuleBuilder builder)
    {
        if (builder.Services.Any(d => d.ImplementationType == typeof(ColumnKeyCheck)))
        {
            return;
        }
        builder.AddOptions<ReportingOptions>(ReportingOptions.SECTION_NAME);
        builder.Services.AddSingleton<IValidateOptions<ReportingOptions>, ReportingQuestionLimitValidator>();
        builder.Services.AddSingleton<IFormDefinitionCheck, ColumnKeyCheck>();
        builder.Services.AddScoped<IFormDefinitionCheck, UnknownReferenceCheck>();
    }
}
