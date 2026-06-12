using System.Text.RegularExpressions;
using Endatix.Infrastructure.Identity.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Features.Submitters;

internal sealed class SubmitterOptionsValidator(IConfiguration configuration) : IValidateOptions<SubmitterOptions>
{
    private static readonly TimeSpan _regexMatchTimeout = TimeSpan.FromMilliseconds(50);
    private const int MaxClaimTypeCount = 20;
    private static readonly Regex _claimNameRegex = new("^[A-Za-z][A-Za-z0-9_./-]*$", RegexOptions.Compiled, _regexMatchTimeout);

    public ValidateOptionsResult Validate(string? name, SubmitterOptions options)
    {
        List<string> failures = [];

        ValidateClaimTypes(options.DisplayIdClaimTypes, nameof(options.DisplayIdClaimTypes), failures);
        ValidateClaimTypes(options.ProfileSnapshotFields, nameof(options.ProfileSnapshotFields), failures);

        if (IsKeycloakEnabled() && options.DisplayIdClaimTypes.Count is 0)
        {
            failures.Add($"{nameof(options.DisplayIdClaimTypes)} must contain at least one claim type when Keycloak authentication is enabled.");
        }

        return failures.Count is 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateClaimTypes(List<string> claimTypes, string optionName, List<string> failures)
    {
        if (claimTypes.Count > MaxClaimTypeCount)
        {
            failures.Add($"{optionName} cannot contain more than {MaxClaimTypeCount} entries.");
        }

        for (var index = 0; index < claimTypes.Count; index++)
        {
            var trimmed = claimTypes[index]?.Trim() ?? string.Empty;
            claimTypes[index] = trimmed;

            if (string.IsNullOrWhiteSpace(trimmed) || !_claimNameRegex.IsMatch(trimmed))
            {
                failures.Add($"{optionName} contains an invalid claim type: '{trimmed}'.");
            }
        }
    }

    private bool IsKeycloakEnabled()
    {
        return configuration.GetValue<bool?>($"Endatix:Auth:Providers:{AuthProviders.Keycloak}:Enabled") == true;
    }
}
