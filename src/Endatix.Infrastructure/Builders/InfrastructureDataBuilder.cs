using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Abstractions;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Builders;

/// <summary>
/// Builder for configuring Endatix data infrastructure.
/// </summary>
public class InfrastructureDataBuilder
{
    private readonly InfrastructureBuilder _parentBuilder;

    /// <summary>
    /// Initializes a new instance of the InfrastructureDataBuilder class.
    /// </summary>
    /// <param name="parentBuilder">The parent infrastructure builder.</param>
    internal InfrastructureDataBuilder(InfrastructureBuilder parentBuilder)
    {
        _parentBuilder = parentBuilder;
    }

    /// <summary>
    /// Configures data infrastructure with default settings.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public InfrastructureDataBuilder UseDefaults()
    {
        _parentBuilder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        _parentBuilder.Services.AddScoped<IFormsRepository, FormsRepository>();
        _parentBuilder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        return this;
    }

    /// <summary>
    /// Configures data options.
    /// </summary>
    /// <param name="configure">Action to configure data options.</param>
    /// <returns>The builder for chaining.</returns>
    public InfrastructureDataBuilder Configure(Action<DataOptions> configure)
    {
        _parentBuilder.Services.Configure(configure);
        return this;
    }

    /// <summary>
    /// Returns to the parent infrastructure builder.
    /// </summary>
    /// <returns>The parent infrastructure builder.</returns>
    public InfrastructureBuilder Parent() => _parentBuilder;
} 