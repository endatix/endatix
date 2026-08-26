using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.CustomQuestions.List;

namespace Endatix.Core.Tests.UseCases.CustomQuestions.List;

public class ListCustomQuestionsHandlerTests
{
    private readonly IRepository<CustomQuestion> _repository;
    private readonly ListCustomQuestionsHandler _handler;

    public ListCustomQuestionsHandlerTests()
    {
        _repository = Substitute.For<IRepository<CustomQuestion>>();
        _handler = new ListCustomQuestionsHandler(_repository);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsCustomQuestions()
    {
        // Arrange
        var questions = new List<CustomQuestion>
        {
            new(SampleData.TENANT_ID, "Question 1", "{ \"type\": \"text\" }", "Description 1"),
            new(SampleData.TENANT_ID, "Question 2", "{ \"type\": \"number\" }", "Description 2")
        };

        var request = new ListCustomQuestionsQuery();
        _repository.CountAsync(Arg.Any<CustomQuestionSpecifications.ListFilter>(), Arg.Any<CancellationToken>())
            .Returns(2);
        _repository.ListAsync(Arg.Any<CustomQuestionSpecifications.ListSpec>(), Arg.Any<CancellationToken>())
            .Returns(questions);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value!.Items.Should().BeEquivalentTo(questions);
        result.Value.TotalRecords.Should().Be(2);

        await _repository.Received(1).CountAsync(
            Arg.Any<CustomQuestionSpecifications.ListFilter>(),
            Arg.Any<CancellationToken>());
        await _repository.Received(1).ListAsync(
            Arg.Any<CustomQuestionSpecifications.ListSpec>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoQuestions_ReturnsEmptyList()
    {
        // Arrange
        var request = new ListCustomQuestionsQuery();
        _repository.CountAsync(Arg.Any<CustomQuestionSpecifications.ListFilter>(), Arg.Any<CancellationToken>())
            .Returns(0);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value!.Items.Should().BeEmpty();
        result.Value.TotalRecords.Should().Be(0);

        await _repository.Received(1).CountAsync(
            Arg.Any<CustomQuestionSpecifications.ListFilter>(),
            Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().ListAsync(
            Arg.Any<CustomQuestionSpecifications.ListSpec>(),
            Arg.Any<CancellationToken>());
    }
}
