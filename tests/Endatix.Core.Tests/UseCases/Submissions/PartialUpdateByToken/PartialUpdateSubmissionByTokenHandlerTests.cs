using MediatR;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Submissions.PartialUpdate;
using Endatix.Core.UseCases.Submissions.PartialUpdateByToken;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Features.ReCaptcha;
using Endatix.Core.Abstractions.Repositories;

namespace Endatix.Core.Tests.UseCases.Submissions.PartialUpdateByToken;

public class PartialUpdateSubmissionByTokenHandlerTests
{
    private readonly ISender _sender;
    private readonly ISubmissionTokenService _tokenService;
    private readonly IReCaptchaPolicyService _recaptchaService;
    private readonly IFormsRepository _formsRepository;
    private readonly PartialUpdateSubmissionByTokenHandler _handler;

    public PartialUpdateSubmissionByTokenHandlerTests()
    {
        _sender = Substitute.For<ISender>();
        _tokenService = Substitute.For<ISubmissionTokenService>();
        _recaptchaService = Substitute.For<IReCaptchaPolicyService>();
        _formsRepository = Substitute.For<IFormsRepository>();
        _handler = new PartialUpdateSubmissionByTokenHandler(_sender, _tokenService, _recaptchaService, _formsRepository);
    }

    [Fact]
    public async Task Handle_InvalidToken_ReturnsInvalidResult()
    {
        // Arrange
        var token = "invalid-token";
        var formId = 1L;
        var request = new PartialUpdateSubmissionByTokenCommand(
            token: token,
            formId: formId,
            isComplete: null,
            currentPage: null,
            jsonData: null,
            metadata: null,
            reCaptchaToken: null
        );
        var tokenResult = Result.Invalid(SubmissonTokenErrors.ValidationErrors.SubmissionTokenInvalid);

        _tokenService.ResolveTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(tokenResult);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().HaveCount(1);
        result.ValidationErrors.First().Should().BeEquivalentTo(SubmissonTokenErrors.ValidationErrors.SubmissionTokenInvalid);
    }

    [Fact]
    public async Task Handle_ValidToken_UpdatesSubmissionSuccessfully()
    {
        // Arrange
        var token = "valid-token";
        var formId = 1L;
        var formDefinitionId = 2L;
        var submissionId = 123L;
        var isComplete = true;
        var currentPage = 2;
        var jsonData = SampleData.SUBMISSION_JSON_DATA_1;
        var metadata = "test metadata";
        string? reCaptchaToken = null;
        _recaptchaService.ValidateReCaptchaAsync(
            Arg.Any<SubmissionVerificationContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var request = new PartialUpdateSubmissionByTokenCommand(
            token: token,
            formId: formId,
            isComplete: isComplete,
            currentPage: currentPage,
            jsonData: jsonData,
            metadata: metadata,
            reCaptchaToken: reCaptchaToken
        );

        var tokenResult = Result.Success(submissionId);
        var submission = new Submission(SampleData.TENANT_ID, jsonData, formId, formDefinitionId) { Id = submissionId };
        var updateResult = Result.Success(submission);

        _tokenService.ResolveTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(tokenResult);

        _formsRepository.GetByIdAsync(formId, Arg.Any<CancellationToken>())
            .Returns(new Form(SampleData.TENANT_ID, "Test Form", isEnabled: true) { Id = formId });

        _recaptchaService.ValidateReCaptchaAsync(
            Arg.Any<SubmissionVerificationContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success());

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

    [Fact]
    public async Task Handle_FormNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var token = "valid-token";
        var formId = 1L;
        var submissionId = 123L;
        var request = new PartialUpdateSubmissionByTokenCommand(
            token: token,
            formId: formId,
            isComplete: null,
            currentPage: null,
            jsonData: null,
            metadata: null,
            reCaptchaToken: null
        );

        _tokenService.ResolveTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(Result.Success(submissionId));

        _formsRepository.GetByIdAsync(formId, Arg.Any<CancellationToken>())
            .Returns(null as Form);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        await _recaptchaService.DidNotReceive().ValidateReCaptchaAsync(Arg.Any<SubmissionVerificationContext>(), Arg.Any<CancellationToken>());
        await _sender.DidNotReceive().Send(Arg.Any<PartialUpdateSubmissionCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SuccessfullReCaptchaValidation_CompletesSubmission()
    {
        // Arrange
        var token = "valid-token";
        var formId = 1L;
        var formDefinitionId = 2L;
        var submissionId = 123L;
        var isComplete = false;
        var currentPage = 2;
        var jsonData = SampleData.SUBMISSION_JSON_DATA_1;
        var metadata = "test metadata";
        var reCaptchaToken = "valid-token";

        var request = new PartialUpdateSubmissionByTokenCommand(
            token: token,
            formId: formId,
            isComplete: isComplete,
            currentPage: currentPage,
            jsonData: jsonData,
            metadata: metadata,
            reCaptchaToken: reCaptchaToken
        );

        _tokenService.ResolveTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(Result.Success(submissionId));

        _formsRepository.GetByIdAsync(formId, Arg.Any<CancellationToken>())
            .Returns(new Form(SampleData.TENANT_ID, "Test Form", isEnabled: true) { Id = formId });

        _recaptchaService.ValidateReCaptchaAsync(
            Arg.Any<SubmissionVerificationContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        _sender.Send(Arg.Any<PartialUpdateSubmissionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new Submission(SampleData.TENANT_ID, jsonData, formId, formDefinitionId) { Id = submissionId }));

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert  
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
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

    [Fact]
    public async Task Handle_ReCaptchaValidationFailed_ReturnsInvalidResult()
    {
        // Arrange
        var token = "valid-token";
        var reCaptchaToken = "invalid-token";
        var formId = 1L;
        var submissionId = 123L;
        var isComplete = true;
        var currentPage = 2;
        var jsonData = SampleData.SUBMISSION_JSON_DATA_1;
        var request = new PartialUpdateSubmissionByTokenCommand(
            token: token,
            formId: formId,
            isComplete: isComplete,
            currentPage: currentPage,
            jsonData: jsonData,
            metadata: null,
            reCaptchaToken: reCaptchaToken
        );

        _tokenService.ResolveTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(Result.Success(submissionId));

        _formsRepository.GetByIdAsync(formId, Arg.Any<CancellationToken>())
            .Returns(new Form(SampleData.TENANT_ID, "Test Form", isEnabled: true) { Id = formId });

        _recaptchaService.ValidateReCaptchaAsync(
            Arg.Any<SubmissionVerificationContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Invalid(ReCaptchaErrors.ValidationErrors.ReCaptchaVerificationFailed));

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
        await _sender.DidNotReceive().Send(Arg.Any<PartialUpdateSubmissionCommand>(), Arg.Any<CancellationToken>());
        result.ValidationErrors.Should().HaveCount(1);
        result.ValidationErrors.First().Should().BeEquivalentTo(ReCaptchaErrors.ValidationErrors.ReCaptchaVerificationFailed);
    }
}
