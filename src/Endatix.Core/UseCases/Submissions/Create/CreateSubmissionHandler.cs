﻿using MediatR;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Submissions.Create;

public class CreateSubmissionHandler(
    IRepository<Submission> submissionRepository,
    IRepository<FormDefinition> formDefinitionRepository,
    IMediator mediator
    ) : ICommandHandler<CreateSubmissionCommand, Result<Submission>>
{
    private const bool DEFAULT_IS_COMPLETE = true;
    private const int DEFAULT_CURRENT_PAGE = 1;
    private const string DEFAULT_METADATA = null;

    public async Task<Result<Submission>> Handle(CreateSubmissionCommand request, CancellationToken cancellationToken)
    {
        // Consider moving Domain event logic to a separate service should we decide to move UseCases in separate project. This way the Domain logic will stay in the core project. Will also centralize it in one place
        // This way the code will transform to  return await _submissionService.CreateSubmission(createSubmissionDto);
        var activeFormDefinitionSpec = new ActiveFormDefinitionByFormIdSpec(request.FormId);
        var activeFormDefinition = await formDefinitionRepository.SingleOrDefaultAsync(activeFormDefinitionSpec, cancellationToken);

        if (activeFormDefinition == null)
        {
            return Result.NotFound("Form not found. Cannot create a submission");
        }

        var submission = new Submission(
            request.JsonData,
            activeFormDefinition.Id,
            request.IsComplete ?? DEFAULT_IS_COMPLETE,
            request.CurrentPage ?? DEFAULT_CURRENT_PAGE,
            request.MetaData ?? DEFAULT_METADATA
        );

        await submissionRepository.AddAsync(submission, cancellationToken);

        await mediator.Publish(new SubmissionCompletedEvent(submission), cancellationToken);

        return Result<Submission>.Created(submission);
    }
}
