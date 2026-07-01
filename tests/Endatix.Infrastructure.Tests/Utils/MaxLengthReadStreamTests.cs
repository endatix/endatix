using Endatix.Infrastructure.Utils;

namespace Endatix.Infrastructure.Tests.Utils;

public sealed class MaxLengthReadStreamTests
{
    [Fact]
    public async Task Read_WithinLimit_ReturnsContent()
    {
        var inner = new MemoryStream("hello"u8.ToArray());
        using var limited = new MaxLengthReadStream(inner, maxLength: 10);

        using var reader = new StreamReader(limited);
        var text = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

        Assert.Equal("hello", text);
    }

    [Fact]
    public async Task Read_ExceedingLimit_Throws()
    {
        var inner = new MemoryStream(new byte[20]);
        using var limited = new MaxLengthReadStream(inner, maxLength: 10);

        var buffer = new byte[20];
        await limited.ReadExactlyAsync(buffer.AsMemory(0, 10), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            limited.ReadExactlyAsync(buffer.AsMemory(0, 1), TestContext.Current.CancellationToken).AsTask());
    }
}
