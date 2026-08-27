using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.FormDefinitions.Create;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Endpoint for creating a new form definition.
/// </summary>
public class Create(IMediator mediator) : Endpoint<CreateFormDefinitionRequest, Results<Created<CreateFormDefinitionResponse>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Post("forms/{formId}/definitions");
        Permissions(Actions.Forms.Edit);
        Summary(s =>
        {
            s.Summary = "Create a new form definition";
            s.Description = "Creates a new form definition for a given form.";
            s.ExampleRequest = new CreateFormDefinitionRequest
            {
                FormId = 1,
                IsDraft = true,
                JsonData = "{}",
            };
            s.ResponseExamples[201] = new CreateFormDefinitionResponse
            {
                Id = "1",
                FormId = "1",
                IsDraft = true,
                JsonData = "{}",
            };
            s.Responses[201] = "Form definition created successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found.";
        });
        Description(builder => builder
            .Produces<CreateFormDefinitionResponse>(StatusCodes.Status201Created, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc/>
    public override async Task<Results<Created<CreateFormDefinitionResponse>, ProblemHttpResult>> ExecuteAsync(CreateFormDefinitionRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new CreateFormDefinitionCommand(request.FormId, request.IsDraft!.Value, request.JsonData!),
            ct);

        return TypedResultsBuilder
            .MapResult(result, FormDefinitionMapper.Map<CreateFormDefinitionResponse>)
            .SetTypedResults<Created<CreateFormDefinitionResponse>, ProblemHttpResult>();
    }
}
