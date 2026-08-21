using Endatix.Core.Infrastructure.Logging;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Represents the request for the "/register" endpoint, handled by the <see cref="Register.ExecuteAsync"/> method.
/// </summary>
public record RegisterRequest(string Email, string Password, string ConfirmPassword, string? TenantSlug = null)
{
    /// <summary>
    /// The email address of the user.
    /// </summary>
    [Sensitive(SensitivityType.Email)]
    public string Email { get; init; } = Email;

    /// <summary>
    /// The password chosen by the user.
    /// </summary>
    [Sensitive(SensitivityType.Secret)]
    public string Password { get; init; } = Password;

    /// <summary>
    /// The confirmation of the password chosen by the user.
    /// </summary>
    [Sensitive(SensitivityType.Secret)]
    public string ConfirmPassword { get; init; } = ConfirmPassword;

    /// <summary>
    /// Optional opaque tenant public id. When set, registers into that tenant if self-registration is enabled.
    /// </summary>
    public string? TenantSlug { get; init; } = TenantSlug;
}
