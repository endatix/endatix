using Endatix.Core.Features.Email;
using Endatix.Framework.Logging;
using Endatix.Infrastructure.Email;
using Endatix.Infrastructure.Tests.Features.ReCaptcha;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Tests.Email;

public sealed class DeprecatedSmtpSenderTests
{
    private const string SmtpSection = "Endatix:Integrations:Email:SmtpSettings";
    private const string SenderSection = "Endatix:Integrations:Email:Sender";

    [Theory]
    [InlineData("smtp@example.com", null, 1)]
    [InlineData(null, "SMTP Team", 1)]
    [InlineData(null, null, 0)]
    public async Task StartAsync_DeprecatedKeys_LogsOneWarningOnlyWhenSet(string? address, string? name, int warnings)
    {
        // Arrange
        var logger = new RecordingLogger<DeprecatedSmtpSender>();
        var settings = Options.Create(new SmtpSettings { DefaultFromAddress = address, DefaultFromName = name });
        var warning = new DeprecatedSmtpSender(settings, logger);

        // Act
        await warning.StartAsync(CancellationToken.None);

        // Assert
        logger.Entries.Should().BeEquivalentTo(
            Enumerable.Repeat((LogLevel.Warning, EndatixEventIds.Email.DeprecatedSmtpSenderKeys), warnings));
    }

    [Fact]
    public void AddEmailSender_OnlyDeprecatedKeys_SendsWithThemButKeepsTheTemplateRows()
    {
        // Arrange
        using var provider = SmtpHost(new Dictionary<string, string?>
        {
            [$"{SmtpSection}:DefaultFromAddress"] = "smtp@example.com",
            [$"{SmtpSection}:DefaultFromName"] = "SMTP Team",
        });

        // Act
        var sender = provider.GetRequiredService<DeploymentSender>();

        // Assert
        sender.Should().Be(new DeploymentSender("smtp@example.com", "SMTP Team", ConfiguredAddress: null));
    }

    [Fact]
    public void Read_DeprecatedNameWithAControlCharacter_DropsItForTheBrandName()
    {
        // Arrange
        var settings = new SmtpSettings { DefaultFromAddress = "smtp@example.com", DefaultFromName = "SMTP\r\nBcc: x@example.com" };

        // Act
        var legacy = DeprecatedSmtpSender.Read(settings);

        // Assert
        legacy.Address.Should().Be("smtp@example.com");
        legacy.Name.Should().BeNull();
    }

    [Fact]
    public void AddEmailSender_SenderAndDeprecatedKeys_TheSenderWins()
    {
        // Arrange
        using var provider = SmtpHost(new Dictionary<string, string?>
        {
            [$"{SmtpSection}:DefaultFromAddress"] = "smtp@example.com",
            [$"{SenderSection}:Address"] = "noreply@example.com",
        });

        // Act
        var sender = provider.GetRequiredService<DeploymentSender>();

        // Assert
        sender.Should().Be(new DeploymentSender("noreply@example.com", "Endatix", "noreply@example.com"));
    }

    [Fact]
    public void AddEmailSender_RegistersTheStartupWarningOnce()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEmailSender<SmtpEmailSender, SmtpSettings>();
        services.AddEmailTemplateSettings();

        // Assert
        services.Count(descriptor => descriptor.ImplementationType == typeof(DeprecatedSmtpSender)).Should().Be(1);
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IHostedService)
            && descriptor.ImplementationType == typeof(DeprecatedSmtpSender));
    }

    private static ServiceProvider SmtpHost(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddEmailSender<SmtpEmailSender, SmtpSettings>();
        return services.BuildServiceProvider();
    }
}
