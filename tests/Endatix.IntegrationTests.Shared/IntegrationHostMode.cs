namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// Mode for hosting the integration test application.
/// </summary>
public enum IntegrationHostMode
{
    /// <summary>Use the production program entry point.</summary>
    ProductionProgram,
    /// <summary>Use the dedicated integration test host.</summary>
    DedicatedIntegrationHost
}
