using Endatix.Modules.Jobs.Runtime;

namespace Endatix.Modules.Jobs.Tests.Runtime;

public sealed class JobsShutdownSignalTests
{
    [Fact]
    public void Dispose_NotRaised_LeavesTheSignalUnraised()
    {
        // Arrange — a host torn down without stopping is treated like a crash, whose handlers are never told.
        var signal = new JobsShutdownSignal();
        var token = signal.Token;

        // Act
        signal.Dispose();

        // Assert
        signal.IsRaised.Should().BeFalse();
        token.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public void Raise_AfterDispose_DoesNotThrow()
    {
        // Arrange — the container can dispose the signal before the host stops and raises it.
        var signal = new JobsShutdownSignal();
        signal.Dispose();

        // Act
        var raise = signal.Raise;

        // Assert
        raise.Should().NotThrow();
    }

    [Fact]
    public void Token_AfterDispose_CanStillBeLinked()
    {
        // Arrange — a handler started after disposal still links its token to the signal.
        var signal = new JobsShutdownSignal();
        signal.Dispose();

        // Act
        var link = () => CancellationTokenSource.CreateLinkedTokenSource(signal.Token, CancellationToken.None).Dispose();

        // Assert
        link.Should().NotThrow();
    }

    [Fact]
    public void Raise_BeforeDispose_CancelsTheToken()
    {
        // Arrange
        var signal = new JobsShutdownSignal();

        // Act
        signal.Raise();

        // Assert
        signal.IsRaised.Should().BeTrue();
        signal.Token.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void Dispose_Twice_DoesNotThrow()
    {
        // Arrange
        var signal = new JobsShutdownSignal();
        signal.Dispose();

        // Act
        var disposeAgain = signal.Dispose;

        // Assert
        disposeAgain.Should().NotThrow();
    }
}
