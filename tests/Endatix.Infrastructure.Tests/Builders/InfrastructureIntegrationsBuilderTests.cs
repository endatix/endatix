using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Features.Email;
using Endatix.Framework.Hosting;
using Endatix.Infrastructure.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Builders;

/// <summary>
/// Tests for <see cref="InfrastructureIntegrationsBuilder"/>.
/// Integrations UseDefaults() registers external storage (including export URL rewriter),
/// email template settings, default email sender (Smtp), and ReCaptcha.
/// </summary>
public class InfrastructureIntegrationsBuilderTests
{
    private readonly ServiceCollection _services;
    private readonly InfrastructureBuilder _parentBuilder;
    private readonly IBuilderRoot _builderRoot;

    public InfrastructureIntegrationsBuilderTests()
    {
        _services = new ServiceCollection();
        _builderRoot = Substitute.For<IBuilderRoot>();
        _builderRoot.Services.Returns(_services);
        _builderRoot.LoggerFactory.Returns(Substitute.For<ILoggerFactory>());
        _builderRoot.Configuration.Returns(new ConfigurationBuilder().Build());
        _builderRoot.AppEnvironment.Returns((IAppEnvironment?)null);
        _parentBuilder = new InfrastructureBuilder(_builderRoot);
    }

    private static bool IsRegistered<T>(IServiceCollection sc) =>
        sc.Any(sd => sd.ServiceType == typeof(T));

    [Fact]
    public void Constructor_IsInternal_UsedViaParentIntegrationsProperty()
    {
        var builder = new InfrastructureBuilder(_builderRoot);

        Assert.NotNull(builder.Integrations);
        Assert.IsType<InfrastructureIntegrationsBuilder>(builder.Integrations);
    }

    [Fact]
    public void UseDefaults_ShouldRegisterExternalStorage()
    {
        var builder = _parentBuilder.Integrations;

        builder.UseDefaults();

        // AddExternalStorage: StorageOptions, AzureBlobStorageProviderOptions, IExportStorageUrlRewriter
        Assert.True(IsRegistered<IExportStorageUrlRewriter>(_services));
     
    }

    [Fact]
    public void UseDefaults_ShouldRegisterEmailServices()
    {
        var builder = _parentBuilder.Integrations;

        builder.UseDefaults();

        Assert.True(IsRegistered<IEmailTemplateService>(_services));
        Assert.True(IsRegistered<IEmailSender>(_services));
    }

    [Fact]
    public void UseDefaults_ShouldRegisterReCaptcha()
    {
        var builder = _parentBuilder.Integrations;

        builder.UseDefaults();

        Assert.True(IsRegistered<Endatix.Core.Features.ReCaptcha.IReCaptchaPolicyService>(_services));
    }

    [Fact]
    public void UseDefaults_ShouldReturnBuilderForChaining()
    {
        var builder = _parentBuilder.Integrations;

        var result = builder.UseDefaults();

        Assert.Same(builder, result);
    }

    [Fact]
    public void Build_ShouldReturnParentBuilder()
    {
        var builder = _parentBuilder.Integrations;

        var result = builder.Build();

        Assert.Same(_parentBuilder, result);
    }
}
