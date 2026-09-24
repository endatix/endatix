using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>
/// Settings bound from <c>Endatix:WebHookDoorbell</c>. When <see cref="Enabled"/> is <c>true</c> the
/// relay hands each webhook event to an external delivery worker instead of delivering it inline.
/// The other keys are required only when it is enabled.
/// </summary>
public sealed class WebHookDoorbellOptions
{
    public const string SectionName = "Endatix:WebHookDoorbell";

    /// <summary>Minimum length of the shared worker key.</summary>
    public const int MinimumWorkerApiKeyLength = 32;

    /// <summary>
    /// When <c>true</c>, webhook events are handed to the worker at <see cref="WorkerBaseUrl"/>.
    /// Read once at startup.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Absolute http or https base address of the delivery worker.</summary>
    public string? WorkerBaseUrl { get; set; }

    /// <summary>Shared secret sent in <c>X-Endatix-Worker-Key</c>; at least 32 characters.</summary>
    public string? WorkerApiKey { get; set; }

    /// <summary>Timeout of one call to the worker, 1–60 seconds.</summary>
    public int TimeoutSeconds { get; set; } = 10;
}

/// <summary>
/// Validates <see cref="WebHookDoorbellOptions"/> at startup; with the doorbell disabled nothing is required.
/// </summary>
public sealed class WebHookDoorbellOptionsValidator : IValidateOptions<WebHookDoorbellOptions>
{
    private const string Section = WebHookDoorbellOptions.SectionName;

    public ValidateOptionsResult Validate(string? name, WebHookDoorbellOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        List<string> failures = [];

        if (!Uri.TryCreate(options.WorkerBaseUrl, UriKind.Absolute, out var workerBaseUrl)
            || (workerBaseUrl.Scheme != Uri.UriSchemeHttp && workerBaseUrl.Scheme != Uri.UriSchemeHttps))
        {
            failures.Add($"{Section}:WorkerBaseUrl must be an absolute http or https URL.");
        }

        if (options.WorkerApiKey is null || options.WorkerApiKey.Length < WebHookDoorbellOptions.MinimumWorkerApiKeyLength)
        {
            failures.Add($"{Section}:WorkerApiKey must be at least {WebHookDoorbellOptions.MinimumWorkerApiKeyLength} characters.");
        }

        if (options.TimeoutSeconds is < 1 or > 60)
        {
            failures.Add($"{Section}:TimeoutSeconds must be between 1 and 60.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
