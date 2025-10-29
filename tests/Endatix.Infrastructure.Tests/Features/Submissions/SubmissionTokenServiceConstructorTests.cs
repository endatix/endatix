using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Tests;
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
        var tenantSettingsRepository = Substitute.For<IRepository<TenantSettings>>();

        // Act
        var act = () => new SubmissionTokenService(null!, tenantSettingsRepository);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithMessage(ErrorMessages.GetErrorMessage("submissionRepository", ErrorType.Null));
    }

    [Fact]
    public void Constructor_NullTenantSettingsRepository_ThrowsArgumentNullException()
    {
        // Arrange
        var submissionRepository = Substitute.For<IRepository<Submission>>();

        // Act
        var act = () => new SubmissionTokenService(submissionRepository, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithMessage(ErrorMessages.GetErrorMessage("tenantSettingsRepository", ErrorType.Null));
    }
}
