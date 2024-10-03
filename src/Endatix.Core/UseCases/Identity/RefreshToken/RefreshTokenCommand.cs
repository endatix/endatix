using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.RefreshToken;

public record RefreshTokenCommand(long UserId, string RefreshToken) : ICommand<Result<AuthTokensDto>>;
