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
    public async Task RefusalAsync_SnapshotWithoutAnAccount_IsUnavailable(string subject)
    {
        // Arrange
        var authorization = Substitute.For<ICurrentUserAuthorizationService>();
        var standing = new AccountStanding(authorization);
        var snapshot = new AuthorizationData { UserId = subject };

        var ct = TestContext.Current.CancellationToken;
        authorization.GetAuthorizationDataAsync(ct).Returns(Result.Success(snapshot));

        // Act
        var refusal = await standing.RefusalAsync<string>(ct);

        // Assert
        Assert.Equal(ResultStatus.Unavailable, refusal?.Status);
    }
}
