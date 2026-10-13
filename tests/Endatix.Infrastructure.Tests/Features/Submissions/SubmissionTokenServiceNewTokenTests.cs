using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Core.Tests;
using Endatix.Infrastructure.Features.Submissions;
using FluentAssertions;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Features.Submissions;

public class SubmissionTokenServiceNewTokenTests
{
    private readonly IRepository<Submission> _submissionRepository = Substitute.For<IRepository<Submission>>();
    private readonly IRepository<TenantSettings> _tenantSettingsRepository = Substitute.For<IRepository<TenantSettings>>();
    private readonly SubmissionTokenService _sut;
    private const long TENANT_ID = SampleData.TENANT_ID;

    public SubmissionTokenServiceNewTokenTests()
    {
        _tenantSettingsRepository.FirstOrDefaultAsync(
            Arg.Any<TenantSettingsByTenantIdSpec>(),
            Arg.Any<CancellationToken>()).Returns(new TenantSettings(TENANT_ID, submissionTokenExpiryHours: 24));
        _sut = new SubmissionTokenService(
            _submissionRepository,
            Substitute.For<IRepository<Form>>(),
            _tenantSettingsRepository);
    }

    [Fact]
    public async Task NewToken_FormOverride_UsesFormHoursAndSavesNothing()
    {
        // Arrange
        var form = Form.Create(new FormCreateArgs(TenantId: TENANT_ID, Name: "Override form", SubmissionTokenExpiryHours: 168));
        var before = DateTime.UtcNow;

        // Act
        var token = await _sut.NewTokenAsync(form, CancellationToken.None);

        // Assert
        token.Value.Should().NotBeNullOrEmpty();
        token.ExpiresAt!.Value.Should().BeCloseTo(before.AddHours(168), TimeSpan.FromMinutes(1));
        await _submissionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewToken_NoFormOverride_UsesTenantHours()
    {
        // Arrange
        var form = Form.Create(new FormCreateArgs(TenantId: TENANT_ID, Name: "Test form"));
        var before = DateTime.UtcNow;

        // Act
        var token = await _sut.NewTokenAsync(form, CancellationToken.None);

        // Assert
        token.ExpiresAt!.Value.Should().BeCloseTo(before.AddHours(24), TimeSpan.FromMinutes(1));
    }
}
