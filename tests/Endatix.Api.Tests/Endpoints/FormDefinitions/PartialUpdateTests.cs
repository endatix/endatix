using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.FormDefinitions;
using Endatix.Core.UseCases.FormDefinitions.PartialUpdate;

namespace Endatix.Api.Tests.Endpoints.FormDefinitions;

public class PartialUpdateTests
{
    private readonly IMediator _mediator;
    private readonly PartialUpdate _endpoint;

    public PartialUpdateTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<PartialUpdate>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var formId = 1L;
        var definitionId = 1L;
        var request = new PartialUpdateFormDefinitionRequest { FormId = formId, DefinitionId = definitionId };
        var result = Result.Invalid();
        
        _mediator.Send(Arg.Any<PartialUpdateFormDefinitionCommand>(), Arg.Any<CancellationToken>())
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
        var request = new PartialUpdateFormDefinitionRequest { FormId = formId, DefinitionId = definitionId };
        var result = Result.NotFound("Form definition not found");

        _mediator.Send(Arg.Any<PartialUpdateFormDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var notFoundResult = response.Result as NotFound;
        notFoundResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithUpdatedFormDefinition()
    {
        // Arrange
        var formId = 1L;
        var definitionId = 1L;
        var request = new PartialUpdateFormDefinitionRequest 
        { 
            FormId = formId,
            DefinitionId = definitionId,
            IsDraft = false,
            JsonData = "{ }"
        };
        
        var formDefinition = new FormDefinition(false, "{ }") { Id = definitionId, FormId = formId };
        var result = Result.Success(formDefinition);

        _mediator.Send(Arg.Any<PartialUpdateFormDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<PartialUpdateFormDefinitionResponse>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value!.Id.Should().Be(definitionId.ToString());
        okResult!.Value!.FormId.Should().Be(formId.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToCommandCorrectly()
    {
        // Arrange
        var request = new PartialUpdateFormDefinitionRequest
        {
            FormId = 123,
            DefinitionId = 456,
            IsDraft = false,
            JsonData = "{ }"
        };
        var result = Result.Success(new FormDefinition(false, "{ }"));
        
        _mediator.Send(Arg.Any<PartialUpdateFormDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<PartialUpdateFormDefinitionCommand>(cmd =>
                cmd.FormId == request.FormId &&
                cmd.DefinitionId == request.DefinitionId &&
                cmd.IsDraft == request.IsDraft &&
                cmd.JsonData == request.JsonData
            ),
            Arg.Any<CancellationToken>()
        );
    }
} 