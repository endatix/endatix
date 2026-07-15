using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Modules.Reporting.Features.FlattenedSubmission;

/// <summary>
/// Handler for the backfill submissions command.
/// </summary>
public sealed class BackfillSubmissionsHandler(
    IRepository<Form> formsRepository,
    ISubmissionBackfillProcessor backfillProcessor) : ICommandHandler<BackfillSubmissionsCommand, Result<SubmissionBackfillResult>>
{
    /// <inheritdoc/>
    public async Task<Result<SubmissionBackfillResult>> Handle(
        BackfillSubmissionsCommand request,
        CancellationToken cancellationToken)
    {
        ActiveFormDefinitionByFormIdSpec formSpec = new(request.FormId);
        var form = await formsRepository.SingleOrDefaultAsync(formSpec, cancellationToken);
        if (form is null || form.TenantId != request.TenantId)
        {
            return Result.NotFound("Form not found.");
        }

        SubmissionBackfillOptions options = new(
            BatchSize: request.BatchSize,
            AfterSubmissionId: request.AfterSubmissionId,
            Force: request.Force);

        var result = await backfillProcessor.BackfillFormAsync(
            request.TenantId,
            request.FormId,
            options,
            cancellationToken);

        return Result.Success(result);
    }
}
