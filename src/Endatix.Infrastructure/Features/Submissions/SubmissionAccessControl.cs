using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Infrastructure.Features.Submissions;

/// <summary>
/// Implementation of ISubmissionAccessControl that computes contextual submission authorization.
/// Returns simplified flat permission arrays for O(1) client-side access.
/// Identity (who the user is) should be fetched from /auth/me endpoint.
/// </summary>
public class SubmissionAccessControl(
    ICurrentUserAuthorizationService authorizationService,
    IRepository<Form> formRepository,
    ISubmissionTokenService tokenService
) : ISubmissionAccessControl
{
    public async Task<Result<FormAccessData>> GetAccessDataAsync(
        SubmissionAccessContext context,
        CancellationToken cancellationToken)
    {
        var identityResult = await authorizationService.GetAuthorizationDataAsync(cancellationToken);
        if (!identityResult.IsSuccess)
        {
            var errorMessage = identityResult.Errors?.FirstOrDefault() ?? "Failed to get authorization data";
            return Result<FormAccessData>.Error(errorMessage);
        }

        var identity = identityResult.Value;
        var formPermissions = new HashSet<string>();
        var submissionPermissions = new HashSet<string>();

        var isAdmin = await CheckIsAdminAsync(cancellationToken);
        if (isAdmin)
        {
            formPermissions.UnionWith(SubmissionPermissions.GetAllForResourceType(ResourceTypes.Form));

            if (context.SubmissionId.HasValue)
            {
                submissionPermissions.UnionWith(SubmissionPermissions.GetAllForResourceType(ResourceTypes.Submission));
            }
            else
            {
                submissionPermissions.Add(SubmissionPermissions.Submission.Create);
            }

            return Result<FormAccessData>.Success(new FormAccessData
            {
                FormPermissions = formPermissions,
                SubmissionPermissions = submissionPermissions
            });
        }

        await EvaluateFormLevelAccessAsync(context, identity, formPermissions, cancellationToken);

        if (context.SubmissionId.HasValue)
        {
            await EvaluateSubmissionLevelAccessAsync(context, identity, submissionPermissions, cancellationToken);
        }
        else
        {
            var hasSubmissionCreate = await authorizationService.HasPermissionAsync(Actions.Submissions.Create, cancellationToken);
            if (hasSubmissionCreate.IsSuccess && hasSubmissionCreate.Value)
            {
                submissionPermissions.Add(SubmissionPermissions.Submission.Create);
            }
        }

        return Result<FormAccessData>.Success(new FormAccessData
        {
            FormPermissions = formPermissions,
            SubmissionPermissions = submissionPermissions
        });
    }

    private async Task<bool> CheckIsAdminAsync(CancellationToken cancellationToken)
    {
        var isPlatformAdmin = await authorizationService.IsPlatformAdminAsync(cancellationToken);
        if (isPlatformAdmin.IsSuccess && isPlatformAdmin.Value)
        {
            return true;
        }

        var isAdmin = await authorizationService.IsAdminAsync(cancellationToken);
        return isAdmin.IsSuccess && isAdmin.Value;
    }

    private async Task EvaluateFormLevelAccessAsync(
        SubmissionAccessContext context,
        AuthorizationData identity,
        HashSet<string> permissions,
        CancellationToken cancellationToken)
    {
        var isPublic = await IsFormPublicAsync(context.FormId, cancellationToken);
        if (isPublic)
        {
            permissions.Add(SubmissionPermissions.Form.View);
        }

        if (identity.UserId != "anonymous")
        {
            var hasFormEdit = await authorizationService.HasPermissionAsync(Actions.Forms.Edit, cancellationToken);
            if (hasFormEdit.IsSuccess && hasFormEdit.Value)
            {
                permissions.Add(SubmissionPermissions.Form.Design);
            }
        }
    }

    private async Task EvaluateSubmissionLevelAccessAsync(
        SubmissionAccessContext context,
        AuthorizationData identity,
        HashSet<string> permissions,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(context.AccessToken))
        {
            var tokenResult = await tokenService.ResolveTokenAsync(context.AccessToken, cancellationToken);
            if (tokenResult.IsSuccess && tokenResult.Value == context.SubmissionId.Value)
            {
                permissions.Add(SubmissionPermissions.Submission.View);
                permissions.Add(SubmissionPermissions.Submission.ViewFiles);
                permissions.Add(SubmissionPermissions.Submission.Edit);
                permissions.Add(SubmissionPermissions.Submission.UploadFile);
                permissions.Add(SubmissionPermissions.Submission.DeleteFile);
            }
        }
        else if (identity.UserId != "anonymous")
        {
            var hasSubmissionView = await authorizationService.HasPermissionAsync(Actions.Submissions.View, cancellationToken);
            if (hasSubmissionView.IsSuccess && hasSubmissionView.Value)
            {
                permissions.Add(SubmissionPermissions.Submission.View);
            }

            var hasSubmissionEdit = await authorizationService.HasPermissionAsync(Actions.Submissions.Edit, cancellationToken);
            if (hasSubmissionEdit.IsSuccess && hasSubmissionEdit.Value)
            {
                permissions.Add(SubmissionPermissions.Submission.Edit);
                permissions.Add(SubmissionPermissions.Submission.UploadFile);
                permissions.Add(SubmissionPermissions.Submission.DeleteFile);
            }
        }
    }

    private async Task<bool> IsFormPublicAsync(long formId, CancellationToken cancellationToken)
    {
        var formSpec = new FormSpecifications.ByIdWithRelated(formId);
        var form = await formRepository.FirstOrDefaultAsync(formSpec, cancellationToken);
        return form?.IsPublic ?? false;
    }
}
