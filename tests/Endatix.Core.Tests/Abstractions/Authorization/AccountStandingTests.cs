using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using NSubstitute;

namespace Endatix.Core.Tests.Abstractions.Authorization;

public sealed class AccountStandingTests
{
    [Fact]
    public async Task RefusalAsync_DormantRegistration_AdmitsWithoutASnapshot()
    {
        var standing = new AccountStanding();
        Assert.Null(await standing.RefusalAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RefusalAsync_FailedSnapshot_ReturnsRetryableFailure()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var service = Substitute.For<ICurrentUserAuthorizationService>();
        service.GetAuthorizationDataAsync(ct).Returns(Result<AuthorizationData>.Error("Unavailable"));

        // Act
        var result = await new AccountStanding(service).RefusalAsync(ct);

        // Assert
        Assert.Equal(ResultStatus.Unavailable, result?.Status);
        Assert.Contains("Try again", Assert.Single(result!.Errors));
    }

    [Theory]
    [InlineData(RequiredActionSeverity.Notice)]
    [InlineData(RequiredActionSeverity.Blocking)]
    public async Task RefusalAsync_ReportedAction_BlocksOnlyForBlockingSeverity(RequiredActionSeverity severity)
    {
        // Arrange
        var service = Substitute.For<ICurrentUserAuthorizationService>();
        var ct = TestContext.Current.CancellationToken;
        RequiredAction action = new() { Key = "setup", ResolvePath = "/setup", Severity = severity, Scope = RequiredActionScope.User };
        service.GetAuthorizationDataAsync(ct).Returns(Result.Success(new AuthorizationData { UserId = "owner", RequiredActions = [action] }));

        // Act
        var refusal = await new AccountStanding(service).RefusalAsync(ct);

        // Assert
        if (severity == RequiredActionSeverity.Notice)
        {
            Assert.Null(refusal);
            return;
        }
        var mapped = refusal!.ToErrorResult<string>();
        Assert.Equal(ResultStatus.Forbidden, mapped.Status);
        Assert.Equal(action, Assert.Single(Assert.IsType<IRequiredActionRefusal>(mapped, exactMatch: false).Actions));
    }

    [Fact]
    public async Task RefusalAsync_LookupCompletesAtSnapshotExpiry_DoesNotAdmit()
    {
        // Arrange
        var clock = Substitute.For<IDateTimeProvider>();
        var deadline = DateTimeOffset.UnixEpoch.AddSeconds(1);
        clock.Now.Returns(deadline.AddTicks(-1));
        var service = Substitute.For<ICurrentUserAuthorizationService>();
        var ct = TestContext.Current.CancellationToken;
        service.GetAuthorizationDataAsync(ct).Returns(_ =>
        {
            clock.Now.Returns(deadline);
            return Result.Success(new AuthorizationData { UserId = "owner", ExpiresAt = deadline.UtcDateTime });
        });

        // Act
        var refusal = await new AccountStanding(service, clock).RefusalAsync(ct);

        // Assert
        Assert.Equal(ResultStatus.Unavailable, refusal?.Status);
    }

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
