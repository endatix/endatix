namespace Endatix.Core.Infrastructure.Logging;

/// <summary>
/// The type of sensitivity for a property. Used to determine how to mask the property value when logging sensitive information.
/// </summary>
public enum SensitivityType
{
    Email,
    Secret,
    PhoneNumber,
    Name,
    Generic
}