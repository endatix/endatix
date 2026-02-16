using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Features.Email;
using Endatix.Core.Features.ReCaptcha;
using Endatix.Framework.Hosting;
using Endatix.Infrastructure.Builders;
using Endatix.Infrastructure.Email;
using Endatix.Infrastructure.Exporting.Exporters.Submissions;
using Endatix.Infrastructure.Exporting.Transformers;
using Endatix.Infrastructure.ReCaptcha;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Builders;

/// <summary>
/// Tests for <see cref="InfrastructureIntegrationsBuilder"/>.
/// UseDefaults() wires: AddExternalStorage, AddEmailTemplateSettings, AddEmailSender&lt;SmtpEmailSender, SmtpSettings&gt;, AddReCaptcha.
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

    private static ServiceDescriptor? GetDescriptor(IServiceCollection sc, Type serviceType) =>
        sc.FirstOrDefault(sd => sd.ServiceType == serviceType);

    [Fact]
    public void Constructor_IsInternal_UsedViaParentIntegrationsProperty()
    {
        var builder = new InfrastructureBuilder(_builderRoot);

        Assert.NotNull(builder.Integrations);
        Assert.IsType<InfrastructureIntegrationsBuilder>(builder.Integrations);
    }

    [Fact]
    public void UseDefaults_ShouldWireAddExternalStorage()
    {
        var builder = _parentBuilder.Integrations;

        builder.UseDefaults();

        var descriptor = _services.FirstOrDefault(sd =>
            sd.ServiceType == typeof(IValueTransformer) &&
            sd.ImplementationType == typeof(StorageUrlRewriteTransformer));
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(StorageUrlRewriteTransformer), descriptor!.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void UseDefaults_ShouldWireAddEmailTemplateSettings()
    {
        var builder = _parentBuilder.Integrations;

        builder.UseDefaults();

        var descriptor = GetDescriptor(_services, typeof(IEmailTemplateService));
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(EmailTemplateService), descriptor!.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void UseDefaults_ShouldWireAddEmailSenderSmtp()
    {
        var builder = _parentBuilder.Integrations;

        builder.UseDefaults();

        var descriptor = GetDescriptor(_services, typeof(IEmailSender));
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(SmtpEmailSender), descriptor!.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void UseDefaults_ShouldWireAddReCaptcha()
    {
        var builder = _parentBuilder.Integrations;

        builder.UseDefaults();

        var descriptor = GetDescriptor(_services, typeof(IReCaptchaPolicyService));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor!.Lifetime);
        Assert.Equal(typeof(GoogleReCaptchaService), descriptor.ImplementationType);
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
