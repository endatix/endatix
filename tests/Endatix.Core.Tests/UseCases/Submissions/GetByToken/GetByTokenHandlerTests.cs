using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Submissions.GetByToken;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Specifications;

namespace Endatix.Core.Tests.UseCases.Submissions.GetByToken;

public class GetByTokenHandlerTests
{
    private readonly IRepository<Submission> _submissionRepository;
    private readonly ISubmissionTokenService _tokenService;
    private readonly GetByTokenHandler _handler;

    public GetByTokenHandlerTests()
    {
        _submissionRepository = Substitute.For<IRepository<Submission>>();
        _tokenService = Substitute.For<ISubmissionTokenService>();
        _handler = new GetByTokenHandler(_submissionRepository, _tokenService);
    }

    [Fact]
    public async Task Handle_InvalidToken_ReturnsInvalidResult()
    {
        // Arrange
        var formId = 1L;
        var token = "invalid-token";
        var request = new GetByTokenQuery(formId, token);
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
    public async Task Handle_ValidToken_AndEnabledForm_ReturnsSubmission()
    {
        // Arrange
        var formId = 1L;
        var formDefinitionId = 2L;
        var token = "valid-token";
        var submissionId = 123L;
        var request = new GetByTokenQuery(formId, token);
        var tokenResult = Result.Success(submissionId);

        var form = new Form(SampleData.TENANT_ID, "Test Form", isEnabled: true);
        var formDefinition = new FormDefinition(SampleData.TENANT_ID, false, "{}");
        var submission = new Submission(SampleData.TENANT_ID, "{}", formId, formDefinitionId);

        var formProperty = typeof(Submission).GetProperty(nameof(Submission.Form))!;
        formProperty.SetValue(submission, form);
        var formDefinitionProperty = typeof(Submission).GetProperty(nameof(Submission.FormDefinition))!;
        formDefinitionProperty.SetValue(submission, formDefinition);

        _tokenService.ResolveTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(tokenResult);
        _submissionRepository.SingleOrDefaultAsync(
            Arg.Any<SubmissionWithDefinitionAndFormSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(submission);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(submission);

        await _submissionRepository.Received(1).SingleOrDefaultAsync(
            Arg.Any<SubmissionWithDefinitionAndFormSpec>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Handle_ValidToken_ButDisabledForm_ReturnsNotFound()
    {
        // Arrange
        var formId = 1L;
        var formDefinitionId = 2L;
        var token = "valid-token";
        var submissionId = 123L;
        var request = new GetByTokenQuery(formId, token);
        var tokenResult = Result.Success(submissionId);

        var form = new Form(SampleData.TENANT_ID, "Test Form", isEnabled: false);
        var formDefinition = new FormDefinition(SampleData.TENANT_ID, false, "{}");
        var submission = new Submission(SampleData.TENANT_ID, "{}", formId, formDefinitionId);

        var formProperty = typeof(Submission).GetProperty(nameof(Submission.Form))!;
        formProperty.SetValue(submission, form);
        var formDefinitionProperty = typeof(Submission).GetProperty(nameof(Submission.FormDefinition))!;
        formDefinitionProperty.SetValue(submission, formDefinition);

        _tokenService.ResolveTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(tokenResult);
        _submissionRepository.SingleOrDefaultAsync(
            Arg.Any<SubmissionWithDefinitionAndFormSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(submission);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Handle_ValidToken_ButSubmissionNotFound_ReturnsNotFound()
    {
        // Arrange
        var formId = 1L;
        var token = "valid-token";
        var submissionId = 123L;
        var request = new GetByTokenQuery(formId, token);
        var tokenResult = Result.Success(submissionId);

        _tokenService.ResolveTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(tokenResult);
        _submissionRepository.SingleOrDefaultAsync(
            Arg.Any<SubmissionWithDefinitionAndFormSpec>(),
            Arg.Any<CancellationToken>())
            .Returns((Submission?)null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }
}
