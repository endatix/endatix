namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// A test persona for the integration tests.
/// </summary>
public sealed record TestPersona
{
    /// <summary>
    /// The anonymous persona.
    /// </summary>
    public static readonly TestPersona Anonymous = new(nameof(Anonymous));
    /// <summary>
    /// The tenant admin persona.
    /// </summary>
    public static readonly TestPersona TenantAdmin = new(nameof(TenantAdmin));
    /// <summary>
    /// The creator persona.
    /// </summary>
    public static readonly TestPersona Creator = new(nameof(Creator));
    /// <summary>
    /// The platform admin persona.
    /// </summary>
    public static readonly TestPersona PlatformAdmin = new(nameof(PlatformAdmin));

    /// <summary>
    /// The kind of persona.
    /// </summary>
    public string Kind { get; }
    /// <summary>
    /// The custom role name for the persona.
    /// </summary>
    public string? CustomRoleName { get; }

    private TestPersona(string kind, string? customRoleName = null)
    {
        Kind = kind;
        CustomRoleName = customRoleName;
    }

    /// <summary>
    /// Creates a custom role persona.
    /// </summary>
    /// <param name="roleName">The name of the custom role.</param>
    /// <returns>A new <see cref="TestPersona"/> instance.</returns>
    public static TestPersona CustomRole(string roleName) => new(nameof(CustomRole), roleName);

    /// <summary>
    /// Whether the persona is anonymous.
    /// </summary>
    public bool IsAnonymous => Kind == nameof(Anonymous);
}
