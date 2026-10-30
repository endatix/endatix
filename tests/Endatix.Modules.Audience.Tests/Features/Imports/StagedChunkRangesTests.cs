using Endatix.Modules.Audience.Features.Imports;

namespace Endatix.Modules.Audience.Tests.Features.Imports;

public sealed class StagedChunkRangesTests
{
    [Fact]
    public void IsTheFile_ChunksInIndexOrderWithAGap_IsTrue()
    {
        // Arrange — row 1,002 is a blank line the browser skipped.
        ChunkSpan[] spans = [new(1, 1_003, 1_000), new(0, 2, 1_000)];

        // Act
        bool whole = StagedChunkRanges.IsTheFile(2_000, spans);

        // Assert
        whole.Should().BeTrue();
    }

    [Fact]
    public void IsTheFile_RangesInRowOrderButReversedAgainstTheirIndexes_IsFalse()
    {
        // Arrange — the write reads chunk 0 first, so its checkpoint would pass every row of chunk 1.
        ChunkSpan[] spans = [new(0, 1_002, 1_000), new(1, 2, 1_000)];

        // Act
        bool whole = StagedChunkRanges.IsTheFile(2_000, spans);

        // Assert
        whole.Should().BeFalse();
    }

    [Fact]
    public void IsTheFile_FewerRowsThanTheFileDeclared_IsFalse()
    {
        // Arrange
        ChunkSpan[] spans = [new(0, 2, 1_000)];

        // Act
        bool whole = StagedChunkRanges.IsTheFile(2_000, spans);

        // Assert
        whole.Should().BeFalse();
    }
}
