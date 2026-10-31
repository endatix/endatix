using Endatix.Core.Abstractions.Forms;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Exceptions;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using MediatR;

namespace Endatix.Core.UseCases.Forms.Delete;

/// <summary>
/// Deletes a form with its definitions and every submission it has, in one transaction that holds the form's
/// <see cref="IFormSubmissionsLock"/>. Creates under way finish first and are deleted with the form; creates that
/// start meanwhile are refused, so once the deletion commits none of the form's submissions is live.
/// </summary>
public class DeleteFormHandler(
    IFormsRepository forms,
    IFormSubmissionsLock formSubmissionsLock,
    IMediator mediator) : ICommandHandler<DeleteFormCommand, Result<Form>>
{
    private const string FORM_NOT_FOUND_MESSAGE = "Form not found.";
    private const string BUSY_MESSAGE = "The form is busy; try again shortly.";

    public async Task<Result<Form>> Handle(DeleteFormCommand request, CancellationToken cancellationToken)
    {
        // Unlocked first: the delete of a missing form, or of another tenant's (form ids are public), must not hold up
        // that form's creates.
        if (!await forms.AnyAsync(new FormSpecifications.ByIdReadOnly(request.FormId), cancellationToken))
        {
            return Result.NotFound(FORM_NOT_FOUND_MESSAGE);
        }

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
        try
        {
            return await DeleteLockedAsync(formId, section, cancellationToken);
        }
        catch (DeadlockVictimException)
        {
            // Rolled back whole by the database; deleting again is safe.
            return Result.Unavailable(BUSY_MESSAGE);
        }
    }

    /// <summary>
    /// Reads the form again under the lock, so the read sees every create that committed before it, and a deletion
    /// that committed while this one waited. The submissions are deleted by form rather than loaded, in batches.
    /// </summary>
    /// <remarks>
    /// Submissions first, then the form and its definitions, the order a submission's writers lock them in: a
    /// writer locks its submission's row and, when it changes the row's form or definition key, checks those rows.
    /// Taking the form's rows first would let such a writer and this deletion each wait for the other.
    /// </remarks>
    private async Task<Result<Form>> DeleteLockedAsync(
        long formId,
        IFormSubmissionsSection section,
        CancellationToken cancellationToken)
    {
        var form = await forms.SingleOrDefaultAsync(new FormWithDefinitionsSpec(formId), cancellationToken);
        if (form is null)
        {
            return Result.NotFound(FORM_NOT_FOUND_MESSAGE);
        }

        await forms.DeleteSubmissionsAsync(form, cancellationToken);
        form.Delete();
        await forms.SaveChangesAsync(cancellationToken);
        await section.CommitAsync(cancellationToken);
        return Result.Success(form);
    }
}
