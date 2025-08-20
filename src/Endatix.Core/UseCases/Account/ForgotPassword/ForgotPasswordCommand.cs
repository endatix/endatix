using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Account.ForgotPassword;

public record ForgotPasswordCommand : ICommand<Result<string>>
{
    public string Email { get; init; }

    public ForgotPasswordCommand(string email)
    {
        Guard.Against.NullOrWhiteSpace(email);

        Email = email;
    }
}