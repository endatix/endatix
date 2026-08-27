using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.FormDefinitions.UpdateActive;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Endpoint for updating the active form definition.
/// </summary>
public class UpdateActive(IMediator mediator) : Endpoint<UpdateActiveFormDefinitionRequest, Results<Ok<UpdateActiveFormDefinitionResponse>, ProblemHttpResult>>
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
            s.ExampleRequest = new UpdateActiveFormDefinitionRequest
            {
                FormId = 1,
                IsDraft = false,
                JsonData = "{}",
            };
            s.ResponseExamples[200] = new UpdateActiveFormDefinitionResponse
            {
                Id = "1",
                FormId = "1",
                IsDraft = false,
                JsonData = "{}",
            };
            s.Responses[200] = "Active form definition updated successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found.";
        });
        Description(builder => builder
            .Produces<UpdateActiveFormDefinitionResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<UpdateActiveFormDefinitionResponse>, ProblemHttpResult>> ExecuteAsync(UpdateActiveFormDefinitionRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new UpdateActiveFormDefinitionCommand(request.FormId, request.IsDraft!.Value, request.JsonData!),
            ct);

        return TypedResultsBuilder
            .MapResult(result, FormDefinitionMapper.Map<UpdateActiveFormDefinitionResponse>)
            .SetTypedResults<Ok<UpdateActiveFormDefinitionResponse>, ProblemHttpResult>();
    }
}
