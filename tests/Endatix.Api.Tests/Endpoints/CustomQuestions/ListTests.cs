using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.CustomQuestions;
using Endatix.Core.UseCases.CustomQuestions.List;

namespace Endatix.Api.Tests.Endpoints.CustomQuestions;

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
        var request = new EmptyRequest();
        var result = Result.Invalid();

        _mediator.Send(Arg.Any<ListCustomQuestionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var badRequestResult = response.Result as BadRequest;
        badRequestResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithCustomQuestions()
    {
        // Arrange
        var request = new EmptyRequest();
        var questions = new List<CustomQuestion>
        {
            new(SampleData.TENANT_ID, "Question 1", "{ \"type\": \"text\" }", "Description 1") { Id = 1 },
            new(SampleData.TENANT_ID, "Question 2", "{ \"type\": \"number\" }", "Description 2") { Id = 2 }
        };
        var result = Result.Success(questions.AsEnumerable());

        _mediator.Send(Arg.Any<ListCustomQuestionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<IEnumerable<CustomQuestionModel>>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value.Should().HaveCount(2);
        okResult!.Value!.First().Id.Should().Be("1");
        okResult!.Value!.First().Name.Should().Be("Question 1");
        okResult!.Value!.First().JsonData.Should().Be( "{ \"type\": \"text\" }");
        okResult!.Value!.First().Description.Should().Be("Description 1");
    }

    [Fact]
    public async Task ExecuteAsync_NoQuestions_ReturnsEmptyList()
    {
        // Arrange
        var request = new EmptyRequest();
        var result = Result.Success(Enumerable.Empty<CustomQuestion>());

        _mediator.Send(Arg.Any<ListCustomQuestionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<IEnumerable<CustomQuestionModel>>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value.Should().BeEmpty();
    }
} 