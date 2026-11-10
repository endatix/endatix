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

    [Fact]
    public void LogSetup_OnChildBuilder_ReturnsSameChildBuilder()
    {
        // Arrange
        var api = CreateBuilder().Api;

        // Act
        var results = new[]
        {
            api.LogSetupInfo("info {Value}", 1),
            api.LogSetupWarning("warning {Value}", 1),
            api.LogSetupError("error {Value}", 1)
        };

        // Assert
        results.Should().OnlyContain(result => ReferenceEquals(result, api));
    }

    private static EndatixBuilder CreateBuilder() =>
        new(new ServiceCollection(), new ConfigurationBuilder().Build());
}
