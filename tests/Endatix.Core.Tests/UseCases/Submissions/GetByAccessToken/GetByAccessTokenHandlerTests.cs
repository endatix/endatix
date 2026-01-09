using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Tests;
using Endatix.Core.UseCases.Submissions.GetByAccessToken;

namespace Endatix.Core.Tests.UseCases.Submissions.GetByAccessToken;

public class GetByAccessTokenHandlerTests
{
    private readonly IRepository<Submission> _submissionRepository;
    private readonly ISubmissionAccessTokenService _tokenService;
    private readonly GetByAccessTokenHandler _handler;

    public GetByAccessTokenHandlerTests()
    {
        _submissionRepository = Substitute.For<IRepository<Submission>>();
        _tokenService = Substitute.For<ISubmissionAccessTokenService>();
        _handler = new GetByAccessTokenHandler(_submissionRepository, _tokenService);
    }

    [Fact]
    public async Task Handle_ValidToken_ReturnsSubmission()
    {
        // Arrange
        var formId = 123L;
        var submissionId = 456L;
        var token = "valid.token.rw.signature";
        var query = new GetByAccessTokenQuery(formId, token);

        var tokenClaims = new SubmissionAccessTokenClaims(
            submissionId,
            new[] { "view", "edit" },
            DateTime.UtcNow.AddHours(1));
        var submission = new Submission(SampleData.TENANT_ID, "{}", formId, 1L);

        _tokenService.ValidateAccessToken(token)
            .Returns(Result.Success(tokenClaims));
        _submissionRepository.SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionSpec>(), Arg.Any<CancellationToken>())
            .Returns(submission);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(submission);

        _tokenService.Received(1).ValidateAccessToken(token);
        await _submissionRepository.Received(1).SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionSpec>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidToken_ReturnsInvalidResult()
    {
        // Arrange
        var formId = 123L;
        var token = "invalid.token";
        var query = new GetByAccessTokenQuery(formId, token);

        _tokenService.ValidateAccessToken(token)
            .Returns(Result<SubmissionAccessTokenClaims>.Invalid(new ValidationError { ErrorMessage = "Invalid token" }));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);

        _tokenService.Received(1).ValidateAccessToken(token);
        await _submissionRepository.DidNotReceive().SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionSpec>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TokenWithoutViewPermission_ReturnsForbiddenResult()
    {
        // Arrange
        var formId = 123L;
        var submissionId = 456L;
        var token = "valid.token.w.signature";
        var query = new GetByAccessTokenQuery(formId, token);

        var tokenClaims = new SubmissionAccessTokenClaims(
            submissionId,
            new[] { "edit" },  // Missing "view" permission
            DateTime.UtcNow.AddHours(1));

        _tokenService.ValidateAccessToken(token)
            .Returns(Result.Success(tokenClaims));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Forbidden);
        result.Errors.Should().Contain("Token does not have view permission");

        _tokenService.Received(1).ValidateAccessToken(token);
        await _submissionRepository.DidNotReceive().SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionSpec>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SubmissionNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var formId = 123L;
        var submissionId = 456L;
        var token = "valid.token.r.signature";
        var query = new GetByAccessTokenQuery(formId, token);

        var tokenClaims = new SubmissionAccessTokenClaims(
            submissionId,
            new[] { "view" },
            DateTime.UtcNow.AddHours(1));

        _tokenService.ValidateAccessToken(token)
            .Returns(Result.Success(tokenClaims));
        _submissionRepository.SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionSpec>(), Arg.Any<CancellationToken>())
            .Returns((Submission?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Submission not found");

        _tokenService.Received(1).ValidateAccessToken(token);
        await _submissionRepository.Received(1).SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionSpec>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TokenWithViewPermission_Succeeds()
    {
        // Arrange
        var formId = 123L;
        var submissionId = 456L;
        var token = "valid.token.r.signature";
        var query = new GetByAccessTokenQuery(formId, token);

        var tokenClaims = new SubmissionAccessTokenClaims(
            submissionId,
            new[] { "view" },
            DateTime.UtcNow.AddHours(1));
        var submission = new Submission(SampleData.TENANT_ID, "{}", formId, 1L);

        _tokenService.ValidateAccessToken(token)
            .Returns(Result.Success(tokenClaims));
        _submissionRepository.SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionSpec>(), Arg.Any<CancellationToken>())
            .Returns(submission);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(submission);
    }

    [Fact]
    public async Task Handle_TokenWithAllPermissions_Succeeds()
    {
        // Arrange
        var formId = 123L;
        var submissionId = 456L;
        var token = "valid.token.rwx.signature";
        var query = new GetByAccessTokenQuery(formId, token);

        var tokenClaims = new SubmissionAccessTokenClaims(
            submissionId,
            new[] { "view", "edit", "export" },
            DateTime.UtcNow.AddHours(1));
        var submission = new Submission(SampleData.TENANT_ID, "{}", formId, 1L);

        _tokenService.ValidateAccessToken(token)
            .Returns(Result.Success(tokenClaims));
        _submissionRepository.SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionSpec>(), Arg.Any<CancellationToken>())
            .Returns(submission);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(submission);
    }

    [Fact]
    public async Task Handle_UsesCorrectSpecificationForSubmissionLookup()
    {
        // Arrange
        var formId = 123L;
        var submissionId = 456L;
        var token = "valid.token.r.signature";
        var query = new GetByAccessTokenQuery(formId, token);

        var tokenClaims = new SubmissionAccessTokenClaims(
            submissionId,
            new[] { "view" },
            DateTime.UtcNow.AddHours(1));
        var submission = new Submission(SampleData.TENANT_ID, "{}", formId, 1L);

        _tokenService.ValidateAccessToken(token)
            .Returns(Result.Success(tokenClaims));
        _submissionRepository.SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionSpec>(), Arg.Any<CancellationToken>())
            .Returns(submission);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _submissionRepository.Received(1).SingleOrDefaultAsync(
            Arg.Is<SubmissionWithDefinitionSpec>(spec => spec != null),
            Arg.Any<CancellationToken>());
    }
}
