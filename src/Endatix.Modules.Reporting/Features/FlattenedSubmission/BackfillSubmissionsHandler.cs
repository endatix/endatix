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
    IRepository<FormDefinition> formDefinitionsRepository,
    ISubmissionBackfillProcessor backfillProcessor) : ICommandHandler<BackfillSubmissionsCommand, Result<SubmissionBackfillResult>>
{
    /// <inheritdoc/>
    public async Task<Result<SubmissionBackfillResult>> Handle(
        BackfillSubmissionsCommand request,
        CancellationToken cancellationToken)
    {
        FormDefinitionsByFormIdSpec formExistsSpec = new(request.FormId);
        var formExists = await formDefinitionsRepository.AnyAsync(formExistsSpec, cancellationToken);
        if (!formExists)
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
