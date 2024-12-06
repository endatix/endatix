using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.Submissions;
using Endatix.Core.UseCases.Submissions.ListByFormId;

namespace Endatix.Api.Tests.Endpoints.Submissions;

public class ListByFormIdTests
{
    private readonly IMediator _mediator;
    private readonly ListByFormId _endpoint;

    public ListByFormIdTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<ListByFormId>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var formId = 1L;
        var request = new ListByFormIdRequest { FormId = formId };
        var result = Result.Invalid();
        
        _mediator.Send(Arg.Any<ListByFormIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var badRequestResult = response.Result as BadRequest;
        badRequestResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_FormNotFound_ReturnsNotFound()
    {
        // Arrange
        var formId = 1L;
        var request = new ListByFormIdRequest { FormId = formId };
        var result = Result.NotFound("Form not found");

        _mediator.Send(Arg.Any<ListByFormIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var notFoundResult = response.Result as NotFound;
        notFoundResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithSubmissions()
    {
        // Arrange
        var formId = 1L;
        var request = new ListByFormIdRequest { FormId = formId, Page = 1, PageSize = 10 };
        var submissions = new List<Submission> 
        { 
            new("{ }", 1, 2) { Id = 1 },
            new("{ }", 1, 2) { Id = 2 }
        };
        var result = Result.Success(submissions.AsEnumerable());

        _mediator.Send(Arg.Any<ListByFormIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<IEnumerable<SubmissionModel>>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value!.Count().Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToQueryCorrectly()
    {
        // Arrange
        var request = new ListByFormIdRequest
        {
            FormId = 123,
            Page = 2,
            PageSize = 20
        };
        var result = Result.Success(Enumerable.Empty<Submission>());
        
        _mediator.Send(Arg.Any<ListByFormIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<ListByFormIdQuery>(query =>
                query.FormId == request.FormId &&
                query.Page == request.Page &&
                query.PageSize == request.PageSize
            ),
            Arg.Any<CancellationToken>()
        );
    }
}
