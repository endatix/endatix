using FluentAssertions;
using MediatR;
using NSubstitute;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Submissions.PartialUpdate;
using Endatix.Core.UseCases.Submissions.PartialUpdateByToken;

namespace Endatix.Core.Tests.UseCases.Submissions.PartialUpdateByToken;

public class PartialUpdateSubmissionByTokenHandlerTests
{
    private readonly ISender _sender;
    private readonly ISubmissionTokenService _tokenService;
    private readonly PartialUpdateSubmissionByTokenHandler _handler;

    public PartialUpdateSubmissionByTokenHandlerTests()
    {
        _sender = Substitute.For<ISender>();
        _tokenService = Substitute.For<ISubmissionTokenService>();
        _handler = new PartialUpdateSubmissionByTokenHandler(_sender, _tokenService);
    }

    [Fact]
    public async Task Handle_InvalidToken_ReturnsNotFoundResult()
    {
        // Arrange
        var token = "invalid-token";
        var formId = 1L;
        var request = new PartialUpdateSubmissionByTokenCommand(token, formId, null, null, null, null);
        var tokenResult = Result.NotFound("Invalid or expired token");

        _tokenService.ResolveTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(tokenResult);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Invalid or expired token");
    }

    [Fact]
    public async Task Handle_ValidToken_UpdatesSubmissionSuccessfully()
    {
        // Arrange
        var token = "valid-token";
        var formId = 1L;
        var submissionId = 123L;
        var isComplete = true;
        var currentPage = 2;
        var jsonData = SampleData.SUBMISSION_JSON_DATA_1;
        var metadata = "test metadata";
        
        var request = new PartialUpdateSubmissionByTokenCommand(
            token, formId, isComplete, currentPage, jsonData, metadata);
        
        var tokenResult = Result.Success(submissionId);
        var submission = new Submission(jsonData) { Id = submissionId };
        var updateResult = Result.Success(submission);

        _tokenService.ResolveTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(tokenResult);
        _sender.Send(Arg.Any<PartialUpdateSubmissionCommand>(), Arg.Any<CancellationToken>())
            .Returns(updateResult);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(submission);

        await _tokenService.Received(1).ObtainTokenAsync(submissionId, Arg.Any<CancellationToken>());
        
        await _sender.Received(1).Send(
            Arg.Is<PartialUpdateSubmissionCommand>(cmd =>
                cmd.SubmissionId == submissionId &&
                cmd.FormId == formId &&
                cmd.IsComplete == isComplete &&
                cmd.CurrentPage == currentPage &&
                cmd.JsonData == jsonData &&
                cmd.Metadata == metadata
            ),
            Arg.Any<CancellationToken>()
        );
    }
}
