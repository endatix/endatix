using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.FormDefinitions;
using Endatix.Core.UseCases.FormDefinitions.Update;

namespace Endatix.Api.Tests.Endpoints.FormDefinitions;

public class UpdateTests
{
    private readonly IMediator _mediator;
    private readonly Update _endpoint;

    public UpdateTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<Update>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var formId = 1L;
        var definitionId = 1L;
        var request = new UpdateFormDefinitionRequest
        {
            FormId = formId,
            DefinitionId = definitionId,
            IsDraft = false,
            JsonData = "{ }",
            IsActive = true
        };
        var result = Result.Invalid();
        
        _mediator.Send(Arg.Any<UpdateFormDefinitionCommand>(), Arg.Any<CancellationToken>())
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
        var request = new UpdateFormDefinitionRequest
        {
            FormId = formId,
            DefinitionId = definitionId,
            IsDraft = false,
            JsonData = "{ }",
            IsActive = true
        };
        var result = Result.NotFound("Form definition not found");

        _mediator.Send(Arg.Any<UpdateFormDefinitionCommand>(), Arg.Any<CancellationToken>())
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
        var request = new UpdateFormDefinitionRequest 
        { 
            FormId = formId,
            DefinitionId = definitionId,
            IsDraft = false,
            JsonData = "{ }",
            IsActive = true
        };
        
        var formDefinition = new FormDefinition(false, "{ }", true) { Id = definitionId, FormId = formId };
        var result = Result.Success(formDefinition);

        _mediator.Send(Arg.Any<UpdateFormDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<UpdateFormDefinitionResponse>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value!.Id.Should().Be(definitionId.ToString());
        okResult!.Value!.FormId.Should().Be(formId.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToCommandCorrectly()
    {
        // Arrange
        var request = new UpdateFormDefinitionRequest
        {
            FormId = 123,
            DefinitionId = 456,
            IsDraft = false,
            JsonData = "{ }",
            IsActive = true
        };
        var result = Result.Success(new FormDefinition(false, "{ }", true));
        
        _mediator.Send(Arg.Any<UpdateFormDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<UpdateFormDefinitionCommand>(cmd =>
                cmd.FormId == request.FormId &&
                cmd.DefinitionId == request.DefinitionId &&
                cmd.IsDraft == request.IsDraft &&
                cmd.JsonData == request.JsonData &&
                cmd.IsActive == request.IsActive
            ),
            Arg.Any<CancellationToken>()
        );
    }
}
