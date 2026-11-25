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

        if (await ApplyAssignmentsAsync(form, request, cancellationToken) is { } refused)
        {
            return refused;
        }
        form.SetEnabled(request.IsEnabled ?? form.IsEnabled);

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

    private async Task<Result<Form>?> ApplyAssignmentsAsync(Form form, PartialUpdateFormCommand request, CancellationToken cancellationToken)
    {
        var theme = await ResolveThemeAsync(form, request.ThemeId, cancellationToken);
        if (!theme.IsSuccess)
        {
            return theme.ToErrorResult<Form>();
        }
        var webhook = new Form.WebHookSettingsChange(UpdateFormWebhooks.Parse(request.WebHookSettingsJson));
        if (await ApplyFolderAsync(form, request, cancellationToken) is { } folder)
        {
            return folder;
        }

        ApplyTheme(form, request.ThemeId, theme.Value);
        if (request.WebHookSettingsJson is not null)
        {
            webhook.ApplyTo(form);
        }
        return null;
    }

    private static void ApplyTheme(Form form, long? id, Theme? theme)
    {
        if (id.HasValue && id != form.ThemeId)
        {
            form.SetTheme(theme);
        }
    }

    private async Task<Result<Theme?>> ResolveThemeAsync(Form form, long? themeId, CancellationToken ct)
    {
        if (themeId is null or DEFAULT_THEME_ID || themeId == form.ThemeId)
        {
            return Result<Theme?>.Success(null);
        }
        var theme = await _themeRepository.FindAsync(themeId.Value, ct);
        return theme is null ? Result.NotFound("Form Theme not found.") : Result<Theme?>.Success(theme);
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

    internal sealed class Registered(FormEdits forms, EditableContent<Theme> themeRepository, FolderAssignmentPolicy folderAssignmentPolicy)
        : RegisteredContentHandler<PartialUpdateFormCommand, Result<Form>>(
            new PartialUpdateFormHandler(forms, themeRepository, folderAssignmentPolicy));
}
