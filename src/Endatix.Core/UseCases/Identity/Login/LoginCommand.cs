using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.Login;

public record LoginCommand(string Email, string Password) : ICommand<Result<TokenDto>>;
