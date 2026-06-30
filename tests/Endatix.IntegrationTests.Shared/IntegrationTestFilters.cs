namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// xUnit filter expressions shared between local runs and CI. Keep workflows in sync with these constants.
/// </summary>
public static class IntegrationTestFilters
{
    /// <summary>Default integration breadth: all tiers except Keycloak (P2).</summary>
    public const string Default = "Category!=Keycloak";

    /// <summary>PR / feature-branch fast gate: P0 + P1 only (excludes Keycloak and explicit P2).</summary>
    public const string PrFast = "Category!=Keycloak&Priority!=P2";
}
