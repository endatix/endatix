using Endatix.Core.Abstractions.Authorization;
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
public class UpdateFormHandler(
    IRepository<Form> repository,
    IRepository<Submission> submissionRepository,
    IMediator mediator,
    FolderAssignmentPolicy folderAssignmentPolicy,
    AccountStanding standing) : ICommandHandler<UpdateFormCommand, Result<Form>>
{
    /// <summary>Creates a handler with dormant account standing.</summary>
    public UpdateFormHandler(IRepository<Form> repository, IRepository<Submission> submissionRepository,
        IMediator mediator, FolderAssignmentPolicy folderAssignmentPolicy)
        : this(repository, submissionRepository, mediator, folderAssignmentPolicy, new AccountStanding()) { }

    /// <inheritdoc />
    public async Task<Result<Form>> Handle(UpdateFormCommand request, CancellationToken cancellationToken)
    {
        var access = new ManagedFormEditAccess(repository, folderAssignmentPolicy, standing, submissionRepository);
        return await access.RefusalAsync(request, cancellationToken) ?? await UpdateAsync(request, cancellationToken);
    }

    private async Task<Result<Form>> UpdateAsync(UpdateFormCommand request, CancellationToken cancellationToken)
    {
        var form = await repository.GetByIdAsync(request.FormId, cancellationToken);
        if (form is null)
        {
            return Result.NotFound("Form not found.");
        }

        var requestedLimitOnePerUser = request.LimitOnePerUser ?? form.LimitOnePerUser;
        var gate = new SingleSubmissionGateChange(form.Id, form.TenantId, form.LimitOnePerUser, requestedLimitOnePerUser, form.IsPublic);
        if (await gate.ConflictAsync(submissionRepository, cancellationToken) is { } conflict)
        {
            return conflict;
        }

        form.SetEnabled(request.IsEnabled); // raises form.enabled_state_changed (outbox) on an actual change

        var folderCheck = await folderAssignmentPolicy.EnsureAndApplyFolderMoveAsync(
            form.FolderId,
            request.FolderId,
            targetFolderId => form.MoveToFolder(targetFolderId),
            "Form cannot be moved to the requested folder.",
            cancellationToken);
        if (!folderCheck.IsOk())
        {
            return folderCheck.ToErrorResult<Form>();
        }

        form.UpdateWebHookSettings(ParseWebHookSettings(request.WebHookSettingsJson));

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
        await repository.UpdateAsync(form, cancellationToken);

        // Kept for the in-process MediatR subscriber (form-access cache invalidation); the webhook now flows
        // via the outbox. The enabled-state change is raised on the aggregate (SetEnabled → outbox) and has no
        // in-process subscriber, so it is not published here.
        await mediator.Publish(new FormUpdatedEvent(form), cancellationToken);

        return Result.Success(form);
    }

    private static WebHookConfiguration? ParseWebHookSettings(string? webHookSettingsJson)
    {
        if (string.IsNullOrWhiteSpace(webHookSettingsJson))
        {
            return null;
        }

        var webHookConfig = System.Text.Json.JsonSerializer.Deserialize<WebHookConfiguration>(webHookSettingsJson);
        if (webHookConfig?.Events is null || webHookConfig.Events.Count == 0)
        {
            return null;
        }

        return webHookConfig;
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
}
