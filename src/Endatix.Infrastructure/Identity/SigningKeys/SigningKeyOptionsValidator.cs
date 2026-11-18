using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Identity.SigningKeys;

/// <summary>A signing key as an options type carries it: where it is configured, and its value.</summary>
internal sealed record SigningKeySetting(string ConfigPath, string? Value);

/// <summary>Startup validation of the signing keys an options type carries, by <see cref="SigningKeyRule"/>.</summary>
internal sealed class SigningKeyOptionsValidator<TOptions>(
    Func<TOptions, IEnumerable<SigningKeySetting>> keys,
    IReadOnlySet<string>? deniedHashes = null) : IValidateOptions<TOptions>
    where TOptions : class
{
    private readonly IReadOnlySet<string> _deniedHashes = deniedHashes ?? new HashSet<string>();

    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        var failures = keys(options)
            .Select(key => SigningKeyRule.Check(key.Value, key.ConfigPath, _deniedHashes))
            .OfType<string>()
            .ToList();

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
