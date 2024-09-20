using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.Submissions.ListByFormId;
using Endatix.Infrastructure.Identity.Authorization;


namespace Endatix.Api.Endpoints.Submissions;

public class ListByFormId(IMediator _mediator) : Endpoint<ListByFormIdRequest, Results<Ok<IEnumerable<SubmissionModel>>, BadRequest, NotFound>>
{
    public override void Configure()
    {
        Get("forms/{formId}/submissions");
        Permissions(Allow.AllowAll);
        Summary(s =>
        {
            s.Summary = "Get a list of Submissions for a given form";
            s.Description = "Returns all submissions for a form given formId. Includes all Form Definitions as well as complete and non-complete responses";
            s.Responses[200] = "List of form Submissions";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found. Pass correct formId";
        });
    }

    public override async Task<Results<Ok<IEnumerable<SubmissionModel>>, BadRequest, NotFound>> ExecuteAsync(ListByFormIdRequest request, CancellationToken cancellationToken)
    {
        var getSubmissionsQuery = new ListByFormIdQuery(request.FormId, request.Page, request.PageSize);

        var result = await _mediator.Send(getSubmissionsQuery, cancellationToken);

        return result.ToEndpointResponse<
            Results<Ok<IEnumerable<SubmissionModel>>, BadRequest, NotFound>,
            IEnumerable<Submission>,
            IEnumerable<SubmissionModel>>(SubmissionMapper.Map<SubmissionModel>);
    }
}
