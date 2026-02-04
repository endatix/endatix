using MediatR;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Tests;
using Endatix.Core.UseCases.Submissions.PartialUpdate;
using Endatix.Core.UseCases.Submissions.PartialUpdateByAccessToken;

namespace Endatix.Core.Tests.UseCases.Submissions.PartialUpdateByAccessToken;

public class PartialUpdateByAccessTokenHandlerTests
{
    private readonly ISender _sender;
    private readonly IRepository<Submission> _submissionRepository;
    private readonly ISubmissionAccessTokenService _tokenService;
    private readonly PartialUpdateByAccessTokenHandler _handler;

    public PartialUpdateByAccessTokenHandlerTests()
    {
        _sender = Substitute.For<ISender>();
        _submissionRepository = Substitute.For<IRepository<Submission>>();
        _tokenService = Substitute.For<ISubmissionAccessTokenService>();
        _handler = new PartialUpdateByAccessTokenHandler(_sender, _submissionRepository, _tokenService);
    }

    private static Submission CreateSubmissionWithForm(long formId, long formDefinitionId, bool isFormEnabled = true)
    {
        var form = new Form(SampleData.TENANT_ID, "Test Form", isEnabled: isFormEnabled);
        var formDefinition = new FormDefinition(SampleData.TENANT_ID, false, "{}");
        var submission = new Submission(SampleData.TENANT_ID, "{}", formId, formDefinitionId);

        var formProperty = typeof(Submission).GetProperty(nameof(Submission.Form))!;
        formProperty.SetValue(submission, form);
        var formDefinitionProperty = typeof(Submission).GetProperty(nameof(Submission.FormDefinition))!;
        formDefinitionProperty.SetValue(submission, formDefinition);

        return submission;
    }

    [Fact]
    public async Task Handle_ValidToken_AndEnabledForm_UpdatesSubmissionSuccessfully()
    {
        // Arrange
        var formId = 123L;
        var formDefinitionId = 1L;
        var submissionId = 456L;
        var token = "valid.token.w.signature";
        var command = new PartialUpdateByAccessTokenCommand(
            token,
            formId,
            true,
            5,
            "{\"field\":\"value\"}",
            "{\"meta\":\"data\"}");

        var tokenClaims = new SubmissionAccessTokenClaims(
            submissionId,
            new[] { "edit" },
            DateTime.UtcNow.AddHours(1));

        var form = new Form(SampleData.TENANT_ID, "Test Form", isEnabled: true);
        var formDefinition = new FormDefinition(SampleData.TENANT_ID, false, "{}");
        var submission = new Submission(SampleData.TENANT_ID, "{}", formId, formDefinitionId);

        var formProperty = typeof(Submission).GetProperty(nameof(Submission.Form))!;
        formProperty.SetValue(submission, form);
        var formDefinitionProperty = typeof(Submission).GetProperty(nameof(Submission.FormDefinition))!;
        formDefinitionProperty.SetValue(submission, formDefinition);

        var updatedSubmission = new Submission(SampleData.TENANT_ID, "{\"field\":\"value\"}", formId, formDefinitionId);

        _tokenService.ValidateAccessToken(token)
            .Returns(Result.Success(tokenClaims));
        _submissionRepository.SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>())
            .Returns(submission);
        _sender.Send(Arg.Any<PartialUpdateSubmissionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(updatedSubmission));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(updatedSubmission);

