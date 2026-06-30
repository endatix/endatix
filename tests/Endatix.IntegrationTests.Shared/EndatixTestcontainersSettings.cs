namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// Settings for shared testcontainers, including container reuse.
/// </summary>
public sealed record EndatixTestcontainersSettings(bool ReuseContainers)
{
    /// <summary>
    /// Reads container settings from the <c>ENDATIX_TEST_REUSE_CONTAINERS</c> environment variable.
    /// </summary>
    public static EndatixTestcontainersSettings FromEnvironment()
    {
        var reuseRaw = Environment.GetEnvironmentVariable("ENDATIX_TEST_REUSE_CONTAINERS");
        var reuse = string.Equals(reuseRaw, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(reuseRaw, "1", StringComparison.OrdinalIgnoreCase);

        return new EndatixTestcontainersSettings(reuse);
    }
}
