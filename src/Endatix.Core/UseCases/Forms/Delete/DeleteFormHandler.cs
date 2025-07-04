using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using MediatR;

namespace Endatix.Core.UseCases.Forms.Delete;

public class DeleteFormHandler(IRepository<Form> _repository, IMediator mediator) : ICommandHandler<DeleteFormCommand, Result<Form>>
{
    public async Task<Result<Form>> Handle(DeleteFormCommand request, CancellationToken cancellationToken)
    {
        var spec = new FormWithDefinitionsAndSubmissionsSpec(request.FormId);
        var form = await _repository.SingleOrDefaultAsync(spec, cancellationToken);
        if (form == null)
        {
            return Result.NotFound("Form not found.");
        }

        form.Delete();
        await _repository.UpdateAsync(form, cancellationToken);

        await mediator.Publish(new FormDeletedEvent(form), cancellationToken);

        return Result.Success(form);
    }
}
