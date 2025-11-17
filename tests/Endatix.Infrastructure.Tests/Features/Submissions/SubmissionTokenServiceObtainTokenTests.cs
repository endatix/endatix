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
    private readonly IRepository<TenantSettings> _tenantSettingsRepository;
    private readonly SubmissionTokenService _sut;
    private const long TENANT_ID = SampleData.TENANT_ID;

    public SubmissionTokenServiceObtainTokenTests()
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
        var submission = new Submission(TENANT_ID, SampleData.FORM_DEFINITION_JSON_DATA_1, 2, 3, false) { Id = submissionId };
        _submissionRepository.GetByIdAsync(submissionId).Returns(submission);

        // Act
        var result = await _sut.ObtainTokenAsync(submissionId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();
        await _submissionRepository.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task ObtainToken_NoTenantSettings_ThrowsArgumentNullException()
    {
        // Arrange
        var submissionId = 1L;
        var submission = new Submission(TENANT_ID, SampleData.FORM_DEFINITION_JSON_DATA_1, 2, 3, false) { Id = submissionId };
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
