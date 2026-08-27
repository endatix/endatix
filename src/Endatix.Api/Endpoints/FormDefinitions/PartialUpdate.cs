using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.FormDefinitions.PartialUpdate;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Endpoint for partially updating a form definition.
/// </summary>
public class PartialUpdate(IMediator mediator) : Endpoint<PartialUpdateFormDefinitionRequest, Results<Ok<PartialUpdateFormDefinitionResponse>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Patch("forms/{formId}/definitions/{definitionId}");
        Permissions(Actions.Forms.Edit);
        Summary(s =>
        {
            s.Summary = "Partially update a form definition";
            s.Description = "Partially updates a form definition for a given form.";
            s.ExampleRequest = new PartialUpdateFormDefinitionRequest
            {
                FormId = 1,
                DefinitionId = 1,
                IsDraft = true,
            };
            s.ResponseExamples[200] = new PartialUpdateFormDefinitionResponse
            {
                Id = "1",
                FormId = "1",
                IsDraft = true,
                JsonData = "{}",
            };
            s.Responses[200] = "Form definition updated successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form definition or form not found.";
        });
        Description(builder => builder
            .Produces<PartialUpdateFormDefinitionResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<PartialUpdateFormDefinitionResponse>, ProblemHttpResult>> ExecuteAsync(PartialUpdateFormDefinitionRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new PartialUpdateFormDefinitionCommand(request.FormId, request.DefinitionId, request.IsDraft, request.JsonData),
            ct);

        return TypedResultsBuilder
            .MapResult(result, FormDefinitionMapper.Map<PartialUpdateFormDefinitionResponse>)
            .SetTypedResults<Ok<PartialUpdateFormDefinitionResponse>, ProblemHttpResult>();
    }
}
