using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Api.Endpoints.FormDefinitions;
using Endatix.Core.UseCases.FormDefinitions;
using Endatix.Core.UseCases.FormDefinitions.GetFields;

namespace Endatix.Api.Tests.Endpoints.FormDefinitions;

public class GetFieldsTests
{
    private readonly IMediator _mediator;
    private readonly GetFields _endpoint;

    public GetFieldsTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<GetFields>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsProblemDetails()
    {
        // Arrange
        var formId = 1L;
        var request = new GetFieldsRequest { FormId = formId };
        var result = Result.Invalid();

        _mediator.Send(Arg.Any<GetFormDefinitionFieldsQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ExecuteAsync_NoDefinitionsFound_ReturnsProblemDetails()
    {
        // Arrange
        var formId = 1L;
        var request = new GetFieldsRequest { FormId = formId };
        var result = Result.NotFound("No form definitions found for the given form.");

        _mediator.Send(Arg.Any<GetFormDefinitionFieldsQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithFields()
    {
        // Arrange
        var formId = 1L;
        var request = new GetFieldsRequest { FormId = formId };
        var fields = new List<DefinitionFieldDto>
        {
            new("name", "Full Name", "text"),
            new("email", "Email Address", "text"),
            new("rating", "Satisfaction Rating", "rating")
        };
        var result = Result.Success(fields.AsEnumerable());

        _mediator.Send(Arg.Any<GetFormDefinitionFieldsQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<IEnumerable<DefinitionFieldModel>>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value!.Count().Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToQueryCorrectly()
    {
        // Arrange
        var request = new GetFieldsRequest { FormId = 123 };
        var result = Result.Success(Enumerable.Empty<DefinitionFieldDto>());

        _mediator.Send(Arg.Any<GetFormDefinitionFieldsQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<GetFormDefinitionFieldsQuery>(query =>
                query.FormId == request.FormId
            ),
            Arg.Any<CancellationToken>()
        );
    }
}
