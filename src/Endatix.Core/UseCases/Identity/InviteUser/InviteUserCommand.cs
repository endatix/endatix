using Ardalis.GuardClauses;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Logging;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.InviteUser;

/// <summary>
/// Command to invite a user to a tenant.
/// </summary>
public sealed record InviteUserCommand : ICommand<Result<User>>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InviteUserCommand"/> class.
    /// </summary>
    /// <param name="email">The email address of the user to invite.</param>
    /// <param name="roleNames">The roles to assign to the user.</param>
    public InviteUserCommand(string email, IReadOnlyList<string>? roleNames = null)
    {
        Guard.Against.NullOrWhiteSpace(email);

        Email = email.Trim();
        RoleNames = roleNames?
            .Where(roleName => !string.IsNullOrWhiteSpace(roleName))
            .Select(roleName => roleName.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }

    [Sensitive(SensitivityType.Email)]
    public string Email { get; }

    public IReadOnlyList<string> RoleNames { get; }
}
