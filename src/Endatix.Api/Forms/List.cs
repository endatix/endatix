using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.Forms.List;

namespace Endatix.Api.Forms;

/// <summary>
/// Endpoint for listing forms.
/// </summary>
public class List(IMediator _mediator) : Endpoint<FormsListRequest, Results<Ok<IEnumerable<FormModel>>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("forms");
        Roles("Admin");
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
    public override async Task<Results<Ok<IEnumerable<FormModel>>, BadRequest, NotFound>> ExecuteAsync(FormsListRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ListFormsQuery(request.Page, request.PageSize),
            cancellationToken);

        return result.ToEndpointResponse<
            Results<Ok<IEnumerable<FormModel>>, BadRequest, NotFound>,
            IEnumerable<Form>,
            IEnumerable<FormModel>>(FormMapper.Map<FormModel>);
    }
}
