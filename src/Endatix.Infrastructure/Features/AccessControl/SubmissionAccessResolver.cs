using Ardalis.Specification;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Authorization.Models;
using Endatix.Core.Authorization.Permissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Infrastructure.Features.AccessControl;

/// <summary>
/// Resolves submission access data based on the given context. Covers the different access scenarios and logic paths to resolve the access data for public access scenarios (no backend management).
/// </summary>
public sealed class SubmissionAccessResolver(
    IRepository<Form> formRepository,
    ISubmissionTokenService tokenService,
    ISubmissionAccessTokenService accessTokenService,
    ICurrentUserAuthorizationService authorizationService) : ISubmissionAccessResolver
{
    /// <inheritdoc/>
    public ValueTask<Result<SubmissionAccessData>> ResolveForAccessToken(SubmissionAccessContext context, CancellationToken cancellationToken)
    {
        var validationResult = accessTokenService.ValidateAccessToken(context.Token!);
        if (!validationResult.IsSuccess)
        {
            return
             ValueTask.FromResult(Result<SubmissionAccessData>.Invalid(validationResult.ValidationErrors));
        }

        var accessClaims = validationResult.Value!;
        var formPermissions = new HashSet<string>();
        var submissionPermissions = new HashSet<string>();

        if (accessClaims.Permissions.Contains(SubmissionAccessTokenPermissions.View.Name, StringComparer.OrdinalIgnoreCase))
        {
            formPermissions.UnionWith(ResourcePermissions.Form.Sets.ViewForm);
            submissionPermissions.UnionWith(ResourcePermissions.Submission.Sets.ReviewSubmission);
        }

        if (accessClaims.Permissions.Contains(SubmissionAccessTokenPermissions.Edit.Name, StringComparer.OrdinalIgnoreCase))
        {
            submissionPermissions.UnionWith(ResourcePermissions.Submission.Sets.EditSubmission);
        }

        if (accessClaims.Permissions.Contains(SubmissionAccessTokenPermissions.Export.Name, StringComparer.OrdinalIgnoreCase))
        {
            submissionPermissions.Add(ResourcePermissions.Submission.Export);
        }

        return ValueTask.FromResult(Result.Success(
            new SubmissionAccessData
            {
                FormId = context.FormId.ToString(),
                SubmissionId = accessClaims.SubmissionId.ToString(),
                FormPermissions = formPermissions,
                SubmissionPermissions = submissionPermissions,
                ExpiresAt = accessClaims.ExpiresAt
            }
        ));
    }

    public async ValueTask<Result<SubmissionAccessData>> ResolveForPrivateFormAsync(SubmissionAccessContext context, CancellationToken cancellationToken) => throw new NotImplementedException();
    public async ValueTask<Result<SubmissionAccessData>> ResolveForPublicFormAsync(SubmissionAccessContext context, CancellationToken cancellationToken) => throw new NotImplementedException();
    public async ValueTask<Result<SubmissionAccessData>> ResolveForSubmissionTokenAsync(SubmissionAccessContext context, CancellationToken cancellationToken) => throw new NotImplementedException();

    /// <inheritdoc/>
    public async ValueTask<bool> ResolveIsFormPublicAsync(long formId, CancellationToken cancellationToken)
    {

        var publicDtoSpec = new FormProjections.IsPublicDtoSpec();
        var spec = new FormSpecifications.ById(formId).WithProjectionOf(publicDtoSpec);
        var formDto = await formRepository.FirstOrDefaultAsync(spec, cancellationToken);

        return formDto?.IsPublic ?? false;
    }
}