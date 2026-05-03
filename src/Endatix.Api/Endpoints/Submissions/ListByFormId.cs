using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Submissions.ListByFormId;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Endpoint for listing submissions by form ID.
/// </summary>
public class ListByFormId(IMediator mediator) : Endpoint<ListByFormIdRequest, Results<Ok<Paged<SubmissionModel>>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("forms/{formId}/submissions");
        Permissions(Actions.Submissions.View);
        Summary(s =>
        {
            s.Summary = "Get a list of Submissions for a given form";
            s.Description = "Returns all submissions for a form given formId. Includes all Form Definitions as well as complete and non-complete responses";
            s.Responses[200] = "List of form Submissions";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found. Pass correct formId";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<Paged<SubmissionModel>>, ProblemHttpResult>> ExecuteAsync(ListByFormIdRequest request, CancellationToken cancellationToken)
    {
        var filterExpressions = request.Filter ?? [];
        var getSubmissionsQuery = new ListByFormIdQuery(request.FormId, request.Page, request.PageSize, filterExpressions);

        var result = await mediator.Send(getSubmissionsQuery, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ToProblem();
        }

        var mapped = result.Value.MapToPaged(SubmissionMapper.MapFromDto);

        return TypedResults.Ok(mapped);
    }
}
