namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// Authentication mode for creating authenticated test clients.
/// </summary>
public enum IntegrationAuthMode
{
    /// <summary>Authenticate via the real /api/auth/login endpoint.</summary>
    Login,
    /// <summary>Generate a synthetic JWT without hitting the login endpoint.</summary>
    SyntheticJwt
}
