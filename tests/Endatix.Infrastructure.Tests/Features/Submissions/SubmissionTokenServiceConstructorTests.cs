using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Infrastructure.Features.Submissions;
using FluentAssertions;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Features.Submissions;

public class SubmissionTokenServiceConstructorTests
{
    [Fact]
    public void Constructor_NullSubmissionRepository_ThrowsArgumentNullException()
    {
        // Arrange
        var formRepository = Substitute.For<IRepository<Form>>();
        var tenantSettingsRepository = Substitute.For<IRepository<TenantSettings>>();

        // Act
        var act = () => new SubmissionTokenService(null!, formRepository, tenantSettingsRepository);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullFormRepository_ThrowsArgumentNullException()
    {
        // Arrange
        var submissionRepository = Substitute.For<IRepository<Submission>>();
        var tenantSettingsRepository = Substitute.For<IRepository<TenantSettings>>();

        // Act
        var act = () => new SubmissionTokenService(submissionRepository, null!, tenantSettingsRepository);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullTenantSettingsRepository_ThrowsArgumentNullException()
    {
        // Arrange
        var submissionRepository = Substitute.For<IRepository<Submission>>();
        var formRepository = Substitute.For<IRepository<Form>>();

        // Act
        var act = () => new SubmissionTokenService(submissionRepository, formRepository, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
