using Endatix.Core.Abstractions.Authorization.PublicForm;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Api.Endpoints.Public.DataLists;

/// <summary>
/// A form's public data-list routes answer only for a list the form's definitions bind: search follows the active
/// definition, display values follow any definition of the form. Any other list answers exactly like one that does
/// not exist.
/// </summary>
internal static class PublicDataListScope
{
    private const string NOT_FOUND_MESSAGE = "Data list not found.";

    /// <summary>
    /// <c>null</c> when the form's definitions bind the list within <paramref name="reach"/>; otherwise the
    /// not-found result the data-list query returns for a missing list.
    /// </summary>
    public static async Task<Result<T>?> RefuseUnlessFormReachesAsync<T>(
        IFormRuntimeScopeReader runtimeScopeReader,
        long formId,
        long dataListId,
        DataListReach reach,
        CancellationToken cancellationToken)
    {
        var reaches = await runtimeScopeReader.ReachesDataListAsync(formId, dataListId, reach, cancellationToken);
        return reaches ? null : Result<T>.NotFound(NOT_FOUND_MESSAGE);
    }
}
