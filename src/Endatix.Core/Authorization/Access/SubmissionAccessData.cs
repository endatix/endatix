using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Endatix.Core.Authorization.Access;

public class SubmissionAccessData : AccessDataBase
{
    public SubmissionAccessData() { }

    private SubmissionAccessData(
        string formId,
        string submissionId,
        IEnumerable<string> formPermissions,
        IEnumerable<string> submissionPermissions)
    {
        FormId = formId;
        SubmissionId = submissionId;

        var normalizedFormPermissions = ToImmutableSet(formPermissions);
        var normalizedSubmissionPermissions = ToImmutableSet(submissionPermissions);

        FormPermissions = normalizedFormPermissions;
        SubmissionPermissions = normalizedSubmissionPermissions;
        Permissions = ToImmutableSet(normalizedFormPermissions.Union(normalizedSubmissionPermissions));
    }

    public string FormId { get; init; } = string.Empty;
    public string SubmissionId { get; init; } = string.Empty;

    [JsonIgnore]
    public override ImmutableHashSet<string> Permissions { get; init; } = EmptyPermissions;

    public ImmutableHashSet<string> FormPermissions { get; init; } = EmptyPermissions;
    public ImmutableHashSet<string> SubmissionPermissions { get; init; } = EmptyPermissions;

    private static SubmissionAccessData Create(
        long formId,
        long submissionId,
        IReadOnlyCollection<string> submissionPermissions)
    {
        return new SubmissionAccessData(
            formId.ToString(),
            submissionId.ToString(),
            ResourcePermissions.Form.Sets.ViewForm,
            submissionPermissions);
    }

    public static SubmissionAccessData CreateWithViewAccess(long formId, long submissionId) => Create(
            formId,
            submissionId,
            ResourcePermissions.Submission.Sets.ViewOnly);

    public static SubmissionAccessData CreateWithEditAccess(long formId, long submissionId) => Create(
            formId,
            submissionId,
            ResourcePermissions.Submission.Sets.EditSubmission);
}