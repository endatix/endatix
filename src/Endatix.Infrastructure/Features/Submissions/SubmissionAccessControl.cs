using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Infrastructure.Features.Submissions;

/// <summary>
/// Implementation of ISubmissionAccessControl that composes cached RBAC with dynamic resource scopes.
/// Follows the "Cached Identity, Dynamic Scopes" pattern:
/// - Identity (RBAC): Stable, User-Centric, Long Cache (Session)
/// - Scopes (ReBAC): Volatile, Request-Centric, Computed on demand
/// </summary>
public class SubmissionAccessControl(
    ICurrentUserAuthorizationService authorizationService,
    IRepository<Form> formRepository,
    ISubmissionTokenService tokenService
) : ISubmissionAccessControl
{
    public async Task<Result<ResourceAccessData>> GetAccessDataAsync(
        SubmissionAccessContext context,
        CancellationToken cancellationToken)
    {
        var identityResult = await authorizationService.GetAuthorizationDataAsync(cancellationToken);
        if (!identityResult.IsSuccess)
        {
            var errorMessage = identityResult.Errors?.FirstOrDefault() ?? "Failed to get authorization data";
            return Result<ResourceAccessData>.Error(errorMessage);
        }

        var identity = identityResult.Value;
        var scopes = new List<ResourceScope>();

        var isAdmin = await CheckIsAdminAsync(cancellationToken);
        if (isAdmin)
        {
            scopes.Add(new ResourceScope(ResourceTypes.Form, context.FormId.ToString(), SubmissionPermissions.GetAllForResourceType(ResourceTypes.Form)));

            if (context.SubmissionId.HasValue)
            {
                scopes.Add(new ResourceScope(ResourceTypes.Submission, context.SubmissionId.Value.ToString(), SubmissionPermissions.GetAllForResourceType(ResourceTypes.Submission)));
            }
            else
            {
                scopes.Add(new ResourceScope(ResourceTypes.Submission, "new", [SubmissionPermissions.Submission.Create]));
            }

            return Result<ResourceAccessData>.Success(new ResourceAccessData
            {
                Identity = identity,
                Scopes = scopes
            });
        }

        var formPermissions = await EvaluateFormLevelAccessAsync(context, identity, cancellationToken);
        scopes.Add(formPermissions);

        if (context.SubmissionId.HasValue)
        {
            var subPermissions = await EvaluateSubmissionLevelAccessAsync(context, identity, cancellationToken);
            scopes.Add(subPermissions);
        }
        else
        {
            var hasSubmissionCreate = await authorizationService.HasPermissionAsync(Actions.Submissions.Create, cancellationToken);
            if (hasSubmissionCreate.IsSuccess && hasSubmissionCreate.Value)
            {
                scopes.Add(new ResourceScope(ResourceTypes.Submission, "new", [SubmissionPermissions.Submission.Create]));
            }
        }

        return Result<ResourceAccessData>.Success(new ResourceAccessData
        {
            Identity = identity,
            Scopes = scopes
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

    private async Task<ResourceScope> EvaluateFormLevelAccessAsync(
        SubmissionAccessContext context,
        AuthorizationData identity,
        CancellationToken cancellationToken)
    {
        var permissions = new HashSet<string>();

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

        return new ResourceScope(ResourceTypes.Form, context.FormId.ToString(), permissions.ToArray());
    }

    private async Task<ResourceScope> EvaluateSubmissionLevelAccessAsync(
        SubmissionAccessContext context,
        AuthorizationData identity,
        CancellationToken cancellationToken)
    {
        var permissions = new HashSet<string>();
        var subIdStr = context.SubmissionId!.Value.ToString();

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

        return new ResourceScope(ResourceTypes.Submission, subIdStr, permissions.ToArray());
    }

    private async Task<bool> IsFormPublicAsync(long formId, CancellationToken cancellationToken)
    {
        var formSpec = new FormSpecifications.ByIdWithRelated(formId);
        var form = await formRepository.FirstOrDefaultAsync(formSpec, cancellationToken);
        return form?.IsPublic ?? false;
    }
}
