using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.FormDefinitions.Update;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Endpoint for updating a form definition.
/// </summary>
public class Update(IMediator mediator) : Endpoint<UpdateFormDefinitionRequest, Results<Ok<UpdateFormDefinitionResponse>, ProblemHttpResult>>
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
            s.ExampleRequest = new UpdateFormDefinitionRequest
            {
                FormId = 1,
                DefinitionId = 1,
                IsDraft = false,
                JsonData = "{}",
            };
            s.ResponseExamples[200] = new UpdateFormDefinitionResponse
            {
                Id = "1",
                FormId = "1",
                IsDraft = false,
                JsonData = "{}",
            };
            s.Responses[200] = "Form definition updated successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form definition or form not found.";
        });
        Description(builder => builder
            .Produces<UpdateFormDefinitionResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<UpdateFormDefinitionResponse>, ProblemHttpResult>> ExecuteAsync(UpdateFormDefinitionRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new UpdateFormDefinitionCommand(request.FormId, request.DefinitionId, request.IsDraft!.Value, request.JsonData!),
            ct);

        return TypedResultsBuilder
            .MapResult(result, FormDefinitionMapper.Map<UpdateFormDefinitionResponse>)
            .SetTypedResults<Ok<UpdateFormDefinitionResponse>, ProblemHttpResult>();
    }
}
