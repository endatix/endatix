using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.FormDefinitions.UpdateActive;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Endpoint for updating the active form definition.
/// </summary>
public class UpdateActive(IMediator mediator) : Endpoint<UpdateActiveFormDefinitionRequest, Results<Ok<UpdateActiveFormDefinitionResponse>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Put("forms/{formId}/definition");
        Permissions(Actions.Forms.Edit);
        Summary(s =>
        {
            s.Summary = "Update the active form definition";
            s.Description = "Updates the active form definition for a given form.";
            s.Responses[200] = "Active form definition updated successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found.";
        });
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<UpdateActiveFormDefinitionResponse>, BadRequest, NotFound>> ExecuteAsync(UpdateActiveFormDefinitionRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new UpdateActiveFormDefinitionCommand(request.FormId, request.IsDraft!.Value, request.JsonData!),
            cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, FormDefinitionMapper.Map<UpdateActiveFormDefinitionResponse>)
            .SetTypedResults<Ok<UpdateActiveFormDefinitionResponse>, BadRequest, NotFound>();
    }
}
