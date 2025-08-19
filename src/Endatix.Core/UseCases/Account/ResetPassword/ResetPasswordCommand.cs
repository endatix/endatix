using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Account.ResetPassword;

public record ResetPasswordCommand(string Email, string ResetCode, string NewPassword) : ICommand<Result<string>>;