using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Submissions.GetFiles;

internal sealed class SubmissionFileTargets(IRepository<Submission> submissions, IFormsRepository forms, ContentQueryScope scope)
{
    internal async Task<(Submission? Submission, Form? Form)> ReadAsync(GetFilesQuery request, CancellationToken ct)
    {
        var spec = scope.Submission(new SubmissionWithDefinitionSpec(request.FormId, request.SubmissionId));
        var submission = await submissions.SingleOrDefaultAsync(spec, ct);
        var form = await scope.FindAsync(forms, request.FormId, ct);
        return (submission, form);
    }
}
