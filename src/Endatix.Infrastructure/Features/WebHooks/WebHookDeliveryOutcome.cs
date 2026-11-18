namespace Endatix.Infrastructure.Features.WebHooks;

/// <summary>What one webhook send to one endpoint came to.</summary>
internal enum WebHookDeliveryOutcome
{
    /// <summary>The endpoint answered with a success status.</summary>
    Delivered,

    /// <summary>The send failed or the endpoint answered otherwise; a retry may succeed.</summary>
    Failed,

    /// <summary>The endpoint's address is not allowed; no request was sent and a retry gets the same answer.</summary>
    Refused,
}
