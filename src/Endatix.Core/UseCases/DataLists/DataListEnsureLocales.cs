using Endatix.Core.Common.Translations;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;

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
    /// <returns><c>null</c> on success; otherwise validation errors.</returns>
    public static IReadOnlyList<ValidationError>? TryEnsure(
        DataList dataList,
        IEnumerable<string>? ensureLocales)
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
                errors.Add(new ValidationError
                {
                    Identifier = $"EnsureLocales.{culture.Value}",
                    ErrorMessage = "Could not add locale."
                });
            }
        }

        return errors.Count == 0 ? null : errors;
    }
}
