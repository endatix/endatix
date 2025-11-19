using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.FormDefinitions.Create;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Endpoint for creating a new form definition.
/// </summary>
public class Create(IMediator mediator) : Endpoint<CreateFormDefinitionRequest, Results<Created<CreateFormDefinitionResponse>, BadRequest, NotFound>>
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
            s.Responses[201] = "Form definition created successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Created<CreateFormDefinitionResponse>, BadRequest, NotFound>> ExecuteAsync(CreateFormDefinitionRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new CreateFormDefinitionCommand(request.FormId, request.IsDraft!.Value, request.JsonData!),
            cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, FormDefinitionMapper.Map<CreateFormDefinitionResponse>)
            .SetTypedResults<Created<CreateFormDefinitionResponse>, BadRequest, NotFound>();
    }
}
