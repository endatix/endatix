using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.FormDefinitions.GetById;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Endpoint for getting a form definition by ID.
/// </summary>
public class GetById(IMediator mediator) : Endpoint<GetFormDefinitionByIdRequest, Results<Ok<FormDefinitionModel>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("forms/{formId}/definitions/{definitionId}");
        Permissions(Actions.Forms.View);
        Summary(s =>
        {
            s.Summary = "Get a form definition by ID";
            s.Description = "Gets a form definition by its ID for a given form.";
            s.ExampleRequest = new GetFormDefinitionByIdRequest { FormId = 1, DefinitionId = 1 };
            s.ResponseExamples[200] = new FormDefinitionModel
            {
                Id = "1",
                FormId = "1",
                IsDraft = false,
                JsonData = "{}",
            };
            s.Responses[200] = "Form definition retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form definition or form not found.";
        });
        Description(builder => builder
            .Produces<FormDefinitionModel>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<FormDefinitionModel>, ProblemHttpResult>> ExecuteAsync(GetFormDefinitionByIdRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new GetFormDefinitionByIdQuery(request.FormId, request.DefinitionId),
            ct);

        return TypedResultsBuilder
            .MapResult(result, FormDefinitionMapper.Map<FormDefinitionModel>)
            .SetTypedResults<Ok<FormDefinitionModel>, ProblemHttpResult>();
    }
}
