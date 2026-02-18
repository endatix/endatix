using Ardalis.Specification;
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
            formPermissions.UnionWith(ResourcePermissions.GetAllForResourceType(ResourceTypes.Form));

            if (context.SubmissionId.HasValue)
            {
                submissionPermissions.UnionWith(ResourcePermissions.GetAllForResourceType(ResourceTypes.Submission));
            }
            else
            {
                submissionPermissions.Add(ResourcePermissions.Submission.Create);
                submissionPermissions.Add(ResourcePermissions.Submission.UploadFile);
            }

            return Result<FormAccessData>.Success(new FormAccessData
            {
                FormId = context.FormId.ToString(),
                SubmissionId = context.SubmissionId?.ToString(),
                FormPermissions = formPermissions,
                SubmissionPermissions = submissionPermissions
            });
        }

        var isFormPublic = await IsFormPublicAsync(context.FormId, cancellationToken);
        if (isFormPublic)
        {
            formPermissions.Add(ResourcePermissions.Form.View);
        }

        await EvaluateFormLevelAccessAsync(formPermissions, cancellationToken);

        if (context.SubmissionId.HasValue)
        {
            await EvaluateSubmissionLevelAccessAsync(context, identity, submissionPermissions, cancellationToken);
        }
        else if (isFormPublic)
        {
            submissionPermissions.Add(ResourcePermissions.Submission.Create);
            submissionPermissions.Add(ResourcePermissions.Submission.UploadFile);
        }

        return Result<FormAccessData>.Success(new FormAccessData
        {
            FormId = context.FormId.ToString(),
            SubmissionId = context.SubmissionId?.ToString(),
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
        HashSet<string> permissions,
        CancellationToken cancellationToken)
    {
        var hasFormEdit = await authorizationService.HasPermissionAsync(Actions.Forms.Edit, cancellationToken);
        if (hasFormEdit.IsSuccess && hasFormEdit.Value)
        {
            permissions.Add(ResourcePermissions.Form.Edit);
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
                permissions.UnionWith(ResourcePermissions.Submission.Sets.ReviewSubmission);
            }
        }
        else if (identity.UserId != AuthorizationData.ANONYMOUS_USER_ID)
        {
            var hasSubmissionView = await authorizationService.HasPermissionAsync(Actions.Submissions.View, cancellationToken);
            if (hasSubmissionView.IsSuccess && hasSubmissionView.Value)
            {
                permissions.Add(ResourcePermissions.Submission.View);
            }

            var hasSubmissionEdit = await authorizationService.HasPermissionAsync(Actions.Submissions.Edit, cancellationToken);
            if (hasSubmissionEdit.IsSuccess && hasSubmissionEdit.Value)
            {
                permissions.UnionWith(ResourcePermissions.Submission.Sets.EditSubmission);
            }

            var hasSubmissionDelete = await authorizationService.HasPermissionAsync(Actions.Submissions.Delete, cancellationToken);
            if (hasSubmissionDelete.IsSuccess && hasSubmissionDelete.Value)
            {
                permissions.Add(ResourcePermissions.Submission.DeleteFile);
            }
        }
    }

    private async Task<bool> IsFormPublicAsync(long formId, CancellationToken cancellationToken)
    {
        var byIdSpec = new FormSpecifications.ById(formId);
        var isPublicDtoSpec = new FormProjections.IsPublicDtoSpec();

        var formDto = await formRepository.FirstOrDefaultAsync(byIdSpec.WithProjectionOf(isPublicDtoSpec), cancellationToken);
        return formDto?.IsPublic ?? false;
    }
}
