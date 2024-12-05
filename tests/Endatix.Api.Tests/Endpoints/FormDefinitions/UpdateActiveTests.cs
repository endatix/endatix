using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.FormDefinitions;
using Endatix.Core.UseCases.FormDefinitions.UpdateActive;

namespace Endatix.Api.Tests.Endpoints.FormDefinitions;

public class UpdateActiveTests
{
    private readonly IMediator _mediator;
    private readonly UpdateActive _endpoint;

    public UpdateActiveTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<UpdateActive>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var formId = 1L;
        var request = new UpdateActiveFormDefinitionRequest
        {
            FormId = formId,
            IsDraft = false,
            JsonData = "{ }",
            IsActive = true
        };
        var result = Result.Invalid();
        
        _mediator.Send(Arg.Any<UpdateActiveFormDefinitionCommand>(), Arg.Any<CancellationToken>())
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
        var request = new UpdateActiveFormDefinitionRequest
        {
            FormId = formId,
            IsDraft = false,
            JsonData = "{ }",
            IsActive = true
        };
        var result = Result.NotFound("Active form definition not found");

        _mediator.Send(Arg.Any<UpdateActiveFormDefinitionCommand>(), Arg.Any<CancellationToken>())
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
        var request = new UpdateActiveFormDefinitionRequest 
        { 
            FormId = formId,
            IsDraft = false,
            JsonData = "{ }",
            IsActive = true
        };
        
        var formDefinition = new FormDefinition(false, "{ }", true) { Id = 1, FormId = formId };
        var result = Result.Success(formDefinition);

        _mediator.Send(Arg.Any<UpdateActiveFormDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<UpdateActiveFormDefinitionResponse>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value!.Id.Should().Be(formDefinition.Id.ToString());
        okResult!.Value!.FormId.Should().Be(formDefinition.FormId.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToCommandCorrectly()
    {
        // Arrange
        var request = new UpdateActiveFormDefinitionRequest
        {
            FormId = 123,
            IsDraft = false,
            JsonData = "{ }",
            IsActive = true
        };
        var result = Result.Success(new FormDefinition(false, "{ }", true));
        
        _mediator.Send(Arg.Any<UpdateActiveFormDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<UpdateActiveFormDefinitionCommand>(cmd =>
                cmd.FormId == request.FormId &&
                cmd.IsDraft == request.IsDraft &&
                cmd.JsonData == request.JsonData &&
                cmd.IsActive == request.IsActive
            ),
            Arg.Any<CancellationToken>()
        );
    }
}
