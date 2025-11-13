using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.FormTemplates.PartialUpdate;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.FormTemplates;

/// <summary>
/// Endpoint for partially updating a form template.
/// </summary>
public class PartialUpdate(IMediator mediator) : Endpoint<PartialUpdateFormTemplateRequest, Results<Ok<PartialUpdateFormTemplateResponse>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Patch("form-templates/{formTemplateId}");
        Permissions(Actions.Templates.Edit);
        Summary(s =>
        {
            s.Summary = "Partially update a form template";
            s.Description = "Partially updates a form template.";
            s.Responses[200] = "Form template updated successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form template not found.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<PartialUpdateFormTemplateResponse>, BadRequest, NotFound>> ExecuteAsync(PartialUpdateFormTemplateRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new PartialUpdateFormTemplateCommand(request.FormTemplateId, request.Name, request.Description, request.JsonData, request.IsEnabled),
            cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, FormTemplateMapper.Map<PartialUpdateFormTemplateResponse>)
            .SetTypedResults<Ok<PartialUpdateFormTemplateResponse>, BadRequest, NotFound>();
    }
}
