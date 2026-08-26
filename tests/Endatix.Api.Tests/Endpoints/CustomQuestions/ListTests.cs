using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.CustomQuestions;
using Endatix.Core.UseCases.CustomQuestions.List;
using Endatix.Core.Infrastructure.Paging;

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
        var request = new CustomQuestionsListRequest();
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
        var request = new CustomQuestionsListRequest { Page = 1, PageSize = 10 };
        var questions = new List<CustomQuestion>
        {
            new(SampleData.TENANT_ID, "Question 1", "{ \"type\": \"text\" }", "Description 1") { Id = 1 },
            new(SampleData.TENANT_ID, "Question 2", "{ \"type\": \"number\" }", "Description 2") { Id = 2 }
        };
        var result = Result.Success(new Paged<CustomQuestion>(1, 10, 2, 1, questions));

        _mediator.Send(Arg.Any<ListCustomQuestionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<Paged<CustomQuestionModel>>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value!.Items.Should().HaveCount(2);
        okResult!.Value!.Items.First().Id.Should().Be("1");
        okResult!.Value!.Items.First().Name.Should().Be("Question 1");
        okResult!.Value!.Items.First().JsonData.Should().Be("{ \"type\": \"text\" }");
        okResult!.Value!.Items.First().Description.Should().Be("Description 1");
    }

    [Fact]
    public async Task ExecuteAsync_NoQuestions_ReturnsEmptyList()
    {
        // Arrange
        var request = new CustomQuestionsListRequest();
        var result = Result.Success(Paged<CustomQuestion>.Empty(10));

        _mediator.Send(Arg.Any<ListCustomQuestionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<Paged<CustomQuestionModel>>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapSortAndDatesToQuery()
    {
        // Arrange
        var request = new CustomQuestionsListRequest
        {
            Page = 1,
            PageSize = 20,
            SortBy = CustomQuestionListSortBy.ModifiedAt,
            SortDir = SortDirection.Asc,
            CreatedFrom = "2026-01-01",
            CreatedTo = "2026-01-31"
        };
        _mediator.Send(Arg.Any<ListCustomQuestionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Paged<CustomQuestion>.Empty(20)));

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<ListCustomQuestionsQuery>(q =>
                q.Page == 1 &&
                q.PageSize == 20 &&
                q.SortBy == CustomQuestionListSortBy.ModifiedAt &&
                q.SortDescending == false &&
                q.CreatedFrom == new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) &&
                q.CreatedTo == new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)),
            Arg.Any<CancellationToken>());
    }
}
