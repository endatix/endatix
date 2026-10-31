using System.IO.Compression;
using System.Text;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Submissions.GetFiles;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;

namespace Endatix.Api.Endpoints.Submissions;

public class GetSubmissionFilesTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ILogger<GetSubmissionFiles> _logger = Substitute.For<ILogger<GetSubmissionFiles>>();
    private readonly AbortRecordingLifetimeFeature _lifetime = new();
    private readonly GetSubmissionFiles _endpoint;

    public GetSubmissionFilesTests()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<IHttpRequestLifetimeFeature>(_lifetime);
        _endpoint = Factory.Create<GetSubmissionFiles>(
            httpContext: httpContext,
            _mediator,
            _logger);
    }

    [Fact]
    public async Task ReturnsZipWithFiles()
    {
        // Arrange
        var files = new List<FileDescriptor>
        {
            new("file1.txt", "text/plain", new MemoryStream(Encoding.UTF8.GetBytes("abc")))
        };
        var result = Result.Success(new GetFilesResult("TestForm", 123, files));
        _mediator.Send(Arg.Any<GetFilesQuery>(), Arg.Any<CancellationToken>()).Returns(result);
        var request = new GetSubmissionFilesRequest { FormId = 1, SubmissionId = 123 };

        // Act
        await _endpoint.HandleAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(200, _endpoint.HttpContext.Response.StatusCode);
        Assert.Equal("application/zip", _endpoint.HttpContext.Response.ContentType);
        Assert.Equal("attachment; filename=test-form-123.zip", _endpoint.HttpContext.Response.Headers["Content-Disposition"]);
    }

    [Fact]
    public async Task ReturnsEmptyZipWhenNoFiles()
    {
        // Arrange
        var result = Result.Success(new GetFilesResult("TestForm", 123, new List<FileDescriptor>()));
        _mediator.Send(Arg.Any<GetFilesQuery>(), Arg.Any<CancellationToken>()).Returns(result);
        var request = new GetSubmissionFilesRequest { FormId = 1, SubmissionId = 123 };

        // Act
        await _endpoint.HandleAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(200, _endpoint.HttpContext.Response.StatusCode);
        Assert.Equal("application/zip", _endpoint.HttpContext.Response.ContentType);
        Assert.Equal("true", _endpoint.HttpContext.Response.Headers["X-Endatix-Empty-File"]);
        // Do not assert on the response body/ZIP content in unit test
    }

    [Fact]
    public async Task Returns404WhenNotFound()
    {
        // Arrange
        _mediator.Send(Arg.Any<GetFilesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.NotFound("Not found"));
        var request = new GetSubmissionFilesRequest { FormId = 1, SubmissionId = 123 };

        // Act
        await _endpoint.HandleAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(404, _endpoint.HttpContext.Response.StatusCode);
    }

    [Fact]
    public async Task Returns500OnException()
    {
        // Arrange
        _mediator.Send(Arg.Any<GetFilesQuery>(), Arg.Any<CancellationToken>())
            .Returns<Task<Result<GetFilesResult>>>(_ => throw new System.Exception("Test error"));
        var request = new GetSubmissionFilesRequest { FormId = 1, SubmissionId = 123 };

        // Act
        await _endpoint.HandleAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(500, _endpoint.HttpContext.Response.StatusCode);
    }

    [Fact]
    public async Task SendsZipOfEachFile()
    {
        // Arrange
        ReturnFiles(new MemoryStream(Encoding.UTF8.GetBytes("abc")), new MemoryStream(Encoding.UTF8.GetBytes("defg")));
        var body = new MemoryStream();
        _endpoint.HttpContext.Response.Body = body;
        var request = new GetSubmissionFilesRequest { FormId = 1, SubmissionId = 123 };

        // Act
        await _endpoint.HandleAsync(request, CancellationToken.None);

        // Assert
        body.Position = 0;
        using var archive = new ZipArchive(body, ZipArchiveMode.Read);
        Assert.Equal(["file1.txt", "file2.txt"], archive.Entries.Select(e => e.FullName));
        using var reader = new StreamReader(archive.GetEntry("file2.txt")!.Open());
        Assert.Equal("defg", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendsValidEmptyZipWhenNoFiles()
    {
        // Arrange
        ReturnFiles();
        var body = new MemoryStream();
        _endpoint.HttpContext.Response.Body = body;
        var request = new GetSubmissionFilesRequest { FormId = 1, SubmissionId = 123 };

        // Act
        await _endpoint.HandleAsync(request, CancellationToken.None);

        // Assert
        body.Position = 0;
        using var archive = new ZipArchive(body, ZipArchiveMode.Read);
        Assert.Empty(archive.Entries);
    }

    [Fact]
    public async Task FailureWhileBuildingZip_Returns500WithoutZipHeaders()
    {
        // Arrange
        ReturnFiles(new UnreadableStream());
        var request = new GetSubmissionFilesRequest { FormId = 1, SubmissionId = 123 };

        // Act
        await _endpoint.HandleAsync(request, CancellationToken.None);

        // Assert
        var response = _endpoint.HttpContext.Response;
        Assert.Equal(500, response.StatusCode);
        Assert.NotEqual("application/zip", response.ContentType);
        Assert.False(response.Headers.ContainsKey("Content-Disposition"));
        Assert.False(_lifetime.Aborted);
    }

    [Fact]
    public async Task FailureAfterResponseStarted_AbortsConnection()
    {
        // Arrange
        ReturnFiles(new MemoryStream(Encoding.UTF8.GetBytes("abc")));
        _endpoint.HttpContext.Response.Body = new UnwritableStream();
        var request = new GetSubmissionFilesRequest { FormId = 1, SubmissionId = 123 };

        // Act
        await _endpoint.HandleAsync(request, CancellationToken.None);

        // Assert
        Assert.True(_lifetime.Aborted);
    }

    [Fact]
    public async Task ClientCancellation_IsNotLoggedAsError()
    {
        // Arrange
        ReturnFiles(new MemoryStream(Encoding.UTF8.GetBytes("abc")));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var request = new GetSubmissionFilesRequest { FormId = 1, SubmissionId = 123 };

        // Act
        await _endpoint.HandleAsync(request, cts.Token);

        // Assert
        Assert.NotEqual(500, _endpoint.HttpContext.Response.StatusCode);
        Assert.False(_lifetime.Aborted);
        _logger.DidNotReceive().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ClientDisconnectWhileSending_IsNotLoggedAsError()
    {
        // Arrange
        ReturnFiles(new MemoryStream(Encoding.UTF8.GetBytes("abc")));
        using var cts = new CancellationTokenSource();
        _endpoint.HttpContext.Response.Body = new UnwritableStream(onWrite: cts.Cancel);
        var request = new GetSubmissionFilesRequest { FormId = 1, SubmissionId = 123 };

        // Act
        await _endpoint.HandleAsync(request, cts.Token);

        // Assert
        _logger.DidNotReceive().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task CopyFailure_DisposesFileStream()
    {
        // Arrange
        var content = new UnreadableStream();
        ReturnFiles(content);
        var request = new GetSubmissionFilesRequest { FormId = 1, SubmissionId = 123 };

        // Act
        await _endpoint.HandleAsync(request, CancellationToken.None);

        // Assert
        Assert.True(content.Disposed);
    }

    [Fact]
    public async Task CopyFailure_DisposesFilesNotYetCopied()
    {
        // Arrange
        var notYetCopied = new UnreadableStream();
        ReturnFiles(new UnreadableStream(), notYetCopied);
        var request = new GetSubmissionFilesRequest { FormId = 1, SubmissionId = 123 };

        // Act
        await _endpoint.HandleAsync(request, CancellationToken.None);

        // Assert
        Assert.True(notYetCopied.Disposed);
    }

    [Fact]
    public async Task CopyFailure_DisposesFilesAlreadyCopied()
    {
        // Arrange
        var copied = new DisposableStream("abc"u8.ToArray());
        ReturnFiles(copied, new UnreadableStream());
        var request = new GetSubmissionFilesRequest { FormId = 1, SubmissionId = 123 };

        // Act
        await _endpoint.HandleAsync(request, CancellationToken.None);

        // Assert
        Assert.True(copied.Disposed);
    }

    private void ReturnFiles(params Stream[] contents)
    {
        var files = contents.Select((content, i) => new FileDescriptor($"file{i + 1}.txt", "text/plain", content)).ToList();
        _mediator.Send(Arg.Any<GetFilesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new GetFilesResult("TestForm", 123, files)));
    }

    private sealed class AbortRecordingLifetimeFeature : IHttpRequestLifetimeFeature
    {
        public bool Aborted { get; private set; }
        public CancellationToken RequestAborted { get; set; }
        public void Abort() => Aborted = true;
    }

    private sealed class DisposableStream(byte[] data) : MemoryStream(data)
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class UnreadableStream : MemoryStream
    {
        public bool Disposed { get; private set; }
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("Storage read failed");
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new IOException("Storage read failed");
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class UnwritableStream(Action? onWrite = null) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count) => throw Reset();
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw Reset();

        private IOException Reset()
        {
            onWrite?.Invoke();
            return new IOException("Connection reset");
        }
    }
}
