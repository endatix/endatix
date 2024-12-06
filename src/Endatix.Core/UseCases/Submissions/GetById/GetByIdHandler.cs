using System.Threading;
using System.Threading.Tasks;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Submissions.GetById;

/// <summary>
/// Handler for the <c>GetByIdQuery</c> class
/// </summary>
public class GetByIdHandler(IRepository<Submission> repository) : IQueryHandler<GetByIdQuery, Result<Submission>>
{
    public async Task<Result<Submission>> Handle(GetByIdQuery request, CancellationToken cancellationToken)
    {
        var singleSubmissionSpec = new SubmissionWithDefinitionSpec(request.FormId, request.SubmissionId);
        var submission = await repository.SingleOrDefaultAsync(singleSubmissionSpec, cancellationToken);

        if (submission == null)
        {
            return Result.NotFound("Submission not found");
        }

        return Result.Success(submission);
    }
}
