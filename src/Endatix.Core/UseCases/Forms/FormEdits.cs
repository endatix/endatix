using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using MediatR;

namespace Endatix.Core.UseCases.Forms;

internal sealed class FormEdits(EditableContent<Form> forms, IRepository<Submission> submissions, IMediator mediator)
{
    internal Task<Form?> FindAsync(long id, CancellationToken ct) => forms.FindAsync(id, ct);

    internal Task<Result<Form>?> ConflictAsync(SingleSubmissionGateChange change, CancellationToken ct) =>
        change.ConflictAsync(submissions, ct);

    internal async Task SaveAsync(Form form, CancellationToken ct)
    {
        await forms.UpdateAsync(form, ct);
        // Subscribers invalidate form-access caches after the aggregate and its outbox events are persisted.
        await mediator.Publish(new FormUpdatedEvent(form), ct);
    }
}
