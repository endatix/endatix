using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Attributes;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.Register;

public record RegisterCommand(string Email, string Password) : ICommand<Result<User>>
{
    public string Email { get; init; } = Email;
    [Sensitive]
    public string Password { get; init; } = Password;
}
