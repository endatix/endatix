using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Folders;
using MediatR;

namespace Endatix.Core.UseCases.Forms.Update;

/// <summary>
/// Handler for updating a form.
/// </summary>
[DirectCallOnly]
public class UpdateFormHandler : ICommandHandler<UpdateFormCommand, Result<Form>>
{
    private readonly FormEdits _forms;
    private readonly FolderAssignmentPolicy _folderAssignmentPolicy;

    /// <summary>Uses the supplied repositories for direct calls.</summary>
    public UpdateFormHandler(IRepository<Form> repository,
        IRepository<Submission> submissionRepository,
        IMediator mediator,
        FolderAssignmentPolicy folderAssignmentPolicy)
        : this(new FormEdits(new EditableContent<Form>(repository, ContentQueryScope.Repository), submissionRepository, mediator), folderAssignmentPolicy) { }

    private UpdateFormHandler(FormEdits forms, FolderAssignmentPolicy folderAssignmentPolicy)
    {
        _forms = forms;
        _folderAssignmentPolicy = folderAssignmentPolicy;
    }

    /// <inheritdoc />
    public async Task<Result<Form>> Handle(UpdateFormCommand request, CancellationToken cancellationToken)
    {
        var form = await _forms.FindAsync(request.FormId, cancellationToken);
        if (form is null)
        {
            return Result.NotFound("Form not found.");
        }

        return await UpdateAsync(form, request, cancellationToken);
    }

    private async Task<Result<Form>> UpdateAsync(Form form, UpdateFormCommand request, CancellationToken cancellationToken)
    {
        var requestedLimitOnePerUser = request.LimitOnePerUser ?? form.LimitOnePerUser;
        var gate = new SingleSubmissionGateChange(form.Id, form.TenantId, form.LimitOnePerUser, requestedLimitOnePerUser, form.IsPublic);
        if (await _forms.ConflictAsync(gate, cancellationToken) is { } conflict)
        {
            return conflict;
        }

        var folderCheck = await _folderAssignmentPolicy.CheckMoveAsync(form.FolderId, request.FolderId, cancellationToken);
        if (!folderCheck.IsOk())
        {
            return folderCheck.ToErrorResult<Form>();
        }

        var webhook = UpdateFormWebhooks.Parse(request.WebHookSettingsJson);
        if (!form.MoveToFolder(request.FolderId))
        {
            return Result.Error("Form cannot be moved to the requested folder.");
        }
        form.SetEnabled(request.IsEnabled);
        form.UpdateWebHookSettings(webhook);

        // Applies the editable details, bumps the revision and raises form.updated (outbox) in one step —
        // before save so the capture is atomic. (UpdateFormCommand doesn't change IsPublic, so it's preserved.)
        form.UpdateDetails(
            request.Name,
            request.Description,
            form.IsPublic,
            requestedLimitOnePerUser,
            request.Metadata,
            ResolveSubmissionTokenExpiryHours(
                form.SubmissionTokenExpiryHours,
                request.SubmissionTokenExpiryHours,
                request.ClearSubmissionTokenExpiryHours));
        await _forms.SaveAsync(form, cancellationToken);

        return Result.Success(form);
    }

    private static int? ResolveSubmissionTokenExpiryHours(
        int? currentHours,
        int? requestedHours,
        bool clear)
    {
        if (clear)
        {
            return null;
        }

        return requestedHours ?? currentHours;
    }

    internal sealed class Registered(FormEdits forms, FolderAssignmentPolicy folderAssignmentPolicy)
        : RegisteredContentHandler<UpdateFormCommand, Result<Form>>(
            new UpdateFormHandler(forms, folderAssignmentPolicy));
}
