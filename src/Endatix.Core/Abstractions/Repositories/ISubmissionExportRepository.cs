using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;

namespace Endatix.Core.Abstractions.Repositories;

/// <summary>
/// Repository abstraction for fetching submission export rows for a given form.
/// </summary>
public interface ISubmissionExportRepository
{
    /// <summary>
    /// Gets export items for a given form using the specified SQL function.
    /// </summary>
    /// <typeparam name="T">The type of export row. Must implement <see cref="IExportItem"/>.</typeparam>
    /// <param name="formId">The form identifier</param>
    /// <param name="sqlFunctionName">Optional SQL function name to use. If null, uses the default export function.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An async enumerable of export rows</returns>
    IAsyncEnumerable<T> GetExportRowsAsync<T>(long formId, string? sqlFunctionName, CancellationToken cancellationToken) where T : class, IExportItem;
}