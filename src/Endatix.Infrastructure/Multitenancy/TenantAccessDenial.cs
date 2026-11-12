using Microsoft.AspNetCore.Http;

namespace Endatix.Infrastructure.Multitenancy;

/// <summary>
/// Marks a request whose signed-in user was refused access to the tenant during sign-in, for example a user stored at
/// another tenant or locked out, so the tenant check can tell the user to ask their administrator for approval.
/// </summary>
public static class TenantAccessDenial
{
    private static readonly object _itemKey = new();

    /// <summary>Marks <paramref name="context"/> as refused access to the tenant.</summary>
    /// <param name="context">The current request, or null outside a request.</param>
    public static void Mark(HttpContext? context)
    {
        if (context is not null)
        {
            context.Items[_itemKey] = true;
        }
    }

    /// <summary>Whether <paramref name="context"/> was refused access to the tenant during sign-in.</summary>
    /// <param name="context">The current request.</param>
    /// <returns>True when sign-in refused the user access to the tenant.</returns>
    public static bool IsMarked(HttpContext context) => context.Items.ContainsKey(_itemKey);
}
