using Endatix.Core.Common.Translations;
using Endatix.Core.Entities;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.UseCases.DataLists;

/// <summary>
/// Adds cultures to a data list catalog before an import that references them.
/// </summary>
public static class DataListEnsureLocales
{
    /// <summary>
    /// Ensures each requested locale is present in <paramref name="dataList"/>'s AvailableLocales.
    /// Already-present locales are no-ops. Invalid codes and catalog capacity errors are returned as validation failures.
    /// </summary>
    /// <param name="dataList">The catalog to extend.</param>
    /// <param name="ensureLocales">Locale tokens the import references.</param>
    /// <param name="logger">Logger for the calling handler. Every rejection is logged before it is returned.</param>
    /// <returns><c>null</c> on success; otherwise validation errors.</returns>
    public static IReadOnlyList<ValidationError>? TryEnsure(
        DataList dataList,
        IEnumerable<string>? ensureLocales,
        ILogger logger)
    {
        List<ValidationError> errors = [];

        foreach (var token in TranslationLocaleList.Tokenize(ensureLocales))
        {
            if (!CultureCode.TryParse(token, out var culture))
            {
                errors.Add(new ValidationError
                {
                    Identifier = $"EnsureLocales.{token}",
                    ErrorMessage = $"'{token}' is not a valid culture code."
                });
                continue;
            }

            if (culture.IsSyntheticDefault)
            {
                errors.Add(new ValidationError
                {
                    Identifier = "EnsureLocales.default",
                    ErrorMessage = "The synthetic 'default' key cannot be added as a culture."
                });
                continue;
            }

            try
            {
                dataList.AddCulture(culture);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                errors.Add(ToEnsureLocaleError(dataList.Id, culture, ex, logger));
            }
        }

        return errors.Count == 0 ? null : errors;
    }

    /// <summary>
    /// Turns a rejected <see cref="DataList.AddCulture"/> into a validation error.
    /// </summary>
    /// <remarks>
    /// A domain rule that opted into <see cref="IEndUserSafeError"/> - the culture cap, for one - reaches
    /// the caller intact rather than being masked into an undiagnosable "Could not add locale.", while
    /// anything unexpected falls back to author-written text and is logged with the exception.
    /// </remarks>
    private static ValidationError ToEnsureLocaleError(
        long dataListId,
        CultureCode culture,
        Exception ex,
        ILogger logger) =>
        new()
        {
            Identifier = $"EnsureLocales.{culture.Value}",
            ErrorMessage = SafeError.LogAndResolve(
                logger,
                ex,
                "Could not add locale.",
                $"adding locale '{culture.Value}' to data list {dataListId}")
        };
}
