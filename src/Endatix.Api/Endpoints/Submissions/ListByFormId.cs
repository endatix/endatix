using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Submissions.ListByFormId;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;

using Endatix.Core.Infrastructure.Paging;

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
            s.Description =
                "Returns submissions for a form given formId. Supports paging, facet filters, " +
                "sortBy/sortDir, and created/modified/started/completed UTC calendar day bounds.";
            s.Responses[200] = "List of form Submissions";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found. Pass correct formId";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<Paged<SubmissionModel>>, ProblemHttpResult>> ExecuteAsync(ListByFormIdRequest request, CancellationToken cancellationToken)
    {
        var sort = request.ToNullableSortRequest(SubmissionListSortBy.CreatedAt, SortDirection.Desc);
        var getSubmissionsQuery = new ListByFormIdQuery(
            request.FormId,
            request.Page,
            request.PageSize,
            request.Filter,
            sort?.Field,
            sort?.Direction == SortDirection.Desc,
            request.ToCreatedRange(),
            request.ToModifiedRange(),
            request.ToStartedRange(),
            request.ToCompletedRange());

        var result = await mediator.Send(getSubmissionsQuery, cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, submissions => submissions.MapToPaged(SubmissionMapper.MapFromDto))
            .SetTypedResults<Ok<Paged<SubmissionModel>>, ProblemHttpResult>();
    }
}
