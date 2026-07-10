namespace Endatix.Modules.Reporting.Features.FlattenedSubmission;

/// <summary>
/// Processes a submission into the reporting flattened read model (outbox worker entry point).
/// </summary>
public interface ISubmissionFlatteningProcessor
{
    /// <summary>
    /// Processes a submission into the reporting flattened read model.
    /// </summary>
    /// <param name="tenantId">The ID of the tenant.</param>
    /// <param name="formId">The ID of the form.</param>
    /// <param name="submissionId">The ID of the submission.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ProcessAsync(long tenantId, long formId, long submissionId, CancellationToken cancellationToken);
}
