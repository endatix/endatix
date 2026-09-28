using Endatix.Modules.Jobs.Runtime;

namespace Endatix.Modules.Jobs.Tests.Runtime;

/// <summary>
/// Proves the job state repository can be substituted at all.
/// </summary>
/// <remarks>
/// The interface is internal, and NSubstitute emits its proxies into a separate dynamic assembly, so
/// without the module granting that assembly access to its internals every test of the job wrapper
/// would fail at the line that creates the substitute — at run time, with no compile error to
/// point at the cause. This test fails there first, and says why.
/// </remarks>
public class BackgroundJobStateRepositorySubstitutionTests
{
    [Fact]
    public async Task SubstituteFor_InternalStateRepository_ReturnsConfiguredValue()
    {
        // Arrange
        var finish = new JobFinish(new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc), TimeSpan.FromDays(7));
        var cancellationToken = TestContext.Current.CancellationToken;
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository.TryCompleteAsync(new AttemptRef(42, 1), finish, cancellationToken).Returns(false);

        // Act
        var owned = await repository.TryCompleteAsync(new AttemptRef(42, 1), finish, cancellationToken);

        // Assert
        owned.Should().BeFalse();
        await repository.Received(1).TryCompleteAsync(new AttemptRef(42, 1), finish, cancellationToken);
    }
}
