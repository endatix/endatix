using System.Text.Json.Serialization;
using Endatix.Core.Abstractions.Submissions;

namespace Endatix.Core.Authorization.Access;

/// <summary>
/// Authorization data for public form/submission access control
/// </summary>
public class PublicFormAccessData : AccessDataBase
{
    /// <inheritdoc/>
    [JsonIgnore]
    public override HashSet<string> Permissions
    {
        get => FormPermissions.Union(SubmissionPermissions).ToHashSet();
        // Keep the computed permissions behavior while satisfying the base `init` signature.
        init { }
    }

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
    public HashSet<string> FormPermissions { get; init; } = [];

    /// <summary>
    /// Permissions for the submission resource (or "new" submission when no submissionId provided).
    /// </summary>
    public HashSet<string> SubmissionPermissions { get; init; } = [];


    public static PublicFormAccessData CreatePublicForm(long formId) => new()
    {
        FormId = formId.ToString(),
        SubmissionId = null,
        FormPermissions = [.. ResourcePermissions.Form.Sets.ViewForm],
        SubmissionPermissions = [.. ResourcePermissions.Submission.Sets.CreateSubmission]
    };

    public static PublicFormAccessData CreateWithSubmissionToken(long formId, long submissionId) => new()
    {
        FormId = formId.ToString(),
        SubmissionId = submissionId.ToString(),
        FormPermissions = [.. ResourcePermissions.Form.Sets.ViewForm],
        SubmissionPermissions = [.. ResourcePermissions.Submission.Sets.FillInSubmission]
    };

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

        return new PublicFormAccessData
        {
            FormId = formId.ToString(),
            SubmissionId = claims.SubmissionId.ToString(),
            FormPermissions = [.. ResourcePermissions.Form.Sets.ViewForm],
            SubmissionPermissions = submissionPermissions
        };
    }
}
