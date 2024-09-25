using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Register;

public record RegisterCommand(string Email, string Password) : ICommand<Result<User>>{

}
