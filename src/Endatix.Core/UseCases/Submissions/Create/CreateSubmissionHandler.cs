using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Submissions.Create;

public class CreateSubmissionHandler : ICommandHandler<CreateSubmissionCommand, Result<Submission>>
{
    private const bool DEFAULT_IS_COMPLETE = true;
    private const int DEFAULT_CURRENT_PAGE = 1;
    private const string DEFAULT_METADATA = null;

    private readonly IRepository<Submission> _submissionRepository;
    private readonly IRepository<FormDefinition> _formDefinitionRepository;
    private readonly IMediator _mediator;

    public CreateSubmissionHandler(IRepository<Submission> submissionRepository, IRepository<FormDefinition> formDefinitionRepository, IMediator mediator)
    {
        _submissionRepository = submissionRepository;
        _formDefinitionRepository = formDefinitionRepository;
        _mediator = mediator;
    }

    public async Task<Result<Submission>> Handle(CreateSubmissionCommand request, CancellationToken cancellationToken)
    {
        // Consider moving Domain event logic to a separate service should we decide to move UseCases in separate project. This way the Domain logic will stay in the core project. Will also centralize it in one place
        // This way the code will transform to  return await _submissionService.CreateSubmission(createSubmissionDto);
        var activeFormDefinitionSpec = new ActiveFormDefinitionByFormIdSpec(request.FormId);
        var activeFormDefinition = await _formDefinitionRepository.SingleOrDefaultAsync(activeFormDefinitionSpec, cancellationToken);

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

        await _submissionRepository.AddAsync(submission, cancellationToken);

        var domainEvent = new SubmissionCompletedEvent(submission);
        await _mediator.Publish(domainEvent, cancellationToken);

        return Result<Submission>.Created(submission);
    }
}
