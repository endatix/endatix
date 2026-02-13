using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure;
using Endatix.Framework.Hosting;
using Endatix.Infrastructure.Builders;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Exporting.Transformers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Builders;

/// <summary>
/// Tests for <see cref="InfrastructureBuilder"/> reflecting the current setup:
/// UseDefaults() registers core (AddCoreInfrastructure, AddWebHookProcessing, AddMultitenancyConfiguration)
/// then Data, Messaging, Security, Integrations. Build() calls Security.Build() and Messaging.Build().
/// </summary>
public class InfrastructureBuilderTests
{
    private readonly ServiceCollection _services;
    private readonly IBuilderRoot _builderRoot;

    public InfrastructureBuilderTests()
    {
        _services = new ServiceCollection();
        _builderRoot = Substitute.For<IBuilderRoot>();
        _builderRoot.Services.Returns(_services);
        _builderRoot.LoggerFactory.Returns(Substitute.For<ILoggerFactory>());
        _builderRoot.Configuration.Returns(CreateMinimalConfiguration());
        _builderRoot.AppEnvironment.Returns((IAppEnvironment?)null);
    }

    private static IConfiguration CreateMinimalConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Endatix:Auth:DefaultScheme"] = InfrastructureSecurityBuilder.MULTI_JWT_SCHEME_NAME,
                ["Endatix:Auth:Providers:EndatixJwt:Enabled"] = "true",
                ["Endatix:Auth:Providers:EndatixJwt:SigningKey"] = "test-signing-key-32-characters-long",
                ["Endatix:Auth:Providers:EndatixJwt:Issuer"] = "test",
                ["Endatix:Auth:Providers:EndatixJwt:Audiences:0"] = "test"
            })
            .Build();
    }

    private static bool IsRegistered<T>(IServiceCollection sc) =>
        sc.Any(sd => sd.ServiceType == typeof(T));

    [Fact]
    public void Constructor_ShouldInitializeChildBuilders()
    {
        var builder = new InfrastructureBuilder(_builderRoot);

        Assert.NotNull(builder.Data);
        Assert.NotNull(builder.Security);
        Assert.NotNull(builder.Messaging);
        Assert.NotNull(builder.Integrations);
    }

    [Fact]
    public void UseDefaults_ShouldRegisterCoreInfrastructure()
    {
        var builder = new InfrastructureBuilder(_builderRoot);

        builder.UseDefaults();

        // AddCoreInfrastructure: HybridCache, IDateTimeProvider, HttpContextAccessor, HubSettings
        Assert.True(IsRegistered<IDateTimeProvider>(_services));
        Assert.Contains(_services, sd => sd.ServiceType == typeof(Microsoft.AspNetCore.Http.IHttpContextAccessor));
    }

    [Fact]
    public void UseDefaults_ShouldRegisterMultitenancy()
    {
        var builder = new InfrastructureBuilder(_builderRoot);

        builder.UseDefaults();

        Assert.True(IsRegistered<ITenantContext>(_services));
    }

    [Fact]
    public void UseDefaults_ShouldRegisterDataServices()
    {
        var builder = new InfrastructureBuilder(_builderRoot);

        builder.UseDefaults();

        Assert.True(IsRegistered<Endatix.Core.Abstractions.Data.IUnitOfWork>(_services));
        Assert.True(IsRegistered<Endatix.Core.Abstractions.Exporting.IExporterFactory>(_services));
        Assert.True(IsRegistered<DataSeeder>(_services));
    }

    [Fact]
    public void UseDefaults_ShouldRegisterIntegrationsServices()
    {
        var builder = new InfrastructureBuilder(_builderRoot);

        builder.UseDefaults();

        // Integrations: AddExternalStorage (IValueTransformer / StorageUrlRewriteTransformer), AddEmailTemplateSettings, AddEmailSender, AddReCaptcha
        Assert.Contains(_services, sd =>
            sd.ServiceType == typeof(IValueTransformer) &&
            sd.ImplementationType == typeof(StorageUrlRewriteTransformer));
        Assert.True(IsRegistered<IEmailTemplateService>(_services));
    }

    [Fact]
    public void UseDefaults_ShouldReturnBuilderForChaining()
    {
        var builder = new InfrastructureBuilder(_builderRoot);

        var result = builder.UseDefaults();

        Assert.Same(builder, result);
    }

    [Fact]
    public void Build_ShouldReturnParentBuilder()
    {
        var builder = new InfrastructureBuilder(_builderRoot);
        builder.UseDefaults();

        var result = builder.Build();

        Assert.Same(_builderRoot, result);
    }
}
