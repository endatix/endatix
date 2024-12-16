using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Submissions;
using MediatR;

namespace Endatix.Core.Tests.UseCases.Submissions.Update;

public class UpdateSubmissionHandlerTests
{
    private readonly IRepository<Submission> _repository;
    private readonly UpdateSubmissionHandler _handler;
    private readonly IMediator _mediator;

    public UpdateSubmissionHandlerTests()
    {
        _repository = Substitute.For<IRepository<Submission>>();
        _handler = new UpdateSubmissionHandler(_repository, _mediator);
        _mediator = Substitute.For<IMediator>();
    }

    [Fact]
    public async Task Handle_SubmissionNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var request = new UpdateSubmissionCommand(1, 1, true, 2, "{ }", "metadata");
        _repository.SingleOrDefaultAsync(
            Arg.Any<SubmissionByFormIdAndSubmissionIdSpec>(), 
            Arg.Any<CancellationToken>())
            .Returns((Submission?)null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Form submission not found.");
    }

    [Fact]
    public async Task Handle_ValidRequest_UpdatesSubmission()
    {
        // Arrange
        var submission = new Submission("{ }", 2, 3) { Id = 1 };
        var request = new UpdateSubmissionCommand(
            1, 2, true, 1, "{ \"updated\": true }", "metadata"
        );
        
        _repository.SingleOrDefaultAsync(
            Arg.Any<SubmissionByFormIdAndSubmissionIdSpec>(), 
            Arg.Any<CancellationToken>())
            .Returns(submission);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.JsonData.Should().Be(request.JsonData);
        result.Value.IsComplete.Should().Be(request.IsComplete!.Value);
        result.Value.CurrentPage.Should().Be(request.CurrentPage);
        result.Value.Metadata.Should().Be(request.Metadata);
        result.Value.FormId.Should().Be(request.FormId);
        
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
