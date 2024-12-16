using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Submissions.PartialUpdate;
using MediatR;

namespace Endatix.Core.Tests.UseCases.Submissions.PartialUpdate;

public class PartialUpdateSubmissionHandlerTests
{
    private readonly IRepository<Submission> _repository;
    private readonly PartialUpdateSubmissionHandler _handler;
    private readonly IMediator _mediator;

    public PartialUpdateSubmissionHandlerTests()
    {
        _repository = Substitute.For<IRepository<Submission>>();
        _mediator = Substitute.For<IMediator>();
        _handler = new PartialUpdateSubmissionHandler(_repository, _mediator);
    }

    [Fact]
    public async Task Handle_SubmissionNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var request = new PartialUpdateSubmissionCommand(1, 1, null, null, null, null);
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
        var request = new PartialUpdateSubmissionCommand(
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
        result.Value.CurrentPage.Should().Be(request.CurrentPage!.Value);
        result.Value.Metadata.Should().Be(request.Metadata);
        result.Value.FormId.Should().Be(request.FormId);
        
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
