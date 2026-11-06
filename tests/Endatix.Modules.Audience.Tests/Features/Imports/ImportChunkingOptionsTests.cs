using Endatix.Modules.Audience.Features.Imports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Endatix.Modules.Audience.Tests.Features.Imports;

public sealed class ImportChunkingOptionsTests
{
    private const string ChunkRowsKey = $"{ImportChunkingOptions.SectionName}:{nameof(ImportChunkingOptions.ChunkRows)}";

    [Fact]
    public void AddImportRunner_NoSetting_ChunksOfAThousandRows()
    {
        // Arrange
        var options = ChunkingFrom(new Dictionary<string, string?>());

        // Act
        var chunkRows = options.Value.ChunkRows;

        // Assert
        chunkRows.Should().Be(1_000);
    }

    [Fact]
    public void AddImportRunner_ChunkRowsSet_ChunksOfThatSize()
    {
        // Arrange
        var options = ChunkingFrom(new Dictionary<string, string?> { [ChunkRowsKey] = "50" });

        // Act
        var chunkRows = options.Value.ChunkRows;

        // Assert
        chunkRows.Should().Be(50);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void AddImportRunner_ChunkRowsBelowOne_IsRefused(string chunkRows)
    {
        // Arrange
        var options = ChunkingFrom(new Dictionary<string, string?> { [ChunkRowsKey] = chunkRows });

        // Act
        var read = () => options.Value;

        // Assert
        read.Should().Throw<OptionsValidationException>()
            .WithMessage($"{ImportChunkingOptions.SectionName}:ChunkRows must be at least 1.");
    }

    private static IOptions<ImportChunkingOptions> ChunkingFrom(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddImportRunner();
        return services.BuildServiceProvider().GetRequiredService<IOptions<ImportChunkingOptions>>();
    }
}
