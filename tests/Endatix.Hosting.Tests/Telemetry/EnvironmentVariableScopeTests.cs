namespace Endatix.Hosting.Tests.Telemetry;

/// <summary>
/// Guards the test helper itself: a scope that fails to restore the ambient environment leaks
/// state into every later test and into the developer's shell.
/// </summary>
[Collection(TelemetryEnvironmentCollection.Name)]
public sealed class EnvironmentVariableScopeTests
{
    private const string Variable = "ENDATIX_TEST_SCOPE_PROBE";

    [Fact]
    public void Dispose_RestoresTheOriginalValue()
    {
        // Arrange
        Environment.SetEnvironmentVariable(Variable, "original");

        try
        {
            // Act
            using (new EnvironmentVariableScope((Variable, "overridden")))
            {
                Environment.GetEnvironmentVariable(Variable).Should().Be("overridden");
            }

            // Assert
            Environment.GetEnvironmentVariable(Variable).Should().Be("original");
        }
        finally
        {
            Environment.SetEnvironmentVariable(Variable, null);
        }
    }

    [Fact]
    public void Dispose_WhenTheSameNameIsSuppliedTwice_RestoresTheOriginalValue()
    {
        // Arrange — the shape ClearOtelEnvironment uses: clear everything, then override one.
        // Recording the value on each pass would capture the null written moments earlier.
        Environment.SetEnvironmentVariable(Variable, "original");

        try
        {
            // Act
            using (new EnvironmentVariableScope((Variable, null), (Variable, "overridden")))
            {
                Environment.GetEnvironmentVariable(Variable).Should().Be("overridden");
            }

            // Assert
            Environment.GetEnvironmentVariable(Variable).Should().Be("original");
        }
        finally
        {
            Environment.SetEnvironmentVariable(Variable, null);
        }
    }

    [Fact]
    public void Dispose_WhenTheVariableWasUnset_LeavesItUnset()
    {
        // Arrange
        Environment.SetEnvironmentVariable(Variable, null);

        // Act
        using (new EnvironmentVariableScope((Variable, "temporary")))
        {
            Environment.GetEnvironmentVariable(Variable).Should().Be("temporary");
        }

        // Assert
        Environment.GetEnvironmentVariable(Variable).Should().BeNull();
    }
}
