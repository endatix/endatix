using Endatix.Core.Abstractions.Forms;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using MediatR;

namespace Endatix.Core.UseCases.Forms.Delete;

/// <summary>
/// Deletes a form with its definitions and every submission it has, in one transaction that holds the form's
/// <see cref="IFormSubmissionsLock"/>. Creates under way finish first, and creates that start meanwhile wait and
/// then find the form gone, so once the deletion commits none of the form's submissions is live.
/// </summary>
public class DeleteFormHandler(
    IFormsRepository forms,
    IFormSubmissionsLock formSubmissionsLock,
    IMediator mediator) : ICommandHandler<DeleteFormCommand, Result<Form>>
{
    public async Task<Result<Form>> Handle(DeleteFormCommand request, CancellationToken cancellationToken)
    {
        var deleted = await DeleteUnderLockAsync(request.FormId, cancellationToken);
        if (deleted.IsSuccess)
        {
            await mediator.Publish(new FormDeletedEvent(deleted.Value), cancellationToken);
        }

        return deleted;
    }

    /// <summary>Returns once the lock is released, so the in-process handlers of the deletion never run under it.</summary>
    private async Task<Result<Form>> DeleteUnderLockAsync(long formId, CancellationToken cancellationToken)
    {
        var locked = await formSubmissionsLock.BeginDeletingAsync(formId, cancellationToken);
        if (!locked.IsSuccess)
        {
            return locked.ToErrorResult<Form>();
        }

        await using var section = locked.Value;
        return await DeleteLockedAsync(formId, section, cancellationToken);
    }

    /// <summary>
    /// Reads the form under the lock, so the read sees every create that committed before it. The submissions are
    /// deleted by form rather than loaded, which keeps the section short for the creates waiting on it.
    /// </summary>
    private async Task<Result<Form>> DeleteLockedAsync(
        long formId,
        IFormSubmissionsSection section,
        CancellationToken cancellationToken)
    {
        var form = await forms.SingleOrDefaultAsync(new FormWithDefinitionsSpec(formId), cancellationToken);
        if (form is null)
        {
            return Result.NotFound("Form not found.");
        }

        form.Delete();
        await forms.UpdateAsync(form, cancellationToken);
        await forms.DeleteSubmissionsAsync(form, cancellationToken);
        await section.CommitAsync(cancellationToken);
        return Result.Success(form);
    }
}
