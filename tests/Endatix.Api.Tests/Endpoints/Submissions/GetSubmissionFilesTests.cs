using System.IO.Compression;
using System.Text;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Submissions.GetFiles;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Endatix.Api.Endpoints.Submissions;

public class GetSubmissionFilesTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly GetSubmissionFiles _endpoint;

    public GetSubmissionFilesTests()
    {
        var httpContext = new DefaultHttpContext();
        _endpoint = Factory.Create<GetSubmissionFiles>(httpContext: httpContext, _mediator);
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
}