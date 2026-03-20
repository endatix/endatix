namespace Endatix.Core.Authorization.Access;

public class SubmissionManagementAccessData : PublicFormAccessData
{
    public static SubmissionManagementAccessData CreateWithViewAccess(long formId, long submissionId)
    {
        return new SubmissionManagementAccessData
        {
            FormId = formId.ToString(),
            SubmissionId = submissionId.ToString(),
            FormPermissions = [.. ResourcePermissions.Form.Sets.ViewForm],
            SubmissionPermissions = [.. ResourcePermissions.Submission.Sets.ViewOnly]
        };
    }

    public static SubmissionManagementAccessData CreateWithEditAccess(long formId, long submissionId)
    {
        return new SubmissionManagementAccessData
        {
            FormId = formId.ToString(),
            SubmissionId = submissionId.ToString(),
            FormPermissions = [.. ResourcePermissions.Form.Sets.ViewForm],
            SubmissionPermissions = [.. ResourcePermissions.Submission.Sets.EditSubmission]
        };
    }
}