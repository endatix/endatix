using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Tests;
using Endatix.Core.UseCases.Submissions.CreateAccessToken;

namespace Endatix.Core.Tests.UseCases.Submissions.CreateAccessToken;

public class CreateAccessTokenHandlerTests
{
    private readonly IRepository<Submission> _submissionRepository;
    private readonly ISubmissionAccessTokenService _tokenService;
    private readonly ICurrentUserAuthorizationService _authorizationService;
    private readonly CreateAccessTokenHandler _handler;

    public CreateAccessTokenHandlerTests()
    {
        _submissionRepository = Substitute.For<IRepository<Submission>>();
        _tokenService = Substitute.For<ISubmissionAccessTokenService>();
        _authorizationService = Substitute.For<ICurrentUserAuthorizationService>();
        _handler = new CreateAccessTokenHandler(_submissionRepository, _tokenService, _authorizationService);
    }

    [Fact]
    public async Task Handle_ValidRequest_GeneratesTokenSuccessfully()
    {
        // Arrange
        var formId = 123L;
        var submissionId = 456L;
        var expiryMinutes = 60;
        var permissions = new[] { "view", "edit" };
        var command = new CreateAccessTokenCommand(formId, submissionId, expiryMinutes, permissions);

        var submission = new Submission(SampleData.TENANT_ID, "{}", formId, 1L);
        var tokenDto = new SubmissionAccessTokenDto(
            "token123",
            DateTime.UtcNow.AddMinutes(expiryMinutes),
            permissions);

        _authorizationService.ValidateAccessAsync("submissions.view", Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _authorizationService.ValidateAccessAsync("submissions.edit", Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _submissionRepository.SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionSpec>(), Arg.Any<CancellationToken>())
            .Returns(submission);
        _tokenService.GenerateAccessToken(submissionId, expiryMinutes, permissions)
            .Returns(Result.Success(tokenDto));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(tokenDto);
        result.Value.Token.Should().Be("token123");
        result.Value.Permissions.Should().BeEquivalentTo(permissions);

        await _authorizationService.Received(1).ValidateAccessAsync("submissions.view", Arg.Any<CancellationToken>());
        await _authorizationService.Received(1).ValidateAccessAsync("submissions.edit", Arg.Any<CancellationToken>());
        await _submissionRepository.Received(1).SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionSpec>(), Arg.Any<CancellationToken>());
        _tokenService.Received(1).GenerateAccessToken(submissionId, expiryMinutes, permissions);
    }

    [Fact]
    public async Task Handle_UnauthorizedUser_ReturnsUnauthorizedResult()
    {
        // Arrange
        var formId = 123L;
        var submissionId = 456L;
        var expiryMinutes = 60;
        var permissions = new[] { "view", "edit" };
        var command = new CreateAccessTokenCommand(formId, submissionId, expiryMinutes, permissions);

        _authorizationService.ValidateAccessAsync("submissions.view", Arg.Any<CancellationToken>())
            .Returns(Result.Forbidden("Access denied"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Forbidden);

        await _authorizationService.Received(1).ValidateAccessAsync("submissions.view", Arg.Any<CancellationToken>());
        await _authorizationService.DidNotReceive().ValidateAccessAsync("submissions.edit", Arg.Any<CancellationToken>());
        await _submissionRepository.DidNotReceive().SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionSpec>(), Arg.Any<CancellationToken>());
        _tokenService.DidNotReceive().GenerateAccessToken(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<IEnumerable<string>>());
    }

