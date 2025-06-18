using MediatR;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Abstractions.Submissions;

namespace Endatix.Core.UseCases.Submissions.Create;

public class CreateSubmissionHandler(
    IRepository<Submission> submissionRepository,
    IFormsRepository formRepository,
    ISubmissionTokenService tokenService,
    IMediator mediator
    ) : ICommandHandler<CreateSubmissionCommand, Result<Submission>>
{
    private const bool DEFAULT_IS_COMPLETE = false;
    private const int DEFAULT_CURRENT_PAGE = 1;
    private const string DEFAULT_METADATA = null;

    public async Task<Result<Submission>> Handle(CreateSubmissionCommand request, CancellationToken cancellationToken)
    {
        // Consider moving Domain event logic to a separate service should we decide to move UseCases in separate project. This way the Domain logic will stay in the core project. Will also centralize it in one place
        // This way the code will transform to  return await _submissionService.CreateSubmission(createSubmissionDto);
        var activeFormDefinitionSpec = new ActiveFormDefinitionByFormIdSpec(request.FormId);
        var formWithActiveDefinition = await formRepository.SingleOrDefaultAsync(activeFormDefinitionSpec, cancellationToken);
        var activeDefinition = formWithActiveDefinition?.ActiveDefinition;

        if (formWithActiveDefinition?.ActiveDefinition is null)
        {
            return Result.NotFound("Form not found. Cannot create a submission");
        }

        var submission = new Submission(
            activeDefinition!.TenantId,
            jsonData: request.JsonData,
            formId: request.FormId,
            formDefinitionId: activeDefinition!.Id,
            isComplete: request.IsComplete ?? DEFAULT_IS_COMPLETE,
            currentPage: request.CurrentPage ?? DEFAULT_CURRENT_PAGE,
            metadata: request.Metadata ?? DEFAULT_METADATA
        );

        await submissionRepository.AddAsync(submission, cancellationToken);
        await tokenService.ObtainTokenAsync(submission.Id, cancellationToken);
        
        if(submission.IsComplete) {
            await mediator.Publish(new SubmissionCompletedEvent(submission), cancellationToken);
        }

        return Result<Submission>.Created(submission);
    }
}
