using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.FormDefinitions.GetActive;
using Errors = Microsoft.AspNetCore.Mvc;

namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Endpoint for getting the active form definition.
/// </summary>
public class GetActive(IMediator mediator) : Endpoint<GetActiveFormDefinitionRequest, Results<Ok<FormDefinitionModel>, BadRequest, NotFound<Errors.ProblemDetails>>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("forms/{formId}/definition");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get the active form definition";
            s.Description = "Gets the active form definition for a given form.";
            s.Responses[200] = "Active form definition retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<FormDefinitionModel>, BadRequest, NotFound<Errors.ProblemDetails>>> ExecuteAsync(GetActiveFormDefinitionRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new GetActiveFormDefinitionQuery(request.FormId),
            cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, FormDefinitionMapper.Map<FormDefinitionModel>)
            .SetTypedResults<Ok<FormDefinitionModel>, BadRequest, NotFound<Errors.ProblemDetails>>();
        ;
    }
}
