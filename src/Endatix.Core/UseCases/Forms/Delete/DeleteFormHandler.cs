using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Forms;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Exceptions;
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
[DirectCallOnly]
public class DeleteFormHandler : ICommandHandler<DeleteFormCommand, Result<Form>>
{
    private readonly DeletableForms _forms;
    private readonly IFormSubmissionsLock _formSubmissionsLock;
    private readonly IMediator _mediator;

    public DeleteFormHandler(IFormsRepository forms, IFormSubmissionsLock formSubmissionsLock, IMediator mediator)
        : this(new DeletableForms(forms, ContentQueryScope.Repository), formSubmissionsLock, mediator) { }

    private DeleteFormHandler(DeletableForms forms, IFormSubmissionsLock formSubmissionsLock, IMediator mediator) =>
        (_forms, _formSubmissionsLock, _mediator) = (forms, formSubmissionsLock, mediator);

    private const string FORM_NOT_FOUND_MESSAGE = "Form not found.";
    private const string BUSY_MESSAGE = "The form is busy; try again shortly.";

    public async Task<Result<Form>> Handle(DeleteFormCommand request, CancellationToken cancellationToken)
    {
        // Unlocked first: the delete of a missing form, or of another tenant's (form ids are public), must not hold up
        // that form's creates.
        if (!await _forms.ExistsAsync(request.FormId, cancellationToken))
        {
            return Result.NotFound(FORM_NOT_FOUND_MESSAGE);
        }

        return await DeleteAsync(request.FormId, cancellationToken);
    }

    private async Task<Result<Form>> DeleteAsync(long formId, CancellationToken cancellationToken)
    {
        var deleted = await DeleteUnderLockAsync(formId, cancellationToken);
        if (deleted.IsSuccess)
        {
            await _mediator.Publish(new FormDeletedEvent(deleted.Value), cancellationToken);
        }

        return deleted;
    }

    /// <summary>Returns once the lock is released, so the in-process handlers of the deletion never run under it.</summary>
    private async Task<Result<Form>> DeleteUnderLockAsync(long formId, CancellationToken cancellationToken)
    {
        var locked = await _formSubmissionsLock.BeginDeletingAsync(formId, cancellationToken);
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
        var form = await _forms.DeleteAsync(formId, cancellationToken);
        if (form is null)
        {
            return Result.NotFound(FORM_NOT_FOUND_MESSAGE);
        }
        await section.CommitAsync(cancellationToken);
        return Result.Success(form);
    }

    internal sealed class Registered(DeletableForms forms, IFormSubmissionsLock formSubmissionsLock, IMediator mediator)
        : RegisteredContentHandler<DeleteFormCommand, Result<Form>>(
            new DeleteFormHandler(forms, formSubmissionsLock, mediator));
}
