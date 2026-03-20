namespace Endatix.Core.Authorization.Access;

public class SubmissionAccessData : PublicFormAccessData
{
    public static SubmissionAccessData CreateWithViewAccess(long formId, long submissionId)
    {
        return new SubmissionAccessData
        {
            FormId = formId.ToString(),
            SubmissionId = submissionId.ToString(),
            FormPermissions = [.. ResourcePermissions.Form.Sets.ViewForm],
            SubmissionPermissions = [.. ResourcePermissions.Submission.Sets.ViewOnly]
        };
    }

    public static SubmissionAccessData CreateWithEditAccess(long formId, long submissionId)
    {
        return new SubmissionAccessData
        {
            FormId = formId.ToString(),
            SubmissionId = submissionId.ToString(),
            FormPermissions = [.. ResourcePermissions.Form.Sets.ViewForm],
            SubmissionPermissions = [.. ResourcePermissions.Submission.Sets.EditSubmission]
        };
    }
}