using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.FormTemplates.Create;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.FormTemplates;

/// <summary>
/// Endpoint for creating a new form template.
/// </summary>
public class Create(IMediator mediator) : Endpoint<CreateFormTemplateRequest, Results<Created<CreateFormTemplateResponse>, BadRequest>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Post("form-templates");
        Permissions(Actions.Templates.Create);
        Summary(s =>
        {
            s.Summary = "Create a new form template";
            s.Description = "Creates a new form template.";
            s.Responses[201] = "Form template created successfully.";
            s.Responses[400] = "Invalid input data.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Created<CreateFormTemplateResponse>, BadRequest>> ExecuteAsync(CreateFormTemplateRequest request, CancellationToken cancellationToken)
    {
        var createCommand = new CreateFormTemplateCommand(request.Name!, request.Description, request.JsonData!, request.IsEnabled!.Value!);
        var result = await mediator.Send(createCommand, cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, FormTemplateMapper.Map<CreateFormTemplateResponse>)
            .SetTypedResults<Created<CreateFormTemplateResponse>, BadRequest>();
    }
}
