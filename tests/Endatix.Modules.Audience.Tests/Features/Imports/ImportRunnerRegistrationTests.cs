using Endatix.Modules.Audience.Features.Imports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Endatix.Modules.Audience.Tests.Features.Imports;

public sealed class ImportRunnerRegistrationTests
{
    private const string ImportChunkRowsKey = $"{AudienceOptions.SectionName}:{nameof(AudienceOptions.ImportChunkRows)}";

    [Fact]
    public void AddImportRunner_NoSetting_ImportChunkRowsIsAThousand()
    {
        // Arrange
        var options = AudienceOptionsFrom(new Dictionary<string, string?>());

        // Act
        var chunkRows = options.Value.ImportChunkRows;

        // Assert
        chunkRows.Should().Be(1_000);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(150)]
    [InlineData(AudienceOptions.MaxImportChunkRows)]
    public void AddImportRunner_ImportChunkRowsInRange_BindsTheSetting(int setting)
    {
        // Arrange
        var options = AudienceOptionsFrom(new Dictionary<string, string?> { [ImportChunkRowsKey] = $"{setting}" });

        // Act
        var chunkRows = options.Value.ImportChunkRows;

        // Assert
        chunkRows.Should().Be(setting);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(AudienceOptions.MaxImportChunkRows + 1)]
    public void AddImportRunner_ImportChunkRowsOutOfRange_FailsNamingTheKey(int setting)
    {
        // Arrange
        var options = AudienceOptionsFrom(new Dictionary<string, string?> { [ImportChunkRowsKey] = $"{setting}" });

        // Act
        var read = () => options.Value;

        // Assert
        read.Should().Throw<OptionsValidationException>()
            .WithMessage($"{ImportChunkRowsKey} must be from 1 to 1000; it is {setting}.");
    }

    private static IOptions<AudienceOptions> AudienceOptionsFrom(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddImportRunner();
        return services.BuildServiceProvider().GetRequiredService<IOptions<AudienceOptions>>();
    }
}
