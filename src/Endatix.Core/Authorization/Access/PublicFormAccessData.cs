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
    [JsonConstructor]
    private PublicFormAccessData(
        string formId,
        string? submissionId,
        ImmutableHashSet<string>? formPermissions,
        ImmutableHashSet<string>? submissionPermissions)
    {
        FormId = formId;
        SubmissionId = submissionId;
        FormPermissions = formPermissions ?? EmptyPermissions;
        SubmissionPermissions = submissionPermissions ?? EmptyPermissions;
        Permissions = MergePermissions(FormPermissions, SubmissionPermissions);
    }

    private PublicFormAccessData(
        string formId,
        string? submissionId,
        IEnumerable<string> formPermissions,
        IEnumerable<string> submissionPermissions)
        : this(
            formId,
            submissionId,
            ToImmutableSet(formPermissions),
            ToImmutableSet(submissionPermissions))
    {
    }

    /// <inheritdoc/>
    [JsonIgnore]
    public override ImmutableHashSet<string> Permissions { get; init; }

    /// <summary>
    /// The form ID this access data applies to.
    /// </summary>
    public string FormId { get; init; }

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

    private static ImmutableHashSet<string> MergePermissions(
        ImmutableHashSet<string> formPermissions,
        ImmutableHashSet<string> submissionPermissions)
    {
        if (formPermissions.Count is 0 && submissionPermissions.Count is 0)
        {
            return EmptyPermissions;
        }

        return ToImmutableSet(formPermissions.Union(submissionPermissions));
    }

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
