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
        ImmutableHashSet<string>? submissionPermissions,
        bool limitOnePerUser = false,
        bool hasUserSubmitted = false,
        bool canStartNewSubmission = true,
        bool isRespondentTestMode = false)
    {
        FormId = formId;
        SubmissionId = submissionId;
        FormPermissions = formPermissions ?? EmptyPermissions;
        SubmissionPermissions = submissionPermissions ?? EmptyPermissions;
        Permissions = MergePermissions(FormPermissions, SubmissionPermissions);
        LimitOnePerUser = limitOnePerUser;
        HasUserSubmitted = hasUserSubmitted;
        CanStartNewSubmission = canStartNewSubmission;
        IsRespondentTestMode = isRespondentTestMode;
    }

    private PublicFormAccessData(
        string formId,
        string? submissionId,
        IEnumerable<string> formPermissions,
        IEnumerable<string> submissionPermissions,
        bool limitOnePerUser = false,
        bool hasUserSubmitted = false,
        bool canStartNewSubmission = true,
        bool isRespondentTestMode = false)
        : this(
            formId,
            submissionId,
            ToImmutableSet(formPermissions),
            ToImmutableSet(submissionPermissions),
            limitOnePerUser,
            hasUserSubmitted,
            canStartNewSubmission,
            isRespondentTestMode)
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

    /// <summary>
    /// Indicates whether the form enforces one response per user.
    /// </summary>
    public bool LimitOnePerUser { get; init; }

    /// <summary>
    /// Indicates whether the current user already has a non-test submission for this form.
    /// </summary>
    public bool HasUserSubmitted { get; init; }

    /// <summary>
    /// Indicates whether the current access context can create a new submission.
    /// </summary>
    public bool CanStartNewSubmission { get; init; }

    /// <summary>
    /// Indicates whether the current respondent is creating a test submission.
    /// </summary>
    public bool IsRespondentTestMode { get; init; }

    private static ImmutableHashSet<string> MergePermissions(
        ImmutableHashSet<string> formPermissions,
        ImmutableHashSet<string> submissionPermissions)
    {
        if (formPermissions.Count is 0 && submissionPermissions.Count is 0)
        {
            return EmptyPermissions;
        }

        return formPermissions.Union(submissionPermissions);
    }

    /// <summary>
    /// Creates public form access data for a new submission.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <param name="limitOnePerUser">Indicates whether the form enforces one response per user.</param>
    /// <param name="hasUserSubmitted">Indicates whether the current user already has a non-test submission for this form.</param>
    /// <param name="isRespondentTestMode">Indicates whether the current respondent is creating a test submission.</param>
    /// <returns>The public form access data.</returns>
    public static PublicFormAccessData CreatePublicForm(
        long formId,
        bool limitOnePerUser = false,
        bool hasUserSubmitted = false,
        bool isRespondentTestMode = false)
        => new(
            formId: formId.ToString(),
            submissionId: null,
            formPermissions: ResourcePermissions.Form.Sets.ViewForm,
            submissionPermissions: ResourcePermissions.Submission.Sets.CreateSubmission,
            limitOnePerUser: limitOnePerUser,
            hasUserSubmitted: hasUserSubmitted,
            canStartNewSubmission: !limitOnePerUser || !hasUserSubmitted,
            isRespondentTestMode: isRespondentTestMode);

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

    /// <summary>
    /// Creates public form access data for a form access token.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <param name="claims">The form access token claims.</param>
    /// <returns>The public form access data.</returns>
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
            _ = submissionPermissions.Add(ResourcePermissions.Submission.Export);
        }

        return new PublicFormAccessData(
            formId.ToString(),
            claims.SubmissionId.ToString(),
            ResourcePermissions.Form.Sets.ViewForm,
            submissionPermissions);
    }
}
