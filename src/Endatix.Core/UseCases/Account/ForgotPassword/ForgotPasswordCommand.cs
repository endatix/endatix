using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Account.ForgotPassword;

public record ForgotPasswordCommand(string Email) : ICommand<Result<string>>;