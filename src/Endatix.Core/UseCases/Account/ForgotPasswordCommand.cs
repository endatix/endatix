using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Account;

public record ForgotPasswordCommand(string Email) : ICommand<Result<string>>;