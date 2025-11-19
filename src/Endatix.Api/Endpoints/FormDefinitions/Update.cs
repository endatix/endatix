using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.FormDefinitions.Update;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Endpoint for updating a form definition.
/// </summary>
public class Update(IMediator mediator) : Endpoint<UpdateFormDefinitionRequest, Results<Ok<UpdateFormDefinitionResponse>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Put("forms/{formId}/definitions/{definitionId}");
        Permissions(Actions.Forms.Edit);
        Summary(s =>
        {
            s.Summary = "Update a form definition";
            s.Description = "Updates a form definition for a given form.";
            s.Responses[200] = "Form definition updated successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form definition or form not found.";
        });
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<UpdateFormDefinitionResponse>, BadRequest, NotFound>> ExecuteAsync(UpdateFormDefinitionRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new UpdateFormDefinitionCommand(request.FormId, request.DefinitionId, request.IsDraft!.Value, request.JsonData!),
            cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, FormDefinitionMapper.Map<UpdateFormDefinitionResponse>)
            .SetTypedResults<Ok<UpdateFormDefinitionResponse>, BadRequest, NotFound>();
    }
}
