using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.UseCases.Forms.List;
using FastEndpoints;
using MediatR;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Forms;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Endpoint for listing forms.
/// </summary>
public class List(IMediator mediator) : Endpoint<FormsListRequest, Results<Ok<IEnumerable<FormDto>>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("forms");
        Permissions(Allow.AllowAll);
        Summary(s =>
        {
            s.Summary = "List forms";
            s.Description = "Lists all forms with optional pagination.";
            s.Responses[200] = "Forms retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
        });
    }

    /// <summary>
    /// Executes the HTTP request for listing forms.
    /// </summary>
    /// <param name="request">The request model containing pagination details.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    public override async Task<Results<Ok<IEnumerable<FormDto>>, BadRequest, NotFound>> ExecuteAsync(FormsListRequest request, CancellationToken cancellationToken)
    {
        var formsQuery = new ListFormsQuery(request.Page, request.PageSize);
        var result = await mediator.Send(formsQuery, cancellationToken);

        return result.ToEndpointResponse<Results<Ok<IEnumerable<FormDto>>, BadRequest, NotFound>, IEnumerable<FormDto>>();
    }
}
