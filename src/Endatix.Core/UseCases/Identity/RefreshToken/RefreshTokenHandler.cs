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
        var accessTokenValidationResult = tokenService.ValidateAccessToken(request.AccessToken, validateLifetime: false);

        if (accessTokenValidationResult.IsInvalid())
        {
            return Result.Invalid(accessTokenValidationResult.ValidationErrors);
        }

        if (!accessTokenValidationResult.IsSuccess)
        {
            return Result.Error();
        }

        var userId = accessTokenValidationResult.Value;
        var refreshTokenValidationResult = await authService.ValidateRefreshToken(userId, request.RefreshToken, cancellationToken);

        if (refreshTokenValidationResult.IsInvalid())
        {
            return Result.Invalid(refreshTokenValidationResult.ValidationErrors);
        }

        if (!refreshTokenValidationResult.IsSuccess)
        {
            return Result.Error();
        }

        var user = refreshTokenValidationResult.Value;
        var accessToken = tokenService.IssueAccessToken(user);
        var refreshToken = tokenService.IssueRefreshToken();

        await authService.StoreRefreshToken(user.Id, refreshToken.Token, refreshToken.ExpireAt, cancellationToken);

        return Result.Success(new AuthTokensDto(accessToken, refreshToken));
    }
}
