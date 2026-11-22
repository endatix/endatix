using Endatix.Core.Entities;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions.Repositories;

/// <summary>Form saves that report, rather than throw, a lost race on the form's settings.</summary>
internal static class FormRepositoryExtensions
{
    internal const string SETTINGS_CHANGED_MESSAGE = "The form's settings changed while the form was being saved; read the form again and retry.";

    /// <summary>
    /// Saves <paramref name="form"/>. While a form's webhook settings are kept in both places (endatix/endatix#1784), a
    /// save that changes them writes the form's settings too, so it can lose to another form save or a settings write
    /// on the same form.
    /// </summary>
    /// <returns>Success; or <see cref="ResultStatus.Conflict"/> when that race was lost: the whole save rolled back and
    /// its changes are no longer tracked (<see cref="FormSettingsChangedException"/>).</returns>
    public static async Task<Result> TryUpdateAsync(
        this IRepository<Form> forms,
        Form form,
        CancellationToken cancellationToken)
    {
        try
        {
            await forms.UpdateAsync(form, cancellationToken);
            return Result.Success();
        }
        catch (FormSettingsChangedException)
        {
            return Result.Conflict(SETTINGS_CHANGED_MESSAGE);
        }
    }
}
