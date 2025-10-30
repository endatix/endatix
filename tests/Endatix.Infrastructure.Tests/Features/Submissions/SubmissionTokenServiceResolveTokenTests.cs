using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Core.Tests;
using Endatix.Infrastructure.Features.Submissions;
using FluentAssertions;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Features.Submissions;

public class SubmissionTokenServiceResolveTokenTests
{
    private readonly IRepository<Submission> _submissionRepository;
    private readonly IRepository<TenantSettings> _tenantSettingsRepository;
    private readonly SubmissionTokenService _sut;
    private const long TENANT_ID = SampleData.TENANT_ID;

    public SubmissionTokenServiceResolveTokenTests()
    {
        _submissionRepository = Substitute.For<IRepository<Submission>>();
        _tenantSettingsRepository = Substitute.For<IRepository<TenantSettings>>();

        // Set up default tenant settings with 24-hour expiry
        var tenantSettings = new TenantSettings(TENANT_ID, submissionTokenExpiryHours: 24);
        _tenantSettingsRepository.FirstOrDefaultAsync(
            Arg.Any<TenantSettingsByTenantIdSpec>(),
            Arg.Any<CancellationToken>()).Returns(tenantSettings);

        _sut = new SubmissionTokenService(_submissionRepository, _tenantSettingsRepository);
    }

    [Fact]
    public async Task ResolveToken_NullOrEmptyToken_ThrowsArgumentException()
    {
        // Arrange
        var token = string.Empty;

        // Act
        var act = () => _sut.ResolveTokenAsync(token, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage(ErrorMessages.GetErrorMessage("token", ErrorType.Empty));
    }

    [Fact]
    public async Task ResolveToken_TokenNotFound_ReturnsNotFound()
    {
        // Arrange
        var token = "invalid-token";
        _submissionRepository.FirstOrDefaultAsync(Arg.Any<SubmissionByTokenSpec>()).Returns((Submission)null!);

        // Act
        var result = await _sut.ResolveTokenAsync(token, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Invalid or expired token");
    }

    [Fact]
    public async Task ResolveToken_ValidToken_ReturnsSuccess()
    {
        // Arrange
        var token = "valid-token";
        var submissionId = 1L;
        var submission = new Submission(TENANT_ID, SampleData.FORM_DEFINITION_JSON_DATA_1, 2, 3, false) { Id = submissionId };
        submission.UpdateToken(new Token(24));
        _submissionRepository.FirstOrDefaultAsync(Arg.Any<SubmissionByTokenSpec>()).Returns(submission);

        // Act
        var result = await _sut.ResolveTokenAsync(token, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(submissionId);
    }

    [Fact]
    public async Task ResolveToken_WhenSubmissionIsCompleteAndTokenNotValidAfterCompletion_ReturnsNotFound()
    {
        // Arrange
        var formId = 1L;
        var formDefinitionId = 2L;
        var token = "valid-token";
        var submission = new Submission(TENANT_ID, "{ }", formId, formDefinitionId, isComplete: true);
        submission.UpdateToken(new Token(24));
        _submissionRepository.FirstOrDefaultAsync(Arg.Any<SubmissionByTokenSpec>()).Returns(submission);

        // Configure tenant settings to NOT allow token access after completion
        var tenantSettings = new TenantSettings(TENANT_ID, submissionTokenExpiryHours: 24, isSubmissionTokenValidAfterCompletion: false);
        _tenantSettingsRepository.FirstOrDefaultAsync(
            Arg.Any<TenantSettingsByTenantIdSpec>(),
            Arg.Any<CancellationToken>()).Returns(tenantSettings);

        // Act
        var result = await _sut.ResolveTokenAsync(token, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Submission completed");
    }

    [Fact]
    public async Task ResolveToken_WhenSubmissionIsCompleteAndTokenValidAfterCompletion_ReturnsSuccess()
    {
        // Arrange
        var formId = 1L;
        var formDefinitionId = 2L;
        var submissionId = 1L;
        var token = "valid-token";
        var submission = new Submission(TENANT_ID, "{ }", formId, formDefinitionId, isComplete: true) { Id = submissionId };
        submission.UpdateToken(new Token(24));
        _submissionRepository.FirstOrDefaultAsync(Arg.Any<SubmissionByTokenSpec>()).Returns(submission);

        // Configure tenant settings to allow token access after completion
        var tenantSettings = new TenantSettings(TENANT_ID, submissionTokenExpiryHours: 24, isSubmissionTokenValidAfterCompletion: true);
        _tenantSettingsRepository.FirstOrDefaultAsync(
            Arg.Any<TenantSettingsByTenantIdSpec>(),
            Arg.Any<CancellationToken>()).Returns(tenantSettings);

        // Act
        var result = await _sut.ResolveTokenAsync(token, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(submissionId);
    }
}