    [Fact]
    public async Task Handle_UnauthorizedForSecondPermission_ReturnsUnauthorizedResult()
    {
        // Arrange
        var formId = 123L;
        var submissionId = 456L;
        var expiryMinutes = 60;
        var permissions = new[] { "view", "edit" };
        var command = new CreateAccessTokenCommand(formId, submissionId, expiryMinutes, permissions);

        _authorizationService.ValidateAccessAsync("submissions.view", Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _authorizationService.ValidateAccessAsync("submissions.edit", Arg.Any<CancellationToken>())
            .Returns(Result.Forbidden("Access denied for edit"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Forbidden);

        await _authorizationService.Received(1).ValidateAccessAsync("submissions.view", Arg.Any<CancellationToken>());
        await _authorizationService.Received(1).ValidateAccessAsync("submissions.edit", Arg.Any<CancellationToken>());
        await _submissionRepository.DidNotReceive().SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionSpec>(), Arg.Any<CancellationToken>());
        _tokenService.DidNotReceive().GenerateAccessToken(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<IEnumerable<string>>());
    }

    [Fact]
    public async Task Handle_SubmissionNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var formId = 123L;
        var submissionId = 456L;
        var expiryMinutes = 60;
        var permissions = new[] { "view" };
        var command = new CreateAccessTokenCommand(formId, submissionId, expiryMinutes, permissions);

        _authorizationService.ValidateAccessAsync("submissions.view", Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _submissionRepository.SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionSpec>(), Arg.Any<CancellationToken>())
            .Returns((Submission?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Submission not found");

        await _authorizationService.Received(1).ValidateAccessAsync("submissions.view", Arg.Any<CancellationToken>());
        await _submissionRepository.Received(1).SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionSpec>(), Arg.Any<CancellationToken>());
        _tokenService.DidNotReceive().GenerateAccessToken(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<IEnumerable<string>>());
    }

    [Fact]
    public async Task Handle_TokenServiceFails_ReturnsTokenServiceError()
    {
        // Arrange
        var formId = 123L;
        var submissionId = 456L;
        var expiryMinutes = 60;
        var permissions = new[] { "view" };
        var command = new CreateAccessTokenCommand(formId, submissionId, expiryMinutes, permissions);

        var submission = new Submission(SampleData.TENANT_ID, "{}", formId, 1L);

        _authorizationService.ValidateAccessAsync("submissions.view", Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _submissionRepository.SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionSpec>(), Arg.Any<CancellationToken>())
            .Returns(submission);
        _tokenService.GenerateAccessToken(submissionId, expiryMinutes, permissions)
            .Returns(Result<SubmissionAccessTokenDto>.Invalid(new ValidationError { ErrorMessage = "Invalid permission" }));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);

        await _authorizationService.Received(1).ValidateAccessAsync("submissions.view", Arg.Any<CancellationToken>());
        await _submissionRepository.Received(1).SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionSpec>(), Arg.Any<CancellationToken>());
        _tokenService.Received(1).GenerateAccessToken(submissionId, expiryMinutes, permissions);
    }

    [Fact]
    public async Task Handle_SinglePermission_ChecksOnlyThatPermission()
    {
        // Arrange
        var formId = 123L;
        var submissionId = 456L;
        var expiryMinutes = 60;
        var permissions = new[] { "export" };
        var command = new CreateAccessTokenCommand(formId, submissionId, expiryMinutes, permissions);

        var submission = new Submission(SampleData.TENANT_ID, "{}", formId, 1L);
        var tokenDto = new SubmissionAccessTokenDto("token", DateTime.UtcNow.AddMinutes(expiryMinutes), permissions);

        _authorizationService.ValidateAccessAsync("submissions.export", Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _submissionRepository.SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionSpec>(), Arg.Any<CancellationToken>())
            .Returns(submission);
        _tokenService.GenerateAccessToken(submissionId, expiryMinutes, permissions)
            .Returns(Result.Success(tokenDto));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _authorizationService.Received(1).ValidateAccessAsync("submissions.export", Arg.Any<CancellationToken>());
        await _authorizationService.DidNotReceive().ValidateAccessAsync("submissions.view", Arg.Any<CancellationToken>());
        await _authorizationService.DidNotReceive().ValidateAccessAsync("submissions.edit", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AllPermissions_ChecksAllThreePermissions()
    {
        // Arrange
        var formId = 123L;
        var submissionId = 456L;
        var expiryMinutes = 60;
        var permissions = new[] { "view", "edit", "export" };
        var command = new CreateAccessTokenCommand(formId, submissionId, expiryMinutes, permissions);

        var submission = new Submission(SampleData.TENANT_ID, "{}", formId, 1L);
        var tokenDto = new SubmissionAccessTokenDto("token", DateTime.UtcNow.AddMinutes(expiryMinutes), permissions);

        _authorizationService.ValidateAccessAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _submissionRepository.SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionSpec>(), Arg.Any<CancellationToken>())
            .Returns(submission);
        _tokenService.GenerateAccessToken(submissionId, expiryMinutes, permissions)
            .Returns(Result.Success(tokenDto));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _authorizationService.Received(1).ValidateAccessAsync("submissions.view", Arg.Any<CancellationToken>());
        await _authorizationService.Received(1).ValidateAccessAsync("submissions.edit", Arg.Any<CancellationToken>());
        await _authorizationService.Received(1).ValidateAccessAsync("submissions.export", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UsesCorrectSpecificationForSubmissionLookup()
    {
        // Arrange
        var formId = 123L;
        var submissionId = 456L;
        var expiryMinutes = 60;
        var permissions = new[] { "view" };
        var command = new CreateAccessTokenCommand(formId, submissionId, expiryMinutes, permissions);

        var submission = new Submission(SampleData.TENANT_ID, "{}", formId, 1L);
        var tokenDto = new SubmissionAccessTokenDto("token", DateTime.UtcNow.AddMinutes(expiryMinutes), permissions);

        _authorizationService.ValidateAccessAsync("submissions.view", Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _submissionRepository.SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionSpec>(), Arg.Any<CancellationToken>())
            .Returns(submission);
        _tokenService.GenerateAccessToken(submissionId, expiryMinutes, permissions)
            .Returns(Result.Success(tokenDto));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _submissionRepository.Received(1).SingleOrDefaultAsync(
            Arg.Is<SubmissionWithDefinitionSpec>(spec =>
                spec != null),
            Arg.Any<CancellationToken>());
    }
}
