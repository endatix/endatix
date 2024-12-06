using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.FormDefinitions;
using Endatix.Core.UseCases.FormDefinitions.GetById;
using Endatix.Infrastructure.Tests.TestUtils;

namespace Endatix.Api.Tests.Endpoints.FormDefinitions;

public class GetByIdTests
{
    private readonly IMediator _mediator;
    private readonly GetById _endpoint;

    public GetByIdTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<GetById>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var formId = 1L;
        var definitionId = 1L;
        var request = new GetFormDefinitionByIdRequest { FormId = formId, DefinitionId = definitionId };
        var result = Result.Invalid();
        
        _mediator.Send(Arg.Any<GetFormDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var badRequestResult = response.Result as BadRequest;
        badRequestResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_FormDefinitionNotFound_ReturnsNotFound()
    {
        // Arrange
        var formId = 1L;
        var definitionId = 1L;
        var request = new GetFormDefinitionByIdRequest { FormId = formId, DefinitionId = definitionId };
        var result = Result.NotFound("Form definition not found");

        _mediator.Send(Arg.Any<GetFormDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var notFoundResult = response.Result as NotFound;
        notFoundResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithFormDefinition()
    {
        // Arrange
        var formId = 1L;
        var definitionId = 1L;
        var request = new GetFormDefinitionByIdRequest { FormId = formId, DefinitionId = definitionId };
        var formDefinition = FormDefinitionFactory.CreateForTesting(true, "{ }", formId, definitionId);
        var result = Result.Success(formDefinition);

        _mediator.Send(Arg.Any<GetFormDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<FormDefinitionModel>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value!.Id.Should().Be(definitionId.ToString());
        okResult!.Value!.FormId.Should().Be(formId.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToQueryCorrectly()
    {
        // Arrange
        var request = new GetFormDefinitionByIdRequest { FormId = 123, DefinitionId = 456 };
        var result = Result.Success(new FormDefinition(true, "{ }"));
        
        _mediator.Send(Arg.Any<GetFormDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<GetFormDefinitionByIdQuery>(query =>
                query.FormId == request.FormId &&
                query.DefinitionId == request.DefinitionId
            ),
            Arg.Any<CancellationToken>()
        );
    }
}
