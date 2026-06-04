using Ardalis.GuardClauses;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Logging;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.ActivateInvite;

/// <summary>
/// Command to activate a tenant invitation.
/// </summary>
public sealed record ActivateInviteCommand : ICommand<Result<User>>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActivateInviteCommand"/> class.
    /// </summary>
    /// <param name="token">The one-time invitation token.</param>
    /// <param name="password">The new password to set for the account.</param>
    public ActivateInviteCommand(string token, string password)
    {
        Guard.Against.NullOrWhiteSpace(token);
        Guard.Against.NullOrWhiteSpace(password);

        Token = token;
        Password = password;
    }

    [Sensitive(SensitivityType.Secret)]
    public string Token { get; }

    [Sensitive(SensitivityType.Secret)]
    public string Password { get; }
}
