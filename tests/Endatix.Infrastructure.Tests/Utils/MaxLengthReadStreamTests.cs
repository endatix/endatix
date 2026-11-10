using Endatix.Infrastructure.Features.Submissions;
using Endatix.Infrastructure.Utils;

namespace Endatix.Infrastructure.Tests.Utils;

public sealed class MaxLengthReadStreamTests
{
    [Fact]
    public void Read_Span_ReturnsBytesWithinLimit()
    {
        // Arrange
        using var stream = new MaxLengthReadStream(new MemoryStream([1, 2, 3, 4]), 4);
        Span<byte> buffer = stackalloc byte[4];

        // Act
        var read = stream.Read(buffer);

        // Assert
        read.Should().Be(4);
        stream.Position.Should().Be(4);
    }

    [Fact]
    public async Task ReadAsync_ArrayOverload_ReturnsBytesWithinLimit()
    {
        // Arrange
        using var stream = new MaxLengthReadStream(new MemoryStream([1, 2, 3]), 3);
        var buffer = new byte[3];

        // Act
        var read = await stream.ReadAsync(buffer.AsMemory(0, 3), CancellationToken.None);

        // Assert
        read.Should().Be(3);
    }

    [Fact]
    public void BoundedHttpResponseStream_ReadSpanAndArray_DelegatesToBoundedContent()
    {
        // Arrange
        using var response = new HttpResponseMessage { Content = new ByteArrayContent([1, 2, 3, 4]) };
        using var stream = new BoundedHttpResponseStream(response, 10);
        var array = new byte[2];
        Span<byte> span = stackalloc byte[2];

        // Act
        var first = stream.Read(array, 0, 2);
        var second = stream.Read(span);

        // Assert
        first.Should().Be(2);
        second.Should().Be(2);
    }
}
