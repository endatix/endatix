using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.FormDefinitions.GetActive;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Endpoint for getting the active form definition.
/// </summary>
public class GetActive(IMediator mediator, IUserContext userContext) : Endpoint<GetActiveFormDefinitionRequest, Results<Ok<FormDefinitionModel>, ProblemHttpResult>>
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
    public override async Task<Results<Ok<FormDefinitionModel>, ProblemHttpResult>> ExecuteAsync(GetActiveFormDefinitionRequest request, CancellationToken cancellationToken)
    {
        var userId = userContext.GetCurrentUserId();

        var result = await mediator.Send(
            new GetActiveFormDefinitionQuery(request.FormId, userId, Actions.Access.Authenticated),
            cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, FormDefinitionMapper.Map<FormDefinitionModel>)
            .SetTypedResults<Ok<FormDefinitionModel>, ProblemHttpResult>();
    }
}
