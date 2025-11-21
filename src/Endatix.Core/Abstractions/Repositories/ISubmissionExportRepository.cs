using Endatix.Core.Entities;

namespace Endatix.Core.Abstractions.Repositories;

/// <summary>
/// Repository abstraction for fetching submission export rows for a given form.
/// </summary>
public interface ISubmissionExportRepository
{
    /// <summary>
    /// Gets export rows for a given form using the specified SQL function.
    /// </summary>
    /// <param name="formId">The form identifier</param>
    /// <param name="sqlFunctionName">Optional SQL function name to use. If null, uses the default export function.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An async enumerable of submission export rows</returns>
    IAsyncEnumerable<SubmissionExportRow> GetExportRowsAsync(long formId, string? sqlFunctionName, CancellationToken cancellationToken);
}