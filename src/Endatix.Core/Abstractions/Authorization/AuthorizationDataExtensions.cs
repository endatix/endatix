using System;

namespace Endatix.Core.Abstractions.Authorization;

/// <summary>
/// AuthorizationData-related extension helpers.
/// </summary>
public static class AuthorizationDataExtensions
{
    private static readonly TimeSpan _immediateTtl = TimeSpan.FromSeconds(1);

    public static TimeSpan ComputeAuthTtl(
        this AuthorizationData? authData,
        DateTime utcNow,
        TimeSpan? defaultTtl = null)
    {
        if (authData is null)
        {
            return defaultTtl ?? _immediateTtl;
        }

        var authDataSafeTtl = authData.ExpiresAt - utcNow;
        return TimeSpan.FromSeconds(Math.Max(_immediateTtl.TotalSeconds, authDataSafeTtl.TotalSeconds));
    }
}