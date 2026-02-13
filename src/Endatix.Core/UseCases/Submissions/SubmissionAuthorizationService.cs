using System.Security.Cryptography;
using System.Text;
using Ardalis.Specification;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Abstractions.Submissions;

namespace Endatix.Core.UseCases.Submissions;

/// <summary>
/// Implementation of ISubmissionAuthorizationService that computes resource-based permissions.
/// Checks in order: Admin -> Access Token -> Authenticated User -> Public Form
/// </summary>
public class SubmissionAuthorizationService(
    ICurrentUserAuthorizationService authorizationService,
    IUserContext userContext,
    IRepository<Form> formRepository,
    ISubmissionAccessTokenService accessTokenService
) : ISubmissionAuthorizationService
{
    private static readonly TimeSpan DEFAULT_EXPIRATION = TimeSpan.FromMinutes(15);
    private const int ETAG_LENGTH = 12;

    public async Task<Result<SubmissionPermissionResult>> GetPermissionsAsync(
        long formId,
        long? submissionId,
        string? token,
        CancellationToken cancellationToken)
    {
        // Step 1: Check for Admin (platform or tenant)
        var adminResult = await CheckAdminPermissionsAsync(cancellationToken);
        if (adminResult.IsSuccess && adminResult.Value)
        {
            return Result.Success(CreateAdminPermissionResult(formId, submissionId));
        }

        // Step 2: Check for Access Token
        if (!string.IsNullOrEmpty(token))
        {
            var tokenResult = CheckTokenPermissions(token, formId, submissionId);
            if (tokenResult != null)
            {
                return tokenResult;
            }
        }

        // Step 3: Check for Authenticated User (RBAC)
        if (userContext.IsAuthenticated)
        {
            var userPermissions = await GetUserPermissionsAsync(formId, submissionId, cancellationToken);
            if (userPermissions != null)
            {
                return userPermissions;
            }
        }

        // Step 4: Public Form (Anonymous access)
        var publicPermissions = await GetPublicPermissionsAsync(formId, cancellationToken);
        return publicPermissions;
    }

    private async Task<Result<bool>> CheckAdminPermissionsAsync(CancellationToken cancellationToken)
    {
        var isPlatformAdmin = await authorizationService.IsPlatformAdminAsync(cancellationToken);
        if (isPlatformAdmin.IsSuccess && isPlatformAdmin.Value)
        {
            return Result.Success(true);
        }

        var isAdmin = await authorizationService.IsAdminAsync(cancellationToken);
        return isAdmin;
    }

    private Result<SubmissionPermissionResult>? CheckTokenPermissions(string token, long formId, long? submissionId)
    {
        var tokenValidationResult = accessTokenService.ValidateAccessToken(token);

        if (!tokenValidationResult.IsSuccess)
        {
            return null; // Continue to next check
        }

        var claims = tokenValidationResult.Value;
        var permissions = new List<string>();

        // Map token scope to permissions
        foreach (var permissionName in claims.Permissions)
        {
            switch (permissionName.ToLowerInvariant())
            {
                case "view":
                    permissions.Add(FormPermissions.View);
                    permissions.Add("submission.files.view");
                    permissions.Add("form.content.view");
                    break;
                case "edit":
                    permissions.Add(FormPermissions.Edit);
                    permissions.Add(FormPermissions.UploadFile);
                    permissions.Add("submission.files.upload");
                    permissions.Add("submission.files.delete");
                    // Include view permissions
                    permissions.Add(FormPermissions.View);
                    permissions.Add("submission.files.view");
                    permissions.Add("form.content.view");
                    break;
                case "export":
                    permissions.Add("submissions.export");
                    break;
            }
        }

        var now = DateTime.UtcNow;
        var expiresAt = claims.ExpiresAt;

        return Result.Success(new SubmissionPermissionResult
        {
            ResourceId = submissionId ?? formId,
            ResourceType = submissionId.HasValue ? "submission" : "form",
            Permissions = permissions.Distinct().ToList(),
            CachedAt = now,
            ExpiresAt = expiresAt,
            ETag = GenerateEtag(formId, submissionId, permissions)
        });
    }

    private async Task<Result<SubmissionPermissionResult>?> GetUserPermissionsAsync(
        long formId,
        long? submissionId,
        CancellationToken cancellationToken)
    {
        // Check for submission-specific permissions based on RBAC
        var hasSubmissionView = await authorizationService.HasPermissionAsync(
            Actions.Submissions.View, cancellationToken);

        if (!hasSubmissionView.IsSuccess || !hasSubmissionView.Value)
        {
            return null; // Continue to public check
        }

        var permissions = new List<string> { FormPermissions.View };

        var hasSubmissionEdit = await authorizationService.HasPermissionAsync(
            Actions.Submissions.Edit, cancellationToken);
        if (hasSubmissionEdit.IsSuccess && hasSubmissionEdit.Value)
        {
            permissions.Add(FormPermissions.Edit);
            permissions.Add(FormPermissions.UploadFile);
            permissions.Add("submission.files.upload");
            permissions.Add("submission.files.delete");
        }

        var hasSubmissionExport = await authorizationService.HasPermissionAsync(
            Actions.Submissions.Export, cancellationToken);
        if (hasSubmissionExport.IsSuccess && hasSubmissionExport.Value)
        {
            permissions.Add("submissions.export");
        }

        var now = DateTime.UtcNow;

        return Result.Success(new SubmissionPermissionResult
        {
            ResourceId = submissionId ?? formId,
            ResourceType = submissionId.HasValue ? "submission" : "form",
            Permissions = permissions.Distinct().ToList(),
            CachedAt = now,
            ExpiresAt = now.Add(DEFAULT_EXPIRATION),
            ETag = GenerateEtag(formId, submissionId, permissions)
        });
    }

    private async Task<Result<SubmissionPermissionResult>> GetPublicPermissionsAsync(
        long formId,
        CancellationToken cancellationToken)
    {
        // Get form to check if it's public
        var formSpec = new FormSpecifications.ByIdWithRelated(formId);
        var form = await formRepository.FirstOrDefaultAsync(formSpec, cancellationToken);

        if (form == null)
        {
            return Result<SubmissionPermissionResult>.NotFound("Form not found");
        }

        if (!form.IsPublic)
        {
            return Result<SubmissionPermissionResult>.Forbidden(
                "Access denied. Form is not public and no authentication provided.");
        }

        // Public form - allow create, file upload, and content view
        var permissions = new List<string>
        {
            FormPermissions.View,
            FormPermissions.UploadFile,
            "submission.files.upload",
            "form.content.view"
        };

        var now = DateTime.UtcNow;

        return Result.Success(new SubmissionPermissionResult
        {
            ResourceId = formId,
            ResourceType = "form",
            Permissions = permissions,
            CachedAt = now,
            ExpiresAt = now.Add(DEFAULT_EXPIRATION),
            ETag = GenerateEtag(formId, null, permissions)
        });
    }

    private static SubmissionPermissionResult CreateAdminPermissionResult(long formId, long? submissionId)
    {
        var permissions = new List<string>
        {
            FormPermissions.View,
            FormPermissions.Edit,
            FormPermissions.UploadFile,
            "submission.files.view",
            "submission.files.upload",
            "submission.files.delete",
            "submissions.export",
            "form.content.view"
        };

        var now = DateTime.UtcNow;

        return new SubmissionPermissionResult
        {
            ResourceId = submissionId ?? formId,
            ResourceType = submissionId.HasValue ? "submission" : "form",
            Permissions = permissions.Distinct().ToList(),
            CachedAt = now,
            ExpiresAt = now.Add(DEFAULT_EXPIRATION),
            ETag = GenerateEtag(formId, submissionId, permissions, isAdmin: true)
        };
    }

    private static string GenerateEtag(long formId, long? submissionId, List<string> permissions, bool isAdmin = false)
    {
        var content = $"{formId}:{submissionId}:{string.Join(",", permissions.OrderBy(p => p))}:{isAdmin}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(hash)[..ETAG_LENGTH];
    }
}
