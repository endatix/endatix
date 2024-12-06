using Endatix.Api.Endpoints.FormDefinitions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.FormDefinitions.Create;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.FormDefinitions;

public class CreateTests
{
    private readonly IMediator _mediator;
    private readonly Create _endpoint;

    public CreateTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<Create>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidData_ReturnsCreatedResult()
    {
        // Arrange
        var request = new CreateFormDefinitionRequest
        {
            FormId = 1,
            IsDraft = true,
            JsonData = "{\"field\": \"value\"}"
        };

        var command = new CreateFormDefinitionCommand(
            request.FormId,
            request.IsDraft!.Value,
            request.JsonData!
        );

        var createdFormDefinition = new FormDefinition(command.IsDraft, command.JsonData)
        {
            Id = 2
        };
        var successResult = Result<FormDefinition>.Created(createdFormDefinition);
        _mediator.Send(command, Arg.Any<CancellationToken>())
            .Returns(successResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var createdResult = response.Result.As<Created<CreateFormDefinitionResponse>>();
        createdResult.Should().NotBeNull();
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        createdResult.Value.Should().NotBeNull();
        createdResult.Value?.Id.Should().Be("2");
        createdResult.Value?.IsDraft.Should().Be(request.IsDraft!.Value);
        createdResult.Value?.JsonData.Should().Be(request.JsonData);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidData_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateFormDefinitionRequest
        {
            FormId = 1,
            IsDraft = true,
            JsonData = "invalid-json"
        };

        var command = new CreateFormDefinitionCommand(
            request.FormId,
            request.IsDraft!.Value,
            request.JsonData!
        );

        var errorResult = Result.Invalid(new ValidationError("Invalid JSON format"));
        _mediator.Send(command, Arg.Any<CancellationToken>())
            .Returns(errorResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var badRequestResult = response.Result.As<BadRequest>();
        badRequestResult.Should().NotBeNull();
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentForm_ReturnsNotFound()
    {
        // Arrange
        var request = new CreateFormDefinitionRequest
        {
            FormId = 999,
            IsDraft = true,
            JsonData = "{\"field\": \"value\"}"
        };

        var command = new CreateFormDefinitionCommand(
            request.FormId,
            request.IsDraft!.Value,
            request.JsonData!
        );

        var notFoundResult = Result.NotFound("Form not found");
        _mediator.Send(command, Arg.Any<CancellationToken>())
            .Returns(notFoundResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var notFoundResponse = response.Result.As<NotFound>();
        notFoundResponse.Should().NotBeNull();
        notFoundResponse.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}
