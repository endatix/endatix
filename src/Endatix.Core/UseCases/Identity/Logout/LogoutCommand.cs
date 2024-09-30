using System.Security.Claims;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.Login;

public record LogoutCommand(ClaimsPrincipal ClaimsPrincipal) : ICommand<Result>;
