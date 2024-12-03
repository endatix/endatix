using MediatR;
using NSubstitute;
using FluentAssertions;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Submissions.GetById;
using Endatix.Core.UseCases.Submissions.GetByToken;

namespace Endatix.Core.Tests.UseCases.Submissions.GetByToken;

public class GetByTokenHandlerTests
{
    private readonly ISender _sender;
    private readonly ISubmissionTokenService _tokenService;
    private readonly GetByTokenHandler _handler;

    public GetByTokenHandlerTests()
    {
        _sender = Substitute.For<ISender>();
        _tokenService = Substitute.For<ISubmissionTokenService>();
        _handler = new GetByTokenHandler(_sender, _tokenService);
    }

    [Fact]
    public async Task Handle_InvalidToken_ReturnsNotFoundResult()
    {
        // Arrange
        var formId = 1L;
        var token = "invalid-token";
        var request = new GetByTokenQuery(formId, token);
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
    public async Task Handle_ValidToken_ReturnsSubmission()
    {
        // Arrange
        var formId = 1L;
        var token = "valid-token";
        var submissionId = 123L;
        var request = new GetByTokenQuery(formId, token);
        var tokenResult = Result.Success(submissionId);
        var submission = new Submission();
        var submissionResult = Result.Success(submission);

        _tokenService.ResolveTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(tokenResult);
        _sender.Send(Arg.Any<GetByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(submissionResult);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(submission);

        await _sender.Received(1).Send(
            Arg.Is<GetByIdQuery>(q => 
                q.FormId == formId && 
                q.SubmissionId == submissionId),
            Arg.Any<CancellationToken>()
        );
    }
}
