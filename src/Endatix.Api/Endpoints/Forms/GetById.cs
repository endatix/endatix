using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Forms.GetById;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Endpoint for getting a form by ID.
/// </summary>
public class GetById(IMediator mediator) : Endpoint<GetFormByIdRequest, Results<Ok<FormModel>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("forms/{formId}");
        Permissions(Actions.Forms.View);
        Summary(s =>
        {
            s.Summary = "Get a form by ID";
            s.Description = "Gets a form by its ID.";
            s.Responses[200] = "Form retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<FormModel>, BadRequest, NotFound>> ExecuteAsync(GetFormByIdRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new GetFormByIdQuery(request.FormId),
            cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, FormMapper.Map<FormModel>)
            .SetTypedResults<Ok<FormModel>, BadRequest, NotFound>();
    }
}
