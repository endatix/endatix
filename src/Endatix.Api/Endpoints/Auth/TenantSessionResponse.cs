using Endatix.Core.UseCases.Identity;

namespace Endatix.Api.Endpoints.Auth;

public sealed record TenantSessionResponse(string AccessToken, string RefreshToken)
{
    internal static TenantSessionResponse Map(AuthTokensDto tokens) =>
        new(tokens.AccessToken.Token, tokens.RefreshToken.Token);
}
