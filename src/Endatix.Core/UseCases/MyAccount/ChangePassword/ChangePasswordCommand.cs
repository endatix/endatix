using System.Security.Claims;
using MediatR;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.MyAccount.ChangePassword;

/// <summary>
/// Command to change a user's password
/// </summary>
/// <param name="User">The ClaimsPrincipal of the authenticated user</param>
/// <param name="CurrentPassword">The user's current password</param>
/// <param name="NewPassword">The new password to set</param>
public record ChangePasswordCommand(ClaimsPrincipal User, string CurrentPassword, string NewPassword) : IRequest<Result<string>>;
