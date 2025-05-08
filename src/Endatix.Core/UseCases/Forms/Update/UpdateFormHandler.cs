using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using MediatR;

namespace Endatix.Core.UseCases.Forms.Update;

public class UpdateFormHandler(
    IRepository<Form> repository,
    IMediator mediator) : ICommandHandler<UpdateFormCommand, Result<Form>>
{
    public async Task<Result<Form>> Handle(UpdateFormCommand request, CancellationToken cancellationToken)
    {
        var form = await repository.GetByIdAsync(request.FormId, cancellationToken);
        if (form == null)
        {
            return Result.NotFound("Form not found.");
        }

        var oldIsEnabled = form.IsEnabled;
        form.Name = request.Name;
        form.Description = request.Description;
        form.IsEnabled = request.IsEnabled;
        await repository.UpdateAsync(form, cancellationToken);

        await mediator.Publish(new FormUpdatedEvent(form), cancellationToken);

        if (oldIsEnabled != request.IsEnabled)
        {
            await mediator.Publish(new FormEnabledStateChangedEvent(form, request.IsEnabled), cancellationToken);
        }

        return Result.Success(form);
    }
}
