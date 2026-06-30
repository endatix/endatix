namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// Settings for integration test host mode, read from environment.
/// </summary>
public sealed record IntegrationHostSettings(IntegrationHostMode HostMode)
{
    /// <summary>
    /// Reads host settings from the <c>ENDATIX_TEST_HOST_MODE</c> environment variable.
    /// </summary>
    public static IntegrationHostSettings FromEnvironment()
    {
        var modeRaw = Environment.GetEnvironmentVariable("ENDATIX_TEST_HOST_MODE") ?? "ProductionProgram";
        var parsed = Enum.TryParse(modeRaw, ignoreCase: true, out IntegrationHostMode mode);
        if (!parsed)
        {
            throw new InvalidOperationException(
                $"Unsupported ENDATIX_TEST_HOST_MODE value '{modeRaw}'. Supported values: ProductionProgram, DedicatedIntegrationHost.");
        }

        return new IntegrationHostSettings(mode);
    }
}
