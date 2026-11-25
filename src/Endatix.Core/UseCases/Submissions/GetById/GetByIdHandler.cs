using System.Threading;
using System.Threading.Tasks;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Submissions.GetById;

/// <summary>
/// Handler for the <c>GetByIdQuery</c> class
/// </summary>
[DirectCallOnly]
public class GetByIdHandler(IRepository<Submission> repository) : IQueryHandler<GetByIdQuery, Result<Submission>>
{
    private readonly Func<Submission, CancellationToken, Task> _loadTheme = static (_, _) => Task.CompletedTask;
    private ContentQueryScope Selection { get; init; } = ContentQueryScope.Repository;

    /// <summary>Reads a submission with its configured form theme.</summary>
    public GetByIdHandler(IRepository<Submission> repository,
        SubmissionFormTheme formTheme) : this(repository) =>
        _loadTheme = formTheme.LoadAsync;

    public async Task<Result<Submission>> Handle(GetByIdQuery request, CancellationToken cancellationToken)
    {
        var singleSubmissionSpec = Selection.Submission(new SubmissionWithDefinitionAndFormSpec(request.FormId, request.SubmissionId));
        var submission = await repository.SingleOrDefaultAsync(singleSubmissionSpec, cancellationToken);

        if (submission == null)
        {
            return Result.NotFound("Submission not found");
        }

        await _loadTheme(submission, cancellationToken);
        return Result.Success(submission);
    }

    internal sealed class Registered(IRepository<Submission> repository,
    SubmissionFormTheme formTheme, ContentQueryScope scope)
        : RegisteredContentHandler<GetByIdQuery, Result<Submission>>(
            new GetByIdHandler(repository, formTheme) { Selection = scope });
}
