using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.UseCases.Submissions.UpdateStatus;
using NSubstitute.ExceptionExtensions;

namespace Endatix.Core.Tests.UseCases.Submissions.UpdateStatus;

public class UpdateStatusHandlerTests
{
    private readonly IRepository<Submission> _submissionRepository;
    private readonly UpdateStatusHandler _handler;

    public UpdateStatusHandlerTests()
    {
        _submissionRepository = Substitute.For<IRepository<Submission>>();
        _handler = new UpdateStatusHandler(_submissionRepository);
    }

    [Fact]
    public async Task Handle_WhenSubmissionNotFound_ReturnsNotFound()
    {
        // Arrange
        var command = new UpdateStatusCommand(1, 1, "new");
        _submissionRepository.GetByIdAsync(command.SubmissionId, Arg.Any<CancellationToken>())
            .Returns((Submission?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Errors.First().Should().Contain("Submission not found");
        await _submissionRepository.DidNotReceive()
            .UpdateAsync(Arg.Any<Submission>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenFormIdDoesNotMatch_ReturnsNotFound()
    {
        // Arrange
        var requestFormId = 999;
        var actualFormId = 1;
        var command = new UpdateStatusCommand(1, requestFormId, "new");
        var submission = new Submission("data", 1, actualFormId);

        _submissionRepository.GetByIdAsync(command.SubmissionId, Arg.Any<CancellationToken>())
            .Returns(submission);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Submission not found");
        await _submissionRepository.DidNotReceive()
            .UpdateAsync(Arg.Any<Submission>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenInvalidStatusCode_ReturnsInvalid()
    {
        // Arrange
        var command = new UpdateStatusCommand(1, 1, "invalid_status");
        var submission = new Submission("data", 1, 1);

        _submissionRepository.GetByIdAsync(command.SubmissionId, Arg.Any<CancellationToken>())
            .Returns(submission);
    
        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainSingle(e => e.ErrorMessage.Contains("Invalid status code provided"));
        await _submissionRepository.DidNotReceive()
            .UpdateAsync(Arg.Any<Submission>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenValidStatusChange_ReturnsSuccess()
    {
        // Arrange
        var command = new UpdateStatusCommand(1, 1, "approved");
        var submission = new Submission("{}", 1, 1, isComplete: true);

        _submissionRepository.GetByIdAsync(command.SubmissionId, Arg.Any<CancellationToken>())
            .Returns(submission);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(submission.Id);
        result.Value.Status.Should().Be("approved");

        await _submissionRepository.Received(1)
            .UpdateAsync(Arg.Is<Submission>(s => s.Id == submission.Id), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("new")]
    [InlineData("approved")]
    public async Task Handle_WithDifferentValidStatuses_UpdatesSuccessfully(string statusCode)
    {
        // Arrange
        var command = new UpdateStatusCommand(1, 1, statusCode);
        var submission = new Submission("{}", 1, 1, isComplete: true);

        _submissionRepository.GetByIdAsync(command.SubmissionId, Arg.Any<CancellationToken>())
            .Returns(submission);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Status.Should().Be(statusCode);

        await _submissionRepository.Received(1)
            .UpdateAsync(Arg.Is<Submission>(s => s.Id == submission.Id), Arg.Any<CancellationToken>());
    }
}