using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Forms.Create;
using Endatix.Infrastructure.Identity.Authorization;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Endpoint for creating a new form and an active form definition.
/// </summary>
public class Create(IMediator mediator) : Endpoint<CreateFormRequest, Results<Created<CreateFormResponse>, BadRequest>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Post("forms");
        Permissions(Actions.Forms.Create);
        Summary(s =>
        {
            s.Summary = "Create a new form";
            s.Description = "Creates a new form and an active form definition for it.";
            s.Responses[201] = "Form created successfully.";
            s.Responses[400] = "Invalid input data.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Created<CreateFormResponse>, BadRequest>> ExecuteAsync(CreateFormRequest request, CancellationToken cancellationToken)
    {
        var createFormCommand = new CreateFormCommand(request.Name!, request.Description, request.IsEnabled!.Value, request.FormDefinitionJsonData!);
        var result = await mediator.Send(createFormCommand, cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, FormMapper.Map<CreateFormResponse>)
            .SetTypedResults<Created<CreateFormResponse>, BadRequest>();
    }
}
