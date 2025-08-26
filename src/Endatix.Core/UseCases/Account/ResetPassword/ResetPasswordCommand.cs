using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Account.ResetPassword;

public record ResetPasswordCommand : ICommand<Result<string>>
{
    public string Email { get; init; }
    public string ResetCode { get; init; }
    public string NewPassword { get; init; }

    public ResetPasswordCommand(string email, string resetCode, string newPassword)
    {
        Guard.Against.NullOrWhiteSpace(email);
        Guard.Against.NullOrWhiteSpace(resetCode);
        Guard.Against.NullOrWhiteSpace(newPassword);

        Email = email;
        ResetCode = resetCode;
        NewPassword = newPassword;
    }
}