using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Endatix.Modules.Reporting.Configuration;

internal sealed class ReportingQuestionLimitValidator(IConfiguration configuration) : IValidateOptions<ReportingOptions>
{
    public ValidateOptionsResult Validate(string? name, ReportingOptions options)
    {
        var reporting = configuration.GetSection(ReportingOptions.SECTION_NAME);
        var host = reporting.GetSection(nameof(options.MaxQuestions));
        if (host.GetChildren().Any())
        {
            return ValidateOptionsResult.Fail($"{host.Path} must be a positive integer.");
        }

        var overrides = reporting.GetSection(nameof(options.MaxQuestionsByTenant));
        if (overrides.Value is not null || options.MaxQuestionsByTenant is null)
        {
            return ValidateOptionsResult.Fail($"{overrides.Path} must map tenant IDs to positive integer limits.");
        }

        // The configuration binder skips dictionary entries it cannot convert.
        var failures = overrides.GetChildren().Where(entry => !IsValid(entry))
            .Select(entry => $"{entry.Path} must name a positive tenant ID and have a positive integer limit.").ToList();
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsValid(IConfigurationSection entry) =>
        long.TryParse(entry.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var tenantId) && tenantId > 0 &&
        tenantId.ToString(CultureInfo.InvariantCulture) == entry.Key &&
        int.TryParse(entry.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit) && limit > 0 &&
        !entry.GetChildren().Any();
}
