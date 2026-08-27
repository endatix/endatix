using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.FormDefinitions.PartialUpdateActive;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Endpoint for partially updating the active form definition.
/// </summary>
public class PartialUpdateActive(IMediator mediator) : Endpoint<PartialUpdateActiveFormDefinitionRequest, Results<Ok<PartialUpdateActiveFormDefinitionResponse>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Patch("forms/{formId}/definition");
        Permissions(Actions.Forms.Edit);
        Summary(s =>
        {
            s.Summary = "Partially update the active form definition";
            s.Description = "Partially updates the active form definition for a given form.";
            s.ExampleRequest = new PartialUpdateActiveFormDefinitionRequest
            {
                FormId = 1,
                IsDraft = true,
            };
            s.ResponseExamples[200] = new PartialUpdateActiveFormDefinitionResponse
            {
                Id = "1",
                FormId = "1",
                IsDraft = true,
                JsonData = "{}",
            };
            s.Responses[200] = "Active form definition updated successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found.";
        });
        Description(builder => builder
            .Produces<PartialUpdateActiveFormDefinitionResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<PartialUpdateActiveFormDefinitionResponse>, ProblemHttpResult>> ExecuteAsync(PartialUpdateActiveFormDefinitionRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new PartialUpdateActiveFormDefinitionCommand(request.FormId, request.IsDraft, request.JsonData),
            ct);

        return TypedResultsBuilder
            .MapResult(result, FormDefinitionMapper.Map<PartialUpdateActiveFormDefinitionResponse>)
            .SetTypedResults<Ok<PartialUpdateActiveFormDefinitionResponse>, ProblemHttpResult>();
    }
}
