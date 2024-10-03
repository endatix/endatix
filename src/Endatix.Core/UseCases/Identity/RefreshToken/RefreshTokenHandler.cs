using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.RefreshToken;

/// <summary>
/// Handles the refresh token logic by validating the refresh token and issuing new access and refresh tokens.
/// </summary>
public class RefreshTokenHandler(IAuthService authService, ITokenService tokenService) : ICommandHandler<RefreshTokenCommand, Result<AuthTokensDto>>
{
    public async Task<Result<AuthTokensDto>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await authService.ValidateRefreshToken(request.UserId, request.RefreshToken, cancellationToken);

        if (validationResult.IsInvalid())
        {
            return Result.Invalid(validationResult.ValidationErrors);
        }

        var user = validationResult.Value;
        var accessToken = tokenService.IssueAccessToken(user);
        var refreshToken = tokenService.IssueRefreshToken();

        await authService.StoreRefreshToken(user.Id, refreshToken.Token, refreshToken.ExpireAt, cancellationToken);

        return Result.Success(new AuthTokensDto(accessToken, refreshToken));
    }
}
