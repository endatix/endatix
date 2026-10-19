using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Submissions.DeleteTests;

/// <summary>
/// The endpoint requires <c>forms.test</c>; deleting also needs <see cref="Actions.Submissions.Delete"/>, checked here
/// because an endpoint's permissions pass on any one of them. Each submission is deleted through the aggregate, so
/// every one raises <c>submission.deleted</c> and Reporting drops its flattened row.
/// </summary>
/// <remarks>
/// Each page commits on its own: a failure part way leaves the earlier pages deleted, and a retry carries on, since
/// deleted submissions no longer match.
/// </remarks>
public sealed class DeleteTestSubmissionsHandler(
    IRepository<Submission> submissions,
    IRepository<Form> forms,
    ICurrentUserAuthorizationService authorization) : ICommandHandler<DeleteTestSubmissionsCommand, Result<DeletedTestSubmissions>>
{
    /// <summary>Submissions loaded and saved per round trip, which bounds each query and each save.</summary>
    internal const int PAGE_SIZE = 500;

    public async Task<Result<DeletedTestSubmissions>> Handle(DeleteTestSubmissionsCommand request, CancellationToken cancellationToken)
    {
        var access = await authorization.ValidateAccessAsync(Actions.Submissions.Delete, cancellationToken);
        if (!access.IsSuccess)
        {
            return access;
        }

        if (await forms.GetByIdAsync(request.FormId, cancellationToken) is null)
        {
            return Result.NotFound("Form not found");
        }

        return Result.Success(new DeletedTestSubmissions(await DeleteAllAsync(request.FormId, cancellationToken)));
    }

    private async Task<int> DeleteAllAsync(long formId, CancellationToken cancellationToken)
    {
        var deleted = 0;
        long afterId = 0;
        while (true)
        {
            var page = await submissions.ListAsync(new TestSubmissionsByFormIdPageSpec(formId, afterId, PAGE_SIZE), cancellationToken);
            if (page.Count == 0)
            {
                return deleted;
            }

            foreach (var submission in page)
            {
                submission.Delete();
            }

            await submissions.SaveChangesAsync(cancellationToken);
            deleted += page.Count;
            if (page.Count < PAGE_SIZE)
            {
                return deleted;
            }

            afterId = page[^1].Id;
        }
    }
}
