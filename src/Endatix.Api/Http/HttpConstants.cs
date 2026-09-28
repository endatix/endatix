namespace Endatix.Api;

/// <summary>
/// Shared HTTP values for Endatix responses.
/// </summary>
public static class HttpConstants
{
    /// <summary>
    /// Response <c>Content-Type</c> values.
    /// </summary>
    public static class ContentType
    {
        /// <summary>
        /// RFC7807 problem details. <c>WriteAsJsonAsync</c> may append a charset.
        /// </summary>
        public const string ProblemDetails = "application/problem+json";
    }
}
