using System.Reflection;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Logging;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases;
using Endatix.Core.UseCases.DataLists;
using Endatix.Core.UseCases.DataLists.List;
using Endatix.Core.UseCases.Folders;
using Endatix.Core.UseCases.FormDefinitions;
using Endatix.Core.UseCases.Submissions;
using Endatix.Core.UseCases.TenantBranding;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Endatix.Infrastructure.Messaging;

/// <summary>
/// Configuration options for MediatR messaging.
/// </summary>
public class MediatRConfigOptions
{
    /// <summary>
    /// Additional assemblies to scan for handlers.
    /// </summary>
    public Assembly[] AdditionalAssemblies { get; set; } = [];

    /// <summary>
    /// Whether to include logging pipeline behavior.
    /// </summary>
    public bool IncludeLoggingPipeline { get; set; } = true;
}

/// <summary>
/// Provides configuration for MediatR messaging infrastructure.
/// </summary>
public static class MessagingConfiguration
{
    /// <summary>
    /// Adds MediatR messaging services to the service collection.
    /// </summary>
    public static IServiceCollection AddMediatRMessaging(
        this IServiceCollection services,
        Action<MediatRConfigOptions>? configure = null)
    {
        var options = new MediatRConfigOptions();
        configure?.Invoke(options);

        var mediatRAssemblies = new[]
        {
            Endatix.Core.AssemblyReference.Assembly,
            Endatix.Infrastructure.AssemblyReference.Assembly
        };

        if (options.AdditionalAssemblies.Length != 0)
        {
            mediatRAssemblies = [.. mediatRAssemblies, .. options.AdditionalAssemblies];
        }

        // Never activate the direct, repository-scoped constructor for a managed query.
        services.TryAddScoped<IRequestHandler<ListDataListsQuery, Result<Paged<DataListDto>>>>(sp =>
            new ListDataListsHandler(sp.GetRequiredService<IRepository<DataList>>(), sp.GetRequiredService<ITenantContext>()));
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssemblies(mediatRAssemblies);
        });

        services.AddScoped<IDomainEventDispatcher, MediatRDomainEventDispatcher>();
        AddUseCaseServices(services);

        if (options.IncludeLoggingPipeline)
        {
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingPipelineBehavior<,>));
        }

        return services;
    }

    private static void AddUseCaseServices(IServiceCollection services)
    {
        services.AddScoped<FolderAssignmentPolicy>();
        services.AddScoped<FolderWritePolicy>();
        services.AddScoped<ManagedFormContentAccess>();
        services.AddScoped<ManagedContentScope>();
        services.AddScoped<ManagedDataListAccess>();
        services.AddScoped<ManagedSubmissionAccess>();
        services.AddScoped<SubmissionFormTheme>();
        services.AddScoped<TenantBrandingAccess>();
    }
}
