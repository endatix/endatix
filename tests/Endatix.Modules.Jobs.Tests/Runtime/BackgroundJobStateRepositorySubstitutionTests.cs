using Endatix.Modules.Jobs.Runtime;

namespace Endatix.Modules.Jobs.Tests.Runtime;

/// <summary>
/// Proves the job state repository can be substituted at all.
/// </summary>
/// <remarks>
/// The interface is internal, and NSubstitute emits its proxies into a separate dynamic assembly, so
/// without the module granting that assembly access to its internals every test of the runner and the
/// sweeper would fail at the line that creates the substitute — at run time, with no compile error to
/// point at the cause. This test fails there first, and says why.
/// </remarks>
public class BackgroundJobStateRepositorySubstitutionTests
{
    [Fact]
    public async Task SubstituteFor_InternalStateRepository_ReturnsConfiguredValue()
    {
        // Arrange
        var utcNow = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository.TryHeartbeatAsync(42, 1, utcNow).Returns(false);

        // Act
        var owned = await repository.TryHeartbeatAsync(42, 1, utcNow);

        // Assert
        owned.Should().BeFalse();
        await repository.Received(1).TryHeartbeatAsync(42, 1, utcNow);
    }
}
