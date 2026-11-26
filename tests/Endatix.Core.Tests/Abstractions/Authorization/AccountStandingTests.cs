using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using NSubstitute;

namespace Endatix.Core.Tests.Abstractions.Authorization;

public sealed class AccountStandingTests
{
    [Fact]
    public async Task RefusalAsync_AdmissionExpiresWithinRequest_ChecksCurrentStanding()
    {
        // Arrange
        var clock = Substitute.For<IDateTimeProvider>();
        var deadline = DateTimeOffset.UnixEpoch.AddSeconds(5);
        clock.Now.Returns(deadline.AddTicks(-1));
        var authorization = Substitute.For<ICurrentUserAuthorizationService>();
        var clear = new AuthorizationData { UserId = "owner", ExpiresAt = deadline.UtcDateTime };
        var ct = TestContext.Current.CancellationToken;
        authorization.GetAuthorizationDataAsync(ct).Returns(Result.Success(clear), Result.Success(clear with { RequiredActionsUnavailable = true }));
        var standing = new AccountStanding(authorization, clock);
        Assert.Null(await standing.RefusalAsync(ct));
        Assert.Null(await standing.RefusalAsync(ct));

        // Act
        clock.Now.Returns(deadline);
        var refusal = await standing.RefusalAsync(ct);

        // Assert
        Assert.Equal(ResultStatus.Unavailable, refusal?.Status);
    }

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
