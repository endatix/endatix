using Endatix.Hosting.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Hosting.Tests.Builders;

public class EndatixBuilderSetupLoggingTests
{
    [Fact]
    public void LogSetupInfo_OnRootBuilder_DoesNotRecurse()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var act = () => builder.LogSetupInfo("info {Value}", 1);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void LogSetupWarning_OnRootBuilder_DoesNotRecurse()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var act = () => builder.LogSetupWarning("warning {Value}", 1);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void LogSetupError_OnRootBuilder_DoesNotRecurse()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var act = () => builder.LogSetupError("error {Value}", 1);

        // Assert
        act.Should().NotThrow();
    }

    private static EndatixBuilder CreateBuilder() =>
        new(new ServiceCollection(), new ConfigurationBuilder().Build());
}
