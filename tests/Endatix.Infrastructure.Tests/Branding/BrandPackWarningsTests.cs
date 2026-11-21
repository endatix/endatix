using Endatix.Framework.Logging;
using Endatix.Infrastructure.Branding;
using Endatix.Infrastructure.Tests.Features.ReCaptcha;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Infrastructure.Tests.Branding;

public sealed class BrandPackWarningsTests : IDisposable
{
    private readonly string _packDirectory = Directory.CreateTempSubdirectory("brand-pack-").FullName;

    public void Dispose() => Directory.Delete(_packDirectory, recursive: true);

    [Fact]
    public async Task StartAsync_TwoWarnings_LogsEachOnce()
    {
        // Arrange
        var logger = new RecordingLogger<BrandPackWarnings>();
        var sut = new BrandPackWarnings(["first", "second"], logger);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        logger.Entries.Should().Equal(
            (LogLevel.Warning, EndatixEventIds.Email.BrandPackFieldIgnored),
            (LogLevel.Warning, EndatixEventIds.Email.BrandPackFieldIgnored));
    }

    [Fact]
    public async Task AddDeploymentBrand_MailtoList_LogsTheWarningAtStartup()
    {
        // Arrange
        File.WriteAllText(
            Path.Combine(_packDirectory, "brand.json"),
            """{ "version": 1, "name": "Example Forms", "links": { "support": "mailto:a@example.com,b@example.com" } }""");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Endatix:FeatureFlags:WhiteLabeling"] = "true",
                [BrandPackLoader.BrandDirectoryKey] = _packDirectory,
            })
            .Build();
        var logger = new RecordingLogger<BrandPackWarnings>();
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<BrandPackWarnings>>(logger);

        // Act
        services.AddDeploymentBrand(configuration);
        await using var provider = services.BuildServiceProvider();
        foreach (var hostedService in provider.GetServices<IHostedService>())
        {
            await hostedService.StartAsync(CancellationToken.None);
        }

        // Assert
        logger.Entries.Should().ContainSingle().Which.EventId.Should().Be(EndatixEventIds.Email.BrandPackFieldIgnored);
    }
}
