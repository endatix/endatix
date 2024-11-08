namespace Endatix.Core.Features.WebHooks;

/// <summary>
/// This class contains the headers sent when a WebHook is fired.
/// </summary>
public class WebHookRequestHeaders
{

    /// <summary>
    /// The event header. E.g. "form_created"
    /// </summary>
    public static string Event => $"{Constants.COMMON_HEADER_PREFIX}-Event";
    /// <summary>
    /// The entity header, e.g. "submission"
    /// </summary>
    public static string Entity => $"{Constants.COMMON_HEADER_PREFIX}-Entity";
    /// <summary>
    /// The action header, e.g. "created".
    /// </summary>
    public static string Action => $"{Constants.COMMON_HEADER_PREFIX}-Action";

    /// <summary>
    /// The hook id header. Unique Id associated with each Web Hook, so it can be traced and validated. Additionally, it can be used for ensuring indempotency of Web Hook requests.
    /// </summary>
    public static string HookId => $"{Constants.COMMON_HEADER_PREFIX}-Hook-Id";

    /// <summary>
    /// This header is sent if the webhook is configured with a secret. This is the HMAC hex digest of the request body, and is generated using the SHA-256 hash function and the secret as the HMAC key. 
    /// NOTE - this is not implemented YET
    /// </summary>
    public static string Siganture => $"{Constants.COMMON_HEADER_PREFIX}-Signature";


    /// <summary>
    /// The constants used in the headers.
    /// </summary>
    public class Constants
    {
        /// <summary>
        /// The common header prefix.
        /// </summary>
        public const string COMMON_HEADER_PREFIX = "X-Endatix";

        /// <summary>
        /// The value of the User Agent header that should be sent with each Web Hook request
        /// </summary>
        public const string ENDATIX_USER_AGENT = "Endatix-HookGun";
    }
}