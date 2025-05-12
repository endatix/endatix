using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.CustomQuestions;
using Endatix.Core.UseCases.CustomQuestions.Create;

namespace Endatix.Api.Tests.Endpoints.CustomQuestions;

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
    public async Task ExecuteAsync_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateCustomQuestionRequest
        {
            Name = "Test Question",
            Description = "Test Description",
            JsonData = "{ }"
        };

        var result = Result.Invalid();

        _mediator.Send(Arg.Any<CreateCustomQuestionCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var badRequestResult = response.Result as BadRequest;
        badRequestResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsCreatedWithCustomQuestion()
    {
        // Arrange
        var request = new CreateCustomQuestionRequest
        {
            Name = "Test Question",
            Description = "Test Description",
            JsonData = "{ \"type\": \"text\" }"
        };

        var customQuestion = new CustomQuestion(SampleData.TENANT_ID, request.Name!, request.JsonData!, request.Description) { Id = 1 };
        var result = Result<CustomQuestion>.Created(customQuestion);

        _mediator.Send(Arg.Any<CreateCustomQuestionCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var createdResult = response.Result as Created<CreateCustomQuestionResponse>;
        createdResult.Should().NotBeNull();
        createdResult!.Value.Should().NotBeNull();
        createdResult!.Value!.Id.Should().Be(customQuestion.Id.ToString());
        createdResult!.Value!.Name.Should().Be(customQuestion.Name);
        createdResult!.Value!.Description.Should().Be(customQuestion.Description);
        createdResult!.Value!.JsonData.Should().Be(customQuestion.JsonData);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToCommandCorrectly()
    {
        // Arrange
        var request = new CreateCustomQuestionRequest
        {
            Name = "Test Question",
            Description = "Test Description",
            JsonData = "{ \"type\": \"text\" }"
        };
        var result = Result<CustomQuestion>.Created(new CustomQuestion(SampleData.TENANT_ID, "Test Question", "{ \"type\": \"text\" }"));

        _mediator.Send(Arg.Any<CreateCustomQuestionCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<CreateCustomQuestionCommand>(cmd =>
                cmd.Name == request.Name &&
                cmd.Description == request.Description &&
                cmd.JsonData == request.JsonData
            ),
            Arg.Any<CancellationToken>()
        );
    }
} 