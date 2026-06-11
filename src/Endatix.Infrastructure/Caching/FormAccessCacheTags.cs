namespace Endatix.Infrastructure.Caching;

internal static class FormAccessCacheTags
{
    /// <summary>Entity-level tag. Covers all cache entries for a form.</summary>
    public static string ForForm(long formId) => $"form:{formId}";

    /// <summary>Access-level tag. Covers only access and routing entries for a form.</summary>
    public static string ForFormAccess(long formId) => $"access:form:{formId}";

    public static string[] ForFormAndAccess(long formId) =>
        [ForForm(formId), ForFormAccess(formId)];
}
