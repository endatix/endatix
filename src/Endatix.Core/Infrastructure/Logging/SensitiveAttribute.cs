namespace Endatix.Core.Infrastructure.Logging;

/// <summary>
/// Indicates that the property/field/parameter is sensitive and should be masked when logging. Use this to protect sensitive data from being logged.
/// <example>
/// <code>
/// public class SomeSentiveFoo
/// {
///     [Sensitive(SensitivityType.Email)]
///     public string Email { get; set; }
/// }
/// </code>
/// </example>
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class SensitiveAttribute(
    SensitivityType type = SensitivityType.Generic
) : Attribute
{
    /// <summary>
    /// The type of sensitivity for the property/field/parameter.
    /// </summary>
    public SensitivityType SensitivityType { get; } = type;
}