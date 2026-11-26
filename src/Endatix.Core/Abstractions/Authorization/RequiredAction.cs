using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Endatix.Core.Abstractions.Authorization;

/// <summary>A pending action reported by a module, projected for the current user.</summary>
[Experimental("ENDATIX0006")]
public sealed record RequiredAction
{
    /// <summary>A stable module-qualified identifier, for example <c>profile.complete</c>.</summary>
    public required string Key { get; init; }
    /// <summary>The application-relative path where a resolver can complete the action.</summary>
    public required string ResolvePath { get; init; }
    /// <summary>Whether resolution affects one user or the tenant.</summary>
    public required RequiredActionScope Scope { get; init; }
    /// <summary>The provider's severity, adjusted for the user and the current time in authorization data.</summary>
    public required RequiredActionSeverity Severity { get; init; }
    /// <summary>Any listed role may resolve the action. An empty list allows any affected user.</summary>
    public IReadOnlyList<string> ResolverRoles { get; init; } = [];
    /// <summary>If false, a blocking action is only a notice for users unable to resolve it.</summary>
    public bool BlockNonResolvers { get; init; }
    /// <summary>Prevent resolution in this session, even if the subject has a resolver role.</summary>
    [JsonIgnore]
    public bool PreventResolution { get; init; }
    /// <summary>The instant when a notice becomes blocking; null leaves its severity unchanged.</summary>
    public DateTimeOffset? BlockingFrom { get; init; }
    /// <summary>Calculated from the signed-in user's roles when the cache entry is built.</summary>
    public bool CanResolve { get; init; }
}

/// <summary>Whose outstanding obligation is resolved by completing the action.</summary>
[Experimental("ENDATIX0006")]
[JsonConverter(typeof(JsonStringEnumConverter<RequiredActionScope>))]
public enum RequiredActionScope
{
    /// <summary>The action belongs to the individual user.</summary>
    [JsonStringEnumMemberName("user")] User,
    /// <summary>The action belongs to the tenant.</summary>
    [JsonStringEnumMemberName("tenant")] Tenant
}

/// <summary>How the pending action affects continued access.</summary>
[Experimental("ENDATIX0006")]
[JsonConverter(typeof(JsonStringEnumConverter<RequiredActionSeverity>))]
public enum RequiredActionSeverity
{
    /// <summary>The user can continue with this action outstanding.</summary>
    [JsonStringEnumMemberName("notice")] Notice,
    /// <summary>The action must be resolved before continuing.</summary>
    [JsonStringEnumMemberName("blocking")] Blocking
}
