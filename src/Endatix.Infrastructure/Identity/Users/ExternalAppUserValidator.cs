using Endatix.Infrastructure.Identity.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Endatix.Infrastructure.Identity.Users;

/// <summary>
/// Validator for external app users.
/// </summary>
internal sealed class ExternalAppUserValidator : IUserValidator<AppUser>
{
    /// <inheritdoc />
    public Task<IdentityResult> ValidateAsync(UserManager<AppUser> manager, AppUser user)
    {
        List<IdentityError> errors = [];

        if (string.IsNullOrWhiteSpace(user.AuthProvider))
        {
            errors.Add(Error(nameof(AppUser.AuthProvider), "Authentication provider is required."));
        }
        else
        {
            var isNativeUser = user.AuthProvider == AuthProviders.Endatix;

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                errors.Add(Error(
                    nameof(AppUser.Email),
                    isNativeUser
                        ? "Email is required for native Endatix users."
                        : "Email is required for external users."));
            }

            if (!isNativeUser && string.IsNullOrWhiteSpace(user.ExternalSubjectId))
            {
                errors.Add(Error(
                    nameof(AppUser.ExternalSubjectId),
                    "External subject id is required for external users."));
            }
        }

        return Task.FromResult(errors.Count == 0
            ? IdentityResult.Success
            : IdentityResult.Failed(errors.ToArray()));
    }

    private static IdentityError Error(string code, string description) =>
        new() { Code = code, Description = description };
}
