namespace Endatix.IntegrationTests.Shared;

public sealed record IntegrationHostSettings(IntegrationHostMode HostMode)
{
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
