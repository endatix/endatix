using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Core.Tests;
using Endatix.Infrastructure.Features.Submissions;
using FluentAssertions;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Features.Submissions;

public class SubmissionTokenServiceObtainTokenTests
{
    private readonly IRepository<Submission> _submissionRepository;
    private readonly IRepository<Form> _formRepository;
    private readonly IRepository<TenantSettings> _tenantSettingsRepository;
    private readonly SubmissionTokenService _sut;
    private const long TENANT_ID = SampleData.TENANT_ID;
    private const long FORM_ID = 2;

    public SubmissionTokenServiceObtainTokenTests()
    {
        _submissionRepository = Substitute.For<IRepository<Submission>>();
        _formRepository = Substitute.For<IRepository<Form>>();
        _tenantSettingsRepository = Substitute.For<IRepository<TenantSettings>>();

        var tenantSettings = new TenantSettings(TENANT_ID, submissionTokenExpiryHours: 24);
        _tenantSettingsRepository.FirstOrDefaultAsync(
            Arg.Any<TenantSettingsByTenantIdSpec>(),
            Arg.Any<CancellationToken>()).Returns(tenantSettings);

        var form = new Form(TENANT_ID, "Test form") { Id = FORM_ID };
        _formRepository.FirstOrDefaultAsync(
            Arg.Any<FormProjections.SessionTokenExpiryDtoSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(new FormProjections.FormSessionTokenExpiryDto(FORM_ID, form.SubmissionTokenExpiryHours));

        _sut = new SubmissionTokenService(_submissionRepository, _formRepository, _tenantSettingsRepository);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ObtainToken_InvalidSubmissionId_ThrowsArgumentException(long submissionId)
    {
        // Act
        var act = () => _sut.ObtainTokenAsync(submissionId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage(ErrorMessages.GetErrorMessage("submissionId", ErrorType.ZeroOrNegative));
    }

    [Fact]
    public async Task ObtainToken_SubmissionNotFound_ReturnsNotFound()
    {
        // Arrange
        var submissionId = 1L;
        _submissionRepository.GetByIdAsync(submissionId).Returns((Submission)null!);

        // Act
        var result = await _sut.ObtainTokenAsync(submissionId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Submission not found");
    }

    [Fact]
    public async Task ObtainToken_NewToken_ReturnsSuccess()
    {
        // Arrange
        var submissionId = 1L;
        var submission = new Submission(TENANT_ID, SampleData.FORM_DEFINITION_JSON_DATA_1, FORM_ID, 3, false) { Id = submissionId };
        _submissionRepository.GetByIdAsync(submissionId).Returns(submission);

        // Act
        var result = await _sut.ObtainTokenAsync(submissionId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();
        submission.Token.Should().NotBeNull();
        submission.Token!.ExpiresAt.Should().NotBeNull();
        await _submissionRepository.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task ObtainToken_FormOverride_UsesFormHoursInsteadOfTenant()
    {
        // Arrange
        var submissionId = 1L;
        var submission = new Submission(TENANT_ID, SampleData.FORM_DEFINITION_JSON_DATA_1, FORM_ID, 3, false) { Id = submissionId };
        _submissionRepository.GetByIdAsync(submissionId).Returns(submission);

        var form = Form.Create(new FormCreateArgs(
            TenantId: TENANT_ID,
            Name: "Override form",
            SubmissionTokenExpiryHours: 168));
        form.Id = FORM_ID;
        _formRepository.FirstOrDefaultAsync(
            Arg.Any<FormProjections.SessionTokenExpiryDtoSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(new FormProjections.FormSessionTokenExpiryDto(FORM_ID, form.SubmissionTokenExpiryHours));

        var before = DateTime.UtcNow;

        // Act
        var result = await _sut.ObtainTokenAsync(submissionId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        submission.Token.Should().NotBeNull();
        submission.Token!.ExpiresAt.Should().NotBeNull();
        submission.Token.ExpiresAt!.Value.Should().BeOnOrAfter(before.AddHours(168).AddMinutes(-1));
        submission.Token.ExpiresAt.Value.Should().BeOnOrBefore(DateTime.UtcNow.AddHours(168).AddMinutes(1));
        await _tenantSettingsRepository.DidNotReceive().FirstOrDefaultAsync(
            Arg.Any<TenantSettingsByTenantIdSpec>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ObtainToken_NoTenantSettings_ThrowsArgumentNullException()
    {
        // Arrange
        var submissionId = 1L;
        var submission = new Submission(TENANT_ID, SampleData.FORM_DEFINITION_JSON_DATA_1, FORM_ID, 3, false) { Id = submissionId };
        _submissionRepository.GetByIdAsync(submissionId).Returns(submission);
        _tenantSettingsRepository.FirstOrDefaultAsync(
            Arg.Any<TenantSettingsByTenantIdSpec>(),
            Arg.Any<CancellationToken>()).Returns((TenantSettings)null!);

        // Act
        var act = () => _sut.ObtainTokenAsync(submissionId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithMessage("*Tenant settings must be configured.*");
    }
}
