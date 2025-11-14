using Endatix.Infrastructure.Identity.Authentication.Providers;

namespace Endatix.Infrastructure.Identity;

/// <summary>
/// Mapper for JwtOptions to EndatixJwtOptions.
/// This is used to map the obsolete JwtOptions to the new EndatixJwtOptions.
/// This is a temporary solution to avoid breaking changes.
/// It will be removed in the future.
/// </summary>
/// <returns>The new EndatixJwtOptions.</returns>
[Obsolete("Will be removed with the depreciated JwtOptions class. This is a temporary solution to avoid breaking changes.")]
public static class JwtOptionsMapper
{
    /// <summary>
    /// Map the obsolete JwtOptions to the new EndatixJwtOptions when the endatixJwtOptions is not provided.
    /// </summary>
    /// <param name="jwtOptions">The obsolete JwtOptions.</param>
    /// <param name="endatixJwtOptions">The new EndatixJwtOptions. If not provided, a new instance will be created.</param>
    /// <returns>The new EndatixJwtOptions.</returns>
    public static EndatixJwtOptions Map(JwtOptions jwtOptions, EndatixJwtOptions? endatixJwtOptions = null)
    {
        var targetOptions = endatixJwtOptions ?? new EndatixJwtOptions();

        if (jwtOptions is null)
        {
            return targetOptions;
        }

        if (string.IsNullOrEmpty(targetOptions.SigningKey) && !string.IsNullOrEmpty(jwtOptions.SigningKey))
        {
            targetOptions.SigningKey = jwtOptions.SigningKey;
        }

        if (string.IsNullOrEmpty(targetOptions.Issuer) && !string.IsNullOrEmpty(jwtOptions.Issuer))
        {
            targetOptions.Issuer = jwtOptions.Issuer;
        }

        if (targetOptions.Audiences.Count is default(int) && jwtOptions.Audiences.Count > 0)
        {
            targetOptions.Audiences = jwtOptions.Audiences;
        }

        if (targetOptions.AccessExpiryInMinutes is default(int) && jwtOptions.AccessExpiryInMinutes > 0)
        {
            targetOptions.AccessExpiryInMinutes = jwtOptions.AccessExpiryInMinutes;
        }

        if (targetOptions.RefreshExpiryInDays is default(int) && jwtOptions.RefreshExpiryInDays > 0)
        {
            targetOptions.RefreshExpiryInDays = jwtOptions.RefreshExpiryInDays;
        }

        return targetOptions;
    }
}