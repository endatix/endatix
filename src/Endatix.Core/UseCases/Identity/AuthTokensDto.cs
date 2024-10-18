namespace Endatix.Core.UseCases.Identity;

public record class AuthTokensDto(TokenDto AccessToken, TokenDto RefreshToken);
