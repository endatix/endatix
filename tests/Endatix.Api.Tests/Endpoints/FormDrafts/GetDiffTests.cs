using Endatix.Api.Endpoints.FormDrafts;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.FormDrafts;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.FormDrafts;

public sealed class GetDiffTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private static DraftDiffRequest Request => new() { FormId = 15, Against = "published" };

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(503)]
    public async Task ExecuteAsync_ReadFailure_ReturnsProblemWithoutDraftTag(int status)
    {
        // Arrange
        Result<DraftDiffResponse> result = status switch
        {
            400 => Result.Invalid(),
            404 => Result.NotFound(),
            _ => Result.Unavailable("Draft upgrade pending.")
        };
        _mediator.Send(new ReadDraftDiff(15), Arg.Any<CancellationToken>()).Returns(result);
        var endpoint = Factory.Create<GetDiff>(_mediator);

        // Act
        var response = await endpoint.ExecuteAsync(Request, TestContext.Current.CancellationToken);

        // Assert
        response.Result.Should().BeOfType<ProblemHttpResult>().Which.StatusCode.Should().Be(status);
        endpoint.HttpContext.Response.Headers.ETag.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_FirstPublication_ReturnsCurrentDraftTagAndDisablesCaching()
    {
        // Arrange
        var firstVersion = new DraftDiffResponse(9, null, []);
        _mediator.Send(new ReadDraftDiff(15), Arg.Any<CancellationToken>()).Returns(Result.Success(firstVersion));
        var endpoint = Factory.Create<GetDiff>(_mediator);

        // Act
        var response = await endpoint.ExecuteAsync(Request, TestContext.Current.CancellationToken);

        // Assert
        response.Result.Should().BeOfType<Ok<DraftDiffResponse>>().Which.Value.Should().Be(firstVersion);
        endpoint.HttpContext.Response.Headers.ETag.ToString().Should().Be("\"9\"");
        endpoint.HttpContext.Response.Headers.CacheControl.ToString().Should().Be("no-store");
    }
}
