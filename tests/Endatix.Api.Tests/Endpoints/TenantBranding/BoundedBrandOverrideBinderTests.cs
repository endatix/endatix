using System.Text;
using Endatix.Api.Endpoints.TenantBranding;
using Endatix.Core.UseCases.TenantBranding.Publishing;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Endatix.Api.Tests.Endpoints.TenantBranding;

public class BoundedBrandOverrideBinderTests
{
    private const int MAX = PublishTenantBrandingCommand.MAX_OVERRIDE_LENGTH;

    private readonly BoundedBrandOverrideBinder _binder = new();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("")]
    [InlineData("""{"version":1}""")]
    [InlineData("""{"name":"Café ☕"}""")]
    public async Task BindAsync_WithABodyWithinTheBound_ReadsItWhole(string body)
    {
        // Act
        var request = await _binder.BindAsync(Context(new MemoryStream(Encoding.UTF8.GetBytes(body))), Cancellation);

        // Assert
        request.Content.Should().Be(body);
    }

    [Fact]
    public async Task BindAsync_WithABodyOfExactlyTheBound_ReadsItWhole()
    {
        // Arrange
        var body = new string('a', MAX);

        // Act
        var request = await _binder.BindAsync(Context(new MemoryStream(Encoding.UTF8.GetBytes(body))), Cancellation);

        // Assert
        request.Content.Should().Be(body);
    }

    [Fact]
    public async Task BindAsync_WithAMegabyte_StopsOneCharacterPastTheBound()
    {
        // Arrange
        var body = new CountingStream(new MemoryStream(Encoding.UTF8.GetBytes(new string('a', 1024 * 1024))));

        // Act
        var request = await _binder.BindAsync(Context(body), Cancellation);

        // Assert - over the bound, so the command answers too_large; the rest is never read
        request.Content.Length.Should().Be(MAX + 1);
        body.BytesRead.Should().BeLessThan(64 * 1024);
    }

    [Fact]
    public async Task BindAsync_WhenTheServerRefusesTheBodyAsTooLarge_IsOverTheBound()
    {
        // Arrange - Kestrel throws this on the first read of a body over its own limit
        var body = new ThrowingStream(new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge));

        // Act
        var request = await _binder.BindAsync(Context(body), Cancellation);

        // Assert
        request.Content.Length.Should().Be(MAX + 1);
    }

    [Fact]
    public async Task BindAsync_WithAnyOtherBadBody_Rethrows()
    {
        // Arrange
        var body = new ThrowingStream(new BadHttpRequestException("Unexpected end of request content.", StatusCodes.Status400BadRequest));

        // Act
        var bind = async () => await _binder.BindAsync(Context(body), Cancellation);

        // Assert
        await bind.Should().ThrowAsync<BadHttpRequestException>();
    }

    private static BinderContext Context(Stream body)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = body;
        return new BinderContext(httpContext, [], null!, false, []);
    }

    private sealed class CountingStream(Stream inner) : Stream
    {
        public long BytesRead { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, count);
            BytesRead += read;
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await inner.ReadAsync(buffer, cancellationToken);
            BytesRead += read;
            return read;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class ThrowingStream(Exception exception) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => throw exception;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => throw exception;
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
