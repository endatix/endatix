using Endatix.Core.Infrastructure.Logging;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.Login;

public record LoginCommand(string Email, string Password) : ICommand<Result<AuthTokensDto>>
{
    public string Email { get; init; } = Email;
    [Sensitive]
    public string Password { get; init; } = Password;
}
