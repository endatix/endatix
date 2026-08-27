using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.FormDefinitions;
using Endatix.Core.UseCases.FormDefinitions.List;
using Endatix.Api.Tests.TestUtils;

namespace Endatix.Api.Tests.Endpoints.FormDefinitions;

public class ListTests
{
    private readonly IMediator _mediator;
    private readonly List _endpoint;

    public ListTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<List>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var formId = 1L;
        var request = new FormDefinitionsListRequest { FormId = formId };
        var result = Result.Invalid();

        _mediator.Send(Arg.Any<ListFormDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithPagedFormDefinitions()
    {
        // Arrange
        var formId = 1L;
        var request = new FormDefinitionsListRequest { FormId = formId };
        var formDefinitions = new List<FormDefinition>
        {
            FormDefinitionFactory.CreateForTesting(true, "{ }", formId, 1),
            FormDefinitionFactory.CreateForTesting(false, "{ }", formId, 2)
        };
        var paged = Paged<FormDefinition>.FromPage(1, 10, 2, formDefinitions);
        var result = Result.Success(paged);

        _mediator.Send(Arg.Any<ListFormDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<Paged<FormDefinitionModel>>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult.Value!.Items.Should().HaveCount(2);
        okResult.Value.TotalRecords.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_FormNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new FormDefinitionsListRequest { FormId = 1 };
        var result = Result<Paged<FormDefinition>>.NotFound("Form not found.");

        _mediator.Send(Arg.Any<ListFormDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToQueryCorrectly()
    {
        // Arrange
        var request = new FormDefinitionsListRequest
        {
            FormId = 123,
            Page = 2,
            PageSize = 20
        };
        var result = Result.Success(Paged<FormDefinition>.FromPage(
            1,
            20,
            1,
            [FormDefinitionFactory.CreateForTesting(true, "{ }", 123, 1)]));

        _mediator.Send(Arg.Any<ListFormDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<ListFormDefinitionsQuery>(query =>
                query.FormId == request.FormId &&
                query.Page == request.Page &&
                query.PageSize == request.PageSize &&
                query.SortBy == FormDefinitionListSortBy.CreatedAt &&
                query.SortDescending
            ),
            Arg.Any<CancellationToken>()
        );
    }
}
