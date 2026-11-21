using Endatix.Core.Branding;
using Endatix.Core.Features.Email;
using Endatix.Infrastructure.Branding;
using Endatix.Infrastructure.Email;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Tests.Setup;

public sealed class DeploymentBrandRegistrationTests : IDisposable
{
    private readonly string _packDirectory = Directory.CreateTempSubdirectory("brand-pack-").FullName;

    public void Dispose() => Directory.Delete(_packDirectory, recursive: true);

    [Fact]
    public void AddEmailSender_WithoutAddDeploymentBrand_ResolvesTheBuiltInBrand()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        // Act
        services.AddEmailSender<SmtpEmailSender, SmtpSettings>();

        // Assert
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<DeploymentBrand>().Should().BeSameAs(DeploymentBrand.BuiltIn);
    }

    [Fact]
    public void AddDeploymentBrand_BeforeAddEmailSender_KeepsThePackReadAtRegistration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDeploymentBrand(PackConfiguration("""{ "version": 1, "name": "Example Forms" }"""));
        services.AddEmailSender<SmtpEmailSender, SmtpSettings>();

        // Assert
        ResolvedBrandName(services).Should().Be("Example Forms");
        ResolvedEmailIdentity(services).Should().Be(("Example Forms", "Example Forms"));
    }

    [Fact]
    public void AddDeploymentBrand_AfterAddEmailSender_ReplacesTheLazyFallback()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddEmailSender<SmtpEmailSender, SmtpSettings>();

        // Act
        services.AddDeploymentBrand(PackConfiguration("""{ "version": 1, "name": "Example Forms" }"""));

        // Assert
        services.Count(descriptor => descriptor.ServiceType == typeof(DeploymentBrand)).Should().Be(1);
        ResolvedBrandName(services).Should().Be("Example Forms");
        ResolvedEmailIdentity(services).Should().Be(("Example Forms", "Example Forms"));
    }

    [Fact]
    public void AddEmailSenderAndTemplateSettings_Together_RegisterTheSenderOnce()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEmailSender<SmtpEmailSender, SmtpSettings>();
        services.AddEmailTemplateSettings();

        // Assert
        services.Count(descriptor => descriptor.ServiceType == typeof(DeploymentSender)).Should().Be(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(EmailVariables)).Should().Be(1);
    }

    [Fact]
    public void AddEmailSender_ConfiguredSender_ResolvesItsAddressAndName()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Endatix:Integrations:Email:Sender:Address"] = "noreply@example.com",
                ["Endatix:Integrations:Email:Sender:Name"] = "Example Team",
            })
            .Build());

        // Act
        services.AddEmailSender<SmtpEmailSender, SmtpSettings>();

        // Assert
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<DeploymentSender>().Should().Be(new DeploymentSender("noreply@example.com", "Example Team", "noreply@example.com"));
    }

    [Theory]
    [InlineData("Example <noreply@example.com>", null)]
    [InlineData("noreply@example.com", "Example\r\nBcc: x@example.com")]
    public void AddEmailSender_InvalidSender_FailsValidation(string address, string? name)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Endatix:Integrations:Email:Sender:Address"] = address,
                ["Endatix:Integrations:Email:Sender:Name"] = name,
            })
            .Build());
        services.AddEmailSender<SmtpEmailSender, SmtpSettings>();
        using var provider = services.BuildServiceProvider();

        // Act
        var resolve = () => provider.GetRequiredService<DeploymentSender>();

        // Assert
        resolve.Should().Throw<OptionsValidationException>().Which.Message.Should().Contain("Endatix:Integrations:Email:Sender");
    }

    [Fact]
    public void AddDeploymentBrand_AfterAddEmailSenderWithInvalidPack_ThrowsAtRegistration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddEmailSender<SmtpEmailSender, SmtpSettings>();

        // Act
        var register = () => services.AddDeploymentBrand(PackConfiguration("""{ "version": 1 }"""));

        // Assert
        register.Should().Throw<BrandPackException>().Which.Message.Should().Contain("/name");
    }

    [Fact]
    public async Task AddEmailSender_OnlyWithAnInvalidPackAndTheFlagOn_StopsStartup()
    {
        // Arrange: a host that calls AddEmailSender and never AddDeploymentBrand.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(PackConfiguration("""{ "version": 1 }"""));
        services.AddEmailSender<SmtpEmailSender, SmtpSettings>();
        await using var provider = services.BuildServiceProvider();

        // Act: start the hosted services the way the host does.
        var start = async () =>
        {
            foreach (var hostedService in provider.GetServices<IHostedService>())
            {
                await hostedService.StartAsync(CancellationToken.None);
            }
        };

        // Assert
        (await start.Should().ThrowAsync<BrandPackException>()).Which.Message.Should().Contain("/name");
    }

    [Fact]
    public async Task AddEmailSender_OnlyWithAWebSupportLink_LogsTheWarningAtStartup()
    {
        // Arrange: a host that calls AddEmailSender and never AddDeploymentBrand.
        var logs = new RecordingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(logs));
        services.AddSingleton(PackConfiguration("""{ "version": 1, "name": "Example Forms", "links": { "support": "https://forms.example.com/help" } }"""));
        services.AddEmailSender<SmtpEmailSender, SmtpSettings>();
        await using var provider = services.BuildServiceProvider();

        // Act
        foreach (var hostedService in provider.GetServices<IHostedService>())
        {
            await hostedService.StartAsync(CancellationToken.None);
        }

        // Assert
        provider.GetRequiredService<DeploymentBrand>().SupportAddress.Should().Be("support@endatix.com");
        logs.Entries.Should().ContainSingle(entry => entry.EventId == 6003)
            .Which.Message.Should().Contain("/links/support is not a mailto link");
    }

    [Fact]
    public void AddEmailSender_HostSuppliesItsOwnSender_StillRegistersTheVariables()
    {
        // Arrange: the host registers its own sender before the email sender.
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var hostSender = new DeploymentSender("host@example.com", "Host Sender", "host@example.com");
        services.AddSingleton(hostSender);

        // Act
        services.AddEmailSender<SmtpEmailSender, SmtpSettings>();
        services.AddEmailTemplateSettings();

        // Assert
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<DeploymentSender>().Should().BeSameAs(hostSender);
        provider.GetRequiredService<EmailVariables>().ProductName.Should().Be("Endatix");
        services.Count(descriptor => descriptor.ServiceType == typeof(EmailVariables)).Should().Be(1);
    }

    private IConfiguration PackConfiguration(string pack)
    {
        File.WriteAllText(Path.Combine(_packDirectory, "brand.json"), pack);
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Endatix:FeatureFlags:WhiteLabeling"] = "true",
                [BrandPackLoader.BrandDirectoryKey] = _packDirectory,
            })
            .Build();
    }

    private static (string ProductName, string SenderName) ResolvedEmailIdentity(IServiceCollection services)
    {
        using var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<EmailVariables>().ProductName, provider.GetRequiredService<DeploymentSender>().Name);
    }

    private static string ResolvedBrandName(IServiceCollection services)
    {
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<DeploymentBrand>().ProductName;
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public List<(int EventId, string Message)> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(Entries);

        public void Dispose()
        {
        }

        private sealed class RecordingLogger(List<(int EventId, string Message)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                entries.Add((eventId.Id, formatter(state, exception)));
        }
    }
}
