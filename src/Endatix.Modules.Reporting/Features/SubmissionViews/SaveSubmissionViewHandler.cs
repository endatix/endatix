using Endatix.Core.Entities;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Modules.Reporting.Data;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Reporting.Features.SubmissionViews;

/// <summary>Saves the submissions grid view everyone on a form sees.</summary>
/// <param name="Save">The save, made from the view read at its revision.</param>
public sealed record SaveSubmissionViewCommand(SubmissionViewSave Save) : ICommand<Result<SubmissionViewDto>>;

/// <summary>
/// Saves the form's shared view when the caller read the latest revision, so one editor never silently overwrites
/// another's change.
/// </summary>
internal sealed class SaveSubmissionViewHandler(
    IRepository<Form> formsRepository,
    ISubmissionViewRepository viewRepository,
    ILogger<SaveSubmissionViewHandler> logger)
    : ICommandHandler<SaveSubmissionViewCommand, Result<SubmissionViewDto>>
{
    internal const string ConflictMessage =
        "Someone else saved this view after you loaded it. Reload the view and save again.";

    /// <inheritdoc />
    public async Task<Result<SubmissionViewDto>> Handle(
        SaveSubmissionViewCommand request,
        CancellationToken cancellationToken)
    {
        var save = request.Save;
        if (save.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }

        var formExists = await formsRepository.AnyAsync(
            new FormSpecifications.ByIdForTenant(save.FormId, save.TenantId),
            cancellationToken);
        if (!formExists)
        {
            return Result.NotFound("Form not found.");
        }

        return await SaveAsync(save, cancellationToken);
    }

    private async Task<Result<SubmissionViewDto>> SaveAsync(SubmissionViewSave save, CancellationToken cancellationToken)
    {
        try
        {
            var saved = await viewRepository.SaveSharedAsync(save, cancellationToken);
            return saved is null
                ? Result.Conflict(ConflictMessage)
                : Result.Success(new SubmissionViewDto(saved.Revision, saved.SettingsJson));
        }
        catch (DomainValidationException ex)
        {
            return InvalidSettings(ex, save.FormId);
        }
    }

    private Result<SubmissionViewDto> InvalidSettings(DomainValidationException exception, long formId) =>
        Result.Invalid(new ValidationError
        {
            Identifier = "Settings",
            ErrorMessage = SafeError.LogAndResolve(
                logger,
                exception,
                "Invalid settings.",
                $"saving the submission view of form {formId}"),
        });
}
