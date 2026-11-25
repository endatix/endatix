using System.Diagnostics.CodeAnalysis;
using Endatix.Core.UseCases.DataLists.ReplaceItems;
using Endatix.Core.UseCases.DataLists.Translations;
using Endatix.Core.UseCases.Forms.List;
using Endatix.Core.UseCases.Submissions.UpdateStatus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MediatR;
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
        RemoveDirectRegistrations(services);
        AddHandlerLoggers(services);
        var included = configuration.TypeEvaluator;
        configuration.TypeEvaluator = type => included(type) && !type.IsDefined(typeof(DirectCallOnlyAttribute), false);
        AddContentServices(services);
    }

    private static void AddContentServices(IServiceCollection services)
    {
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
        services.AddScoped<SubmissionTestForms>();
        services.AddScoped<SubmissionTargets>();
        services.AddScoped<SubmissionChangeSaver>();
        services.AddScoped<SubmissionFileTargets>();
        services.AddScoped<ISubmissionListParent, TenantFormParent>();
    }

    private static void RemoveDirectRegistrations(IServiceCollection services)
    {
        var direct = services.Where(descriptor => !descriptor.IsKeyedService && descriptor.ServiceType.IsGenericType
            && descriptor.ServiceType.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)
            && (descriptor.ImplementationType ?? descriptor.ImplementationInstance?.GetType())?.IsDefined(typeof(DirectCallOnlyAttribute), false) == true);
        foreach (var descriptor in direct.ToArray())
        {
            services.Remove(descriptor);
        }
    }

    private static void AddHandlerLoggers(IServiceCollection services)
    {
        AddLogger<ReplaceDataListItemsHandler>(services);
        AddLogger<ReplaceDataListTranslationsCsvHandler>(services);
        AddLogger<ListFormsHandler>(services);
        AddLogger<UpdateStatusHandler>(services);
        AddLogger<DeleteThemeHandler>(services);
        AddLogger<GetFormDefinitionFieldsHandler>(services);
    }

    // The registered adapter emits the operation's logs, including a caller's typed logger override.
    private static void AddLogger<T>(IServiceCollection services) =>
        services.TryAddKeyedTransient<ILogger>(typeof(T), static (sp, _) => sp.GetRequiredService<ILogger<T>>());
}
