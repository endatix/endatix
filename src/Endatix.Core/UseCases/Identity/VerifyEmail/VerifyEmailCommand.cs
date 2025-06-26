using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities.Identity;

namespace Endatix.Core.UseCases.Identity.VerifyEmail;

/// <summary>
/// Command to verify a user's email address.
/// </summary>
public record VerifyEmailCommand(string Token) : ICommand<Result<User>>
{
    /// <summary>
    /// The verification token.
    /// </summary>
    public string Token { get; init; } = Token;
} 