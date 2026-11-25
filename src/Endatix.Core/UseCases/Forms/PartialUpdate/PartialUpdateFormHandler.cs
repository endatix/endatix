using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Folders;
using MediatR;

namespace Endatix.Core.UseCases.Forms.PartialUpdate;

/// <summary>
/// Handler for partially updating a form.
/// </summary>
[DirectCallOnly]
public class PartialUpdateFormHandler : ICommandHandler<PartialUpdateFormCommand, Result<Form>>
{
    private readonly FormEdits _forms;
    private readonly EditableContent<Theme> _themeRepository;
    private readonly FolderAssignmentPolicy _folderAssignmentPolicy;

    /// <summary>Uses the supplied repositories for direct calls.</summary>
    public PartialUpdateFormHandler(IRepository<Form> repository,
        IRepository<Theme> themeRepository,
        IRepository<Submission> submissionRepository,
        IMediator mediator,
        FolderAssignmentPolicy folderAssignmentPolicy)
        : this(new FormEdits(new EditableContent<Form>(repository, ContentQueryScope.Repository), submissionRepository, mediator), new EditableContent<Theme>(themeRepository, ContentQueryScope.Repository), folderAssignmentPolicy) { }

    private PartialUpdateFormHandler(FormEdits forms, EditableContent<Theme> themeRepository, FolderAssignmentPolicy folderAssignmentPolicy)
    {
        _forms = forms;
        _themeRepository = themeRepository;
        _folderAssignmentPolicy = folderAssignmentPolicy;
    }

    private const long DEFAULT_THEME_ID = 0; // ThemeId of 0 means clear the theme (set to default)

    public async Task<Result<Form>> Handle(PartialUpdateFormCommand request, CancellationToken cancellationToken)
    {
        var form = await _forms.FindAsync(request.FormId, cancellationToken);
        if (form is null)
        {
            return Result.NotFound("Form not found.");
        }

        return await UpdateAsync(form, request, cancellationToken);
    }

    private async Task<Result<Form>> UpdateAsync(Form form, PartialUpdateFormCommand request, CancellationToken cancellationToken)
    {
        var requestedLimitOnePerUser = request.LimitOnePerUser ?? form.LimitOnePerUser;
        var gate = new SingleSubmissionGateChange(form.Id, form.TenantId, form.LimitOnePerUser, requestedLimitOnePerUser, request.IsPublic ?? form.IsPublic);
        if (await _forms.ConflictAsync(gate, cancellationToken) is { } conflict)
        {
            return conflict;
        }

        form.SetEnabled(request.IsEnabled ?? form.IsEnabled); // raises form.enabled_state_changed (outbox) on a change

        var theme = await ApplyThemeAsync(form, request.ThemeId, cancellationToken);
        if (theme is not null)
        {
            return theme;
        }

        if (request.WebHookSettingsJson != null)
        {
            UpdateWebHookSettings(request, form);
        }

        var folder = await ApplyFolderAsync(form, request, cancellationToken);
        if (folder is not null)
        {
            return folder;
        }

        // Applies the editable details, bumps the revision and raises form.updated (outbox) in one step —
        // before save so the capture is atomic. Omitted fields keep their current value.
        form.UpdateDetails(
            request.Name ?? form.Name,
            request.Description ?? form.Description,
            request.IsPublic ?? form.IsPublic,
            requestedLimitOnePerUser,
            request.Metadata ?? form.Metadata,
            ResolveExpiry(form, request));
        await _forms.SaveAsync(form, cancellationToken);

        return Result.Success(form);
    }

    private async Task<Result<Form>?> ApplyThemeAsync(Form form, long? themeId, CancellationToken cancellationToken)
    {
        if (!themeId.HasValue || form.ThemeId == themeId)
        {
            return null;
        }
        if (themeId.Value == DEFAULT_THEME_ID)
        {
            form.SetTheme(null);
            return null;
        }
        var theme = await _themeRepository.FindAsync(themeId.Value, cancellationToken);
        if (theme is null)
        {
            return Result.NotFound("Form Theme not found.");
        }

        form.SetTheme(theme);
        return null;
    }

    private async Task<Result<Form>?> ApplyFolderAsync(Form form, PartialUpdateFormCommand request, CancellationToken cancellationToken)
    {
        if (!request.ClearFolderId && !request.FolderId.HasValue)
        {
            return null;
        }

        var clearing = request.ClearFolderId;
        var check = await _folderAssignmentPolicy.EnsureAndApplyFolderMoveAsync(
            form.FolderId,
            clearing ? null : request.FolderId,
            clearing ? _ => form.ClearFolder() : form.MoveToFolder,
            clearing ? "Form cannot be cleared from the requested folder." : "Form cannot be moved to the requested folder.",
            cancellationToken);
        return check.IsOk() ? null : check.ToErrorResult<Form>();
    }

    private static int? ResolveExpiry(Form form, PartialUpdateFormCommand request)
    {
        if (request.ClearSubmissionTokenExpiryHours)
        {
            return null;
        }

        return request.SubmissionTokenExpiryHours ?? form.SubmissionTokenExpiryHours;
    }

    private static void UpdateWebHookSettings(PartialUpdateFormCommand request, Form form)
    {
        WebHookConfiguration? webHookConfig;

        if (request.WebHookSettingsJson!.Trim() == string.Empty)
        {
            webHookConfig = null;
        }
        else
        {
            webHookConfig = System.Text.Json.JsonSerializer.Deserialize<WebHookConfiguration>(request.WebHookSettingsJson);

            if (webHookConfig?.Events == null || webHookConfig.Events.Count == 0)
            {
                webHookConfig = null;
            }
        }

        form.UpdateWebHookSettings(webHookConfig);
    }

    internal sealed class Registered(FormEdits forms, EditableContent<Theme> themeRepository, FolderAssignmentPolicy folderAssignmentPolicy)
        : RegisteredContentHandler<PartialUpdateFormCommand, Result<Form>>(
            new PartialUpdateFormHandler(forms, themeRepository, folderAssignmentPolicy));
}
