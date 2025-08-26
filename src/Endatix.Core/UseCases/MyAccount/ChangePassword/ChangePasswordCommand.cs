using MediatR;
using Endatix.Core.Infrastructure.Result;
using Ardalis.GuardClauses;

namespace Endatix.Core.UseCases.MyAccount.ChangePassword;

/// <summary>
/// Command to change a user's password
/// </summary>
public record ChangePasswordCommand : IRequest<Result<string>>
{
    /// <summary>
    /// The user ID of the authenticated user
    /// </summary>
    public long? UserId { get; init; }

    /// <summary>
    /// The user's current password
    /// </summary>
    public string CurrentPassword { get; init; }

    /// <summary>
    /// The new password to set
    /// </summary>
    public string NewPassword { get; init; }



    /// <param name="userId">The user ID of the authenticated user</param>
    /// <param name="currentPassword">The user's current password</param>
    /// <param name="newPassword">The new password to set</param>
    public ChangePasswordCommand(long? userId, string currentPassword, string newPassword)
    {
        Guard.Against.NullOrWhiteSpace(currentPassword);
        Guard.Against.NullOrWhiteSpace(newPassword);

        UserId = userId;
        CurrentPassword = currentPassword;
        NewPassword = newPassword;
    }
}
