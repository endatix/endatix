namespace Endatix.Api.Endpoints.MyAccount;

/// <summary>
/// Request model for changing a password
/// </summary>
/// <param name="CurrentPassword">The user's current password</param>
/// <param name="NewPassword">The new password to set</param>
/// <param name="ConfirmPassword">The new password to set</param>
public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword
)
{
    /// <summary>
    /// The user's current password
    /// </summary>
    public string CurrentPassword { get; init; } = CurrentPassword;

    /// <summary>
    /// The new password to set
    /// </summary>
    public string NewPassword { get; init; } = NewPassword;

    /// <summary>
    /// The new password to set
    /// </summary>
    public string ConfirmPassword { get; init; } = ConfirmPassword;
}