        _tokenService.Received(1).ValidateAccessToken(token);
        await _submissionRepository.Received(1).SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>());
        await _sender.Received(1).Send(Arg.Any<PartialUpdateSubmissionCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidToken_ReturnsInvalidResult()
    {
        // Arrange
        var formId = 123L;
        var token = "invalid.token";
        var command = new PartialUpdateByAccessTokenCommand(token, formId, null, null, null, null);

        _tokenService.ValidateAccessToken(token)
            .Returns(Result<SubmissionAccessTokenClaims>.Invalid(new ValidationError { ErrorMessage = "Invalid token" }));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);

        _tokenService.Received(1).ValidateAccessToken(token);
        await _submissionRepository.DidNotReceive().SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>());
        await _sender.DidNotReceive().Send(Arg.Any<PartialUpdateSubmissionCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidToken_ButDisabledForm_ReturnsNotFound()
    {
        // Arrange
        var formId = 123L;
        var formDefinitionId = 1L;
        var submissionId = 456L;
        var token = "valid.token.w.signature";
        var command = new PartialUpdateByAccessTokenCommand(token, formId, null, null, null, null);

        var tokenClaims = new SubmissionAccessTokenClaims(
            submissionId,
            new[] { "edit" },
            DateTime.UtcNow.AddHours(1));

        var form = new Form(SampleData.TENANT_ID, "Test Form", isEnabled: false);
        var formDefinition = new FormDefinition(SampleData.TENANT_ID, false, "{}");
        var submission = new Submission(SampleData.TENANT_ID, "{}", formId, formDefinitionId);

        var formProperty = typeof(Submission).GetProperty(nameof(Submission.Form))!;
        formProperty.SetValue(submission, form);
        var formDefinitionProperty = typeof(Submission).GetProperty(nameof(Submission.FormDefinition))!;
        formDefinitionProperty.SetValue(submission, formDefinition);

        _tokenService.ValidateAccessToken(token)
            .Returns(Result.Success(tokenClaims));
        _submissionRepository.SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>())
            .Returns(submission);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Form not found");

        _tokenService.Received(1).ValidateAccessToken(token);
        await _submissionRepository.Received(1).SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>());
        await _sender.DidNotReceive().Send(Arg.Any<PartialUpdateSubmissionCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TokenWithoutEditPermission_ReturnsForbiddenResult()
    {
        // Arrange
        var formId = 123L;
        var submissionId = 456L;
        var token = "valid.token.r.signature";
        var command = new PartialUpdateByAccessTokenCommand(token, formId, null, null, null, null);

        var tokenClaims = new SubmissionAccessTokenClaims(
            submissionId,
            new[] { "view" },  // Missing "edit" permission
            DateTime.UtcNow.AddHours(1));

        _tokenService.ValidateAccessToken(token)
            .Returns(Result.Success(tokenClaims));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Forbidden);
        result.Errors.Should().Contain("Token does not have edit permission");

        _tokenService.Received(1).ValidateAccessToken(token);
        await _submissionRepository.DidNotReceive().SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>());
        await _sender.DidNotReceive().Send(Arg.Any<PartialUpdateSubmissionCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SubmissionNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var formId = 123L;
        var submissionId = 456L;
        var token = "valid.token.w.signature";
        var command = new PartialUpdateByAccessTokenCommand(token, formId, null, null, null, null);

        var tokenClaims = new SubmissionAccessTokenClaims(
            submissionId,
            new[] { "edit" },
            DateTime.UtcNow.AddHours(1));

        _tokenService.ValidateAccessToken(token)
            .Returns(Result.Success(tokenClaims));
        _submissionRepository.SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>())
            .Returns((Submission?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Submission not found");

        _tokenService.Received(1).ValidateAccessToken(token);
        await _submissionRepository.Received(1).SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>());
        await _sender.DidNotReceive().Send(Arg.Any<PartialUpdateSubmissionCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DelegatesCorrectlyToPartialUpdateSubmissionCommand()
    {
        // Arrange
        var formId = 123L;
        var submissionId = 456L;
        var token = "valid.token.w.signature";
        var isComplete = true;
        var currentPage = 5;
        var jsonData = "{\"field\":\"value\"}";
        var metadata = "{\"meta\":\"data\"}";
        var command = new PartialUpdateByAccessTokenCommand(token, formId, isComplete, currentPage, jsonData, metadata);

        var tokenClaims = new SubmissionAccessTokenClaims(
            submissionId,
            new[] { "edit" },
            DateTime.UtcNow.AddHours(1));
        var submission = CreateSubmissionWithForm(formId, 1L, isFormEnabled: true);
        var updatedSubmission = new Submission(SampleData.TENANT_ID, jsonData, formId, 1L);

        _tokenService.ValidateAccessToken(token)
            .Returns(Result.Success(tokenClaims));
        _submissionRepository.SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>())
            .Returns(submission);
        _sender.Send(Arg.Any<PartialUpdateSubmissionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(updatedSubmission));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _sender.Received(1).Send(
            Arg.Is<PartialUpdateSubmissionCommand>(cmd =>
                cmd.SubmissionId == submissionId &&
                cmd.FormId == formId &&
                cmd.IsComplete == isComplete &&
                cmd.CurrentPage == currentPage &&
                cmd.JsonData == jsonData &&
                cmd.Metadata == metadata),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TokenWithEditPermission_Succeeds()
    {
        // Arrange
        var formId = 123L;
        var submissionId = 456L;
        var token = "valid.token.w.signature";
        var command = new PartialUpdateByAccessTokenCommand(token, formId, null, null, null, null);

        var tokenClaims = new SubmissionAccessTokenClaims(
            submissionId,
            new[] { "edit" },
            DateTime.UtcNow.AddHours(1));
        var submission = CreateSubmissionWithForm(formId, 1L, isFormEnabled: true);
        var updatedSubmission = new Submission(SampleData.TENANT_ID, "{}", formId, 1L);

        _tokenService.ValidateAccessToken(token)
            .Returns(Result.Success(tokenClaims));
        _submissionRepository.SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>())
            .Returns(submission);
        _sender.Send(Arg.Any<PartialUpdateSubmissionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(updatedSubmission));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(updatedSubmission);
    }

    [Fact]
    public async Task Handle_TokenWithAllPermissions_Succeeds()
    {
        // Arrange
        var formId = 123L;
        var submissionId = 456L;
        var token = "valid.token.rwx.signature";
        var command = new PartialUpdateByAccessTokenCommand(token, formId, null, null, null, null);

        var tokenClaims = new SubmissionAccessTokenClaims(
            submissionId,
            new[] { "view", "edit", "export" },
            DateTime.UtcNow.AddHours(1));
        var submission = CreateSubmissionWithForm(formId, 1L, isFormEnabled: true);
        var updatedSubmission = new Submission(SampleData.TENANT_ID, "{}", formId, 1L);

        _tokenService.ValidateAccessToken(token)
            .Returns(Result.Success(tokenClaims));
        _submissionRepository.SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>())
            .Returns(submission);
        _sender.Send(Arg.Any<PartialUpdateSubmissionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(updatedSubmission));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(updatedSubmission);
    }

    [Fact]
    public async Task Handle_PartialUpdateSubmissionCommandFails_ReturnsFailureResult()
    {
        // Arrange
        var formId = 123L;
        var submissionId = 456L;
        var token = "valid.token.w.signature";
        var command = new PartialUpdateByAccessTokenCommand(token, formId, null, null, null, null);

        var tokenClaims = new SubmissionAccessTokenClaims(
            submissionId,
            new[] { "edit" },
            DateTime.UtcNow.AddHours(1));
        var submission = CreateSubmissionWithForm(formId, 1L, isFormEnabled: true);

        _tokenService.ValidateAccessToken(token)
            .Returns(Result.Success(tokenClaims));
        _submissionRepository.SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>())
            .Returns(submission);
        _sender.Send(Arg.Any<PartialUpdateSubmissionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<Submission>.Error("Update failed"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Error);

        await _sender.Received(1).Send(Arg.Any<PartialUpdateSubmissionCommand>(), Arg.Any<CancellationToken>());
    }
}
