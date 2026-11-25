using System.Diagnostics.CodeAnalysis;
using Endatix.Core.UseCases.FormDefinitions;
using Endatix.Core.UseCases.FormDefinitions.GetFields;
using Endatix.Core.UseCases.Forms;
using Endatix.Core.UseCases.Forms.Delete;
using Endatix.Core.UseCases.Themes.Delete;
using Endatix.Core.UseCases.DataLists;
using Endatix.Core.UseCases.DataLists.Delete;
using Endatix.Core.UseCases.DataLists.UpdateDetails;
using Endatix.Core.UseCases.Submissions;
using Endatix.Core.UseCases.Submissions.GetFiles;
using Endatix.Core.UseCases.Submissions.ListByFormId;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Core.UseCases;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class DirectCallOnlyAttribute : Attribute;

/// <summary>Registers tenant-bound reads for managed content without changing the public, direct-call handlers.</summary>
[Experimental("ENDATIX0006")]
public static class ManagedContentHandlers
{
    /// <summary>Leaves direct-call compatibility adapters out of MediatR discovery.</summary>
    public static void Configure(IServiceCollection services, MediatRServiceConfiguration configuration)
    {
        var included = configuration.TypeEvaluator;
        configuration.TypeEvaluator = type => included(type) && !type.IsDefined(typeof(DirectCallOnlyAttribute), false);
        services.AddScoped<ContentQueryScope>();
        services.AddScoped<FormFieldDefinitions>();
        services.AddScoped<ActiveDefinitions>();
        services.AddScoped(typeof(EditableContent<>));
        services.AddScoped<DataListRows>();
        services.AddScoped<DataListDeletion>();
        services.AddScoped<DataListDetailsWriter>();
        services.AddScoped<FormEdits>();
        services.AddScoped<DeletableForms>();
        services.AddScoped<ThemeRemoval>();
        services.AddScoped<SubmissionTargets>();
        services.AddScoped<SubmissionChangeSaver>();
        services.AddScoped<SubmissionFileTargets>();
        services.AddScoped<ISubmissionListParent, TenantFormParent>();
    }
}
