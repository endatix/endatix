using Endatix.Core.UseCases.Forms;

namespace Endatix.Core.Abstractions.Forms;

/// <summary>
/// Checks for form dependencies on a data list.
/// </summary>
public interface IDataListDependencyChecker
{
    /// <summary>
    /// Checks if data list has any form that are using it.
    /// </summary>
    /// <param name="dataListId">The ID of the data list to check.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the data list has any dependencies, false otherwise.</returns>
    Task<bool> HasFormDependenciesAsync(long dataListId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all forms that are using the data list.
    /// </summary>
    /// <param name="dataListId">The ID of the data list to get the dependent forms for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The dependent forms.</returns>
    Task<IReadOnlyCollection<FormDto>> GetDependentFormsAsync(long dataListId, CancellationToken cancellationToken = default);
}
