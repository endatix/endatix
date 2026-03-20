using System.Text.Json.Serialization;

namespace Endatix.Core.Authorization.Access;

public class SubmissionAccessData : AccessDataBase
{
    public string FormId { get; init; } = string.Empty;
    public string SubmissionId { get; init; } = string.Empty;

    [JsonIgnore]
    public override HashSet<string> Permissions { get; init; } = [];

    public HashSet<string> FormPermissions { get; init; } = [];
    public HashSet<string> SubmissionPermissions { get; init; } = [];

    private static SubmissionAccessData Create(
        long formId,
        long submissionId,
        IReadOnlyCollection<string> submissionPermissions)
    {
        var formPermissions = ResourcePermissions.Form.Sets.ViewForm;

        return new SubmissionAccessData
        {
            FormId = formId.ToString(),
            SubmissionId = submissionId.ToString(),
            FormPermissions = [.. formPermissions],
            SubmissionPermissions = [.. submissionPermissions],
            Permissions = [.. formPermissions.Union(submissionPermissions)]
        };
    }

    public static SubmissionAccessData CreateWithViewAccess(long formId, long submissionId)
    {
        return Create(
            formId,
            submissionId,
            ResourcePermissions.Submission.Sets.ViewOnly);
    }

    public static SubmissionAccessData CreateWithEditAccess(long formId, long submissionId)
    {
        return Create(
            formId,
            submissionId,
            ResourcePermissions.Submission.Sets.EditSubmission);
    }
}