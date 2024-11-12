using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.RefreshToken;

public record RefreshTokenCommand(string AccessToken, string RefreshToken) : ICommand<Result<AuthTokensDto>>;
