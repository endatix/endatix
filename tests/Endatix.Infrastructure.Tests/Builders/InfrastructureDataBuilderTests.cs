using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Framework.Hosting;
using Endatix.Infrastructure.Builders;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Exporting;
using Endatix.Infrastructure.Exporting.Exporters.Submissions;
using Endatix.Infrastructure.Features.Submissions;
using Endatix.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Builders;

/// <summary>
/// Tests for <see cref="InfrastructureDataBuilder"/>.
/// Data builder registers repositories, unit of work, exporters, and related data services.
/// Hub, Storage, and Export URL rewriter are registered by Integrations/Core, not Data.
/// </summary>
public class InfrastructureDataBuilderTests
{
    private readonly ServiceCollection _services;
    private readonly InfrastructureBuilder _parentBuilder;
    private readonly IBuilderRoot _builderRoot;

    public InfrastructureDataBuilderTests()
    {
        _services = new ServiceCollection();
        _builderRoot = Substitute.For<IBuilderRoot>();
        _builderRoot.Services.Returns(_services);
        _builderRoot.LoggerFactory.Returns(Substitute.For<ILoggerFactory>());
        _builderRoot.Configuration.Returns(Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>());
        _builderRoot.AppEnvironment.Returns((IAppEnvironment?)null);
        _parentBuilder = new InfrastructureBuilder(_builderRoot);
    }

    private static bool IsRegistered<T>(IServiceCollection sc) =>
        sc.Any(sd => sd.ServiceType == typeof(T));

    [Fact]
    public void Constructor_IsInternal_UsedViaParentDataProperty()
    {
        var builder = new InfrastructureBuilder(_builderRoot);

        Assert.NotNull(builder.Data);
        Assert.IsType<InfrastructureDataBuilder>(builder.Data);
    }

    [Fact]
    public void UseDefaults_ShouldRegisterDataServices()
    {
        var builder = _parentBuilder.Data;

        builder.UseDefaults();

        Assert.True(IsRegistered<IIdGenerator<long>>(_services));
        Assert.True(IsRegistered<IUnitOfWork>(_services));
        Assert.Contains(_services, sd => sd.ServiceType.IsGenericType && sd.ServiceType.GetGenericTypeDefinition() == typeof(IRepository<>));
        Assert.True(IsRegistered<IFormsRepository>(_services));
        Assert.True(IsRegistered<DataSeeder>(_services));
    }

    [Fact]
    public void UseDefaults_ShouldRegisterExporters()
    {
        var builder = _parentBuilder.Data;

        builder.UseDefaults();

        Assert.True(IsRegistered<IExporterFactory>(_services));
        Assert.True(IsRegistered<ISubmissionFileExtractor>(_services));
        Assert.True(IsRegistered<IExporter<SubmissionExportRow>>(_services));
        Assert.True(IsRegistered<SubmissionJsonExporter>(_services));
        Assert.True(IsRegistered<SubmissionCsvExporter>(_services));
        Assert.True(IsRegistered<IExporter<DynamicExportRow>>(_services));
    }

    [Fact]
    public void UseDefaults_ShouldReturnBuilderForChaining()
    {
        var builder = _parentBuilder.Data;

        var result = builder.UseDefaults();

        Assert.Same(builder, result);
    }

    [Fact]
    public void Build_ShouldReturnParentBuilder()
    {
        var builder = _parentBuilder.Data;

        var result = builder.Build();

        Assert.Same(_parentBuilder, result);
    }
}
