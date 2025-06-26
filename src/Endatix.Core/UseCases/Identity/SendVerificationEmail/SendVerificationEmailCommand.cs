using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.SendVerificationEmail;

/// <summary>
/// Command to send a verification email to a user.
/// </summary>
public record SendVerificationEmailCommand(string Email) : ICommand<Result>
{
    /// <summary>
    /// The email address to send the verification email to.
    /// </summary>
    public string Email { get; init; } = Email;
} 