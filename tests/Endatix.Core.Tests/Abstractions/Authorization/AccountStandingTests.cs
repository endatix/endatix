using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using NSubstitute;

namespace Endatix.Core.Tests.Abstractions.Authorization;

public sealed class AccountStandingTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(AuthorizationData.ANONYMOUS_USER_ID)]
    public void Refusal_SnapshotWithoutAnAccount_IsUnavailable(string subject)
    {
        // Arrange
        var standing = new AccountStanding(Substitute.For<ICurrentUserAuthorizationService>());
        var snapshot = new AuthorizationData { UserId = subject };

        // Act
        var refusal = standing.Refusal<string>(snapshot);

        // Assert
        Assert.Equal(ResultStatus.Unavailable, refusal?.Status);
    }
}
