using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Endatix.Core.Abstractions.Authorization.PublicForm;
using Endatix.Core.Abstractions.Submissions;

namespace Endatix.Core.Authorization.Access;

/// <summary>
/// Authorization data for public form/submission access control
/// </summary>
public class PublicFormAccessData : AccessDataBase
{
    /// <summary>
    /// Parameterless constructor for JSON deserialization only (used by HybridCache).
    /// </summary>
    [JsonConstructor]
    private PublicFormAccessData() { }

    private PublicFormAccessData(
        string formId,
        string? submissionId,
        IEnumerable<string> formPermissions,
        IEnumerable<string> submissionPermissions,
        long? formTenantId = null)
    {
        FormId = formId;
        SubmissionId = submissionId;
        FormTenantId = formTenantId;

        var normalizedFormPermissions = ToImmutableSet(formPermissions);
        var normalizedSubmissionPermissions = ToImmutableSet(submissionPermissions);

        FormPermissions = normalizedFormPermissions;
        SubmissionPermissions = normalizedSubmissionPermissions;
        Permissions = ToImmutableSet(normalizedFormPermissions.Union(normalizedSubmissionPermissions));
    }

    /// <inheritdoc/>
    [JsonIgnore]
    public override ImmutableHashSet<string> Permissions { get; init; } = EmptyPermissions;

    /// <summary>
    /// The form ID this access data applies to.
    /// </summary>
    public string FormId { get; init; } = string.Empty;

    /// <summary>
    /// The submission ID if applicable (null for new submissions).
    /// </summary>
    public string? SubmissionId { get; init; }

    /// <summary>
    /// Permissions for the form resource.
    /// </summary>
    public ImmutableHashSet<string> FormPermissions { get; init; } = EmptyPermissions;

    /// <summary>
    /// Permissions for the submission resource (or "new" submission when no submissionId provided).
    /// </summary>
    public ImmutableHashSet<string> SubmissionPermissions { get; init; } = EmptyPermissions;

    /// <summary>
    /// The Form's Tenant ID used to accommodate tenant-scoped queries.
    /// </summary>
    public long? FormTenantId { get; init; }

    /// <summary>
    /// Creates public form access data for a new submission.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <returns>The public form access data.</returns>
    public static PublicFormAccessData CreatePublicForm(long formId)
        => new(
            formId.ToString(),
            null,
            ResourcePermissions.Form.Sets.ViewForm,
            ResourcePermissions.Submission.Sets.CreateSubmission);

    /// <summary>
    /// Public form access derived from a minimal form ReBAC JWT; carries tenant for data-list repository scoping.
    /// </summary>
    public static PublicFormAccessData CreatePublicFormForDataListJwt(long formId, long tenantId)
        => new(
            formId.ToString(),
            null,
            ResourcePermissions.Form.Sets.ViewForm,
            ResourcePermissions.Submission.Sets.CreateSubmission,
            formTenantId: tenantId);

    /// <summary>
    /// Same as <see cref="CreatePublicFormForDataListJwt(long,long)"/> using validated frame token claims.
    /// </summary>
    public static PublicFormAccessData CreatePublicFormForDataListJwt(FormAccessTokenClaims claims) =>
        CreatePublicFormForDataListJwt(claims.FormId, claims.TenantId);

    /// <summary>
    /// Creates public form access data for a submission token.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <param name="submissionId">The submission ID.</param>
    /// <returns>The public form access data.</returns>
    public static PublicFormAccessData CreateWithSubmissionToken(long formId, long submissionId)
        => new(
            formId.ToString(),
            submissionId.ToString(),
            ResourcePermissions.Form.Sets.ViewForm,
            ResourcePermissions.Submission.Sets.FillInSubmission);

    public static PublicFormAccessData CreateWithAccessTokenClaims(
        long formId,
        SubmissionAccessTokenClaims claims)
    {
        var submissionPermissions = new HashSet<string>();

        if (claims.Permissions.Contains(SubmissionAccessTokenPermissions.View.Name))
        {
            submissionPermissions.UnionWith(ResourcePermissions.Submission.Sets.ViewOnly);
        }

        if (claims.Permissions.Contains(SubmissionAccessTokenPermissions.Edit.Name))
        {
            submissionPermissions.UnionWith(ResourcePermissions.Submission.Sets.EditSubmission);
        }

        if (claims.Permissions.Contains(SubmissionAccessTokenPermissions.Export.Name))
        {
            submissionPermissions.Add(ResourcePermissions.Submission.Export);
        }

        return new PublicFormAccessData(
            formId.ToString(),
            claims.SubmissionId.ToString(),
            ResourcePermissions.Form.Sets.ViewForm,
            submissionPermissions);
    }
}
