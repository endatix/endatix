using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.Core.UseCases.Assistant.DefineForm;

namespace Endatix.Api.Endpoints.Assistant;

/// <summary>
/// Endpoint for defining a form using AI assistance.
/// </summary>
public class DefineForm(IMediator _mediator) : Endpoint<DefineFormRequest, Results<Ok<DefineFormResponse>, BadRequest>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Post("assistant/forms/define");
        Permissions(Allow.AllowAll);
        Summary(s =>
        {
            s.Summary = "Define a form using AI assistance";
            s.Description = "Uses AI to generate or refine a form definition based on the provided prompt.";
            s.Responses[200] = "Form definition generated successfully.";
            s.Responses[400] = "Invalid input data.";
        });
    }

    /// <summary>
    /// Executes the HTTP request for defining a form using AI assistance.
    /// </summary>
    /// <param name="request">The request model containing the prompt and optional existing definition.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    public override async Task<Results<Ok<DefineFormResponse>, BadRequest>> ExecuteAsync(DefineFormRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new DefineFormCommand(request.Prompt, request.Definition, request.AssistantId, request.ThreadId),
            cancellationToken);

        return result.ToEndpointResponse<
            Results<Ok<DefineFormResponse>, BadRequest>,
            AssistedDefinitionDto,
            DefineFormResponse>(assistedDefinition => new DefineFormResponse {
                Definition = assistedDefinition.Definition,
                AssistantId = assistedDefinition.AssistantId,
                ThreadId = assistedDefinition.ThreadId
            });
    }
}
