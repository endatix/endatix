namespace Endatix.Api.Infrastructure.Cors;

/// <summary>
/// Enum to represent the wildcard result
/// <see cref="None"/> - No wildcard found
/// <see cref="MatchAll"/> - "*" found, which means matchAll(any)
/// <see cref="IgnoreAll"/> - "-" found, which means ignoreAll(none)
/// </summary>
public enum CorsWildcardResult
{
    /// <summary>
    /// No wildcard found
    /// </summary>
    None,

    /// <summary>
    ///  "*" found, which means matchAll(any)
    /// </summary>
    MatchAll,

    /// <summary>
    /// "-" found, which means ignoreAll(none)
    /// </summary>
    IgnoreAll
}