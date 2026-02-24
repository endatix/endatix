using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Api.Endpoints.Submissions;
using Endatix.Core.UseCases.Submissions.ListByFormId;
using Endatix.Core.UseCases.Submissions;

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
    public async Task ExecuteAsync_InvalidRequest_ReturnsProblemDetails()
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
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_FormNotFound_ReturnsProblemDetails()
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
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithSubmissions()
    {
        // Arrange
        var formId = 1L;
        var request = new ListByFormIdRequest { FormId = formId, Page = 1, PageSize = 10 };
        var submissions = new List<SubmissionDto>
        {
            new(3, false, "{}", 1, 2, 5, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(-5), "{ }", "new", null),
            new(4, false, "{}", 1, 2, 6, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(-10), "{ }", "new", "7"),
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
            PageSize = 20,
            Filter = ["expression1", "expression1"]
        };
        var result = Result.Success(Enumerable.Empty<SubmissionDto>());
        
        _mediator.Send(Arg.Any<ListByFormIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<ListByFormIdQuery>(query =>
                query.FormId == request.FormId &&
                query.Page == request.Page &&
                query.PageSize == request.PageSize &&
                query.FilterExpressions == request.Filter
            ),
            Arg.Any<CancellationToken>()
        );
    }
}
