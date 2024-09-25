using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.Forms.GetById;
using Endatix.Infrastructure.Identity.Authorization;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Endpoint for getting a form by ID.
/// </summary>
public class GetById(IMediator _mediator) : Endpoint<GetFormByIdRequest, Results<Ok<FormModel>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("forms/{formId}");
        Permissions(Allow.AllowAll);
        Summary(s =>
        {
            s.Summary = "Get a form by ID";
            s.Description = "Gets a form by its ID.";
            s.Responses[200] = "Form retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found.";
        });
    }

    /// <summary>
    /// Executes the HTTP request for getting a form by ID.
    /// </summary>
    /// <param name="request">The request model containing the form ID.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    public override async Task<Results<Ok<FormModel>, BadRequest, NotFound>> ExecuteAsync(GetFormByIdRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetFormByIdQuery(request.FormId),
            cancellationToken);

        return result.ToEndpointResponse<
            Results<Ok<FormModel>, BadRequest, NotFound>,
            Form,
            FormModel>(FormMapper.Map<FormModel>);
    }
}
