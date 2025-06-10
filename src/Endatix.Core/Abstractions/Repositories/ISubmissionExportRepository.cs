using Endatix.Core.Entities;

namespace Endatix.Core.Abstractions.Repositories;

/// <summary>
/// Repository abstraction for fetching submission export rows for a given form.
/// </summary>
public interface ISubmissionExportRepository
{
    IAsyncEnumerable<SubmissionExportRow> GetExportRowsAsync(long formId, CancellationToken cancellationToken);
}