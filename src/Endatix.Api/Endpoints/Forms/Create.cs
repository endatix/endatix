using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.Forms.Create;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Endpoint for creating a new form and an active form definition.
/// </summary>
public class Create(IMediator _mediator) : Endpoint<CreateFormRequest, Results<Created<CreateFormResponse>, BadRequest>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Post("forms");
        Roles("Admin");
        Summary(s =>
        {
            s.Summary = "Create a new form";
            s.Description = "Creates a new form and an active form definition for it.";
            s.Responses[201] = "Form created successfully.";
            s.Responses[400] = "Invalid input data.";
        });
    }

    /// <summary>
    /// Executes the HTTP request for creating a new form.
    /// </summary>
    /// <param name="request">The request model containing form details.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    public override async Task<Results<Created<CreateFormResponse>, BadRequest>> ExecuteAsync(CreateFormRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new CreateFormCommand(request.Name!, request.Description, request.IsEnabled!.Value, request.FormDefinitionJsonData!),
            cancellationToken);
        
        return result.ToEndpointResponse<
            Results<Created<CreateFormResponse>, BadRequest>,
            Form,
            CreateFormResponse>(FormMapper.Map<CreateFormResponse>);
    }    
}
