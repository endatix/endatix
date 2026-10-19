using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Submissions.DeleteTests;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Deletes all of a form's test submissions, typically before the form goes live.
/// </summary>
public class DeleteTests(IMediator mediator)
    : Endpoint<DeleteTestSubmissionsRequest, Results<Ok<DeleteTestSubmissionsResponse>, ProblemHttpResult>>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Post("forms/{formId}/submissions/test/delete");
        // The handler also requires submissions.delete: Permissions(...) passes on any one listed permission.
        Permissions(Actions.Forms.Test);
        Summary(s =>
        {
            s.Summary = "Delete a form's test submissions";
            s.Description =
                "Soft-deletes every test submission of the form, a page at a time. Requires forms.test and submissions.delete. " +
                "A failure part way leaves the pages already deleted; calling again finishes the job.";
            s.ExampleRequest = new DeleteTestSubmissionsRequest { FormId = 1 };
            s.ResponseExamples[200] = new DeleteTestSubmissionsResponse(12);
            s.Responses[200] = "The test submissions were deleted; deletedCount says how many.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[403] = "The caller cannot delete submissions.";
            s.Responses[404] = "Form not found.";
        });
        Description(builder => builder
            .Produces<DeleteTestSubmissionsResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<DeleteTestSubmissionsResponse>, ProblemHttpResult>> ExecuteAsync(
        DeleteTestSubmissionsRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteTestSubmissionsCommand(request.FormId), ct);

        return TypedResultsBuilder
            .MapResult(result, deleted => new DeleteTestSubmissionsResponse(deleted.Count))
            .SetTypedResults<Ok<DeleteTestSubmissionsResponse>, ProblemHttpResult>();
    }
}

/// <summary>
/// Request to delete a form's test submissions.
/// </summary>
public sealed class DeleteTestSubmissionsRequest
{
    /// <summary>The form whose test submissions are deleted.</summary>
    public long FormId { get; init; }
}

/// <summary>
/// How many test submissions were deleted.
/// </summary>
/// <param name="DeletedCount">The number of test submissions deleted.</param>
public sealed record DeleteTestSubmissionsResponse(int DeletedCount);

/// <summary>
/// Validates <see cref="DeleteTestSubmissionsRequest"/>.
/// </summary>
public sealed class DeleteTestSubmissionsValidator : Validator<DeleteTestSubmissionsRequest>
{
    /// <summary>Requires a form id.</summary>
    public DeleteTestSubmissionsValidator()
    {
        RuleFor(x => x.FormId).GreaterThan(0);
    }
}
