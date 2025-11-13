using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.FormTemplates.Delete;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.FormTemplates;

/// <summary>
/// Endpoint for deleting a form template.
/// </summary>
public class Delete(IMediator mediator) : Endpoint<DeleteFormTemplateRequest, Results<Ok<string>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Delete("form-templates/{formTemplateId}");
        Permissions(Actions.Templates.Delete);
        Summary(s =>
        {
            s.Summary = "Delete a form template";
            s.Description = "Deletes a form template.";
            s.Responses[204] = "Form template deleted successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form template not found.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<string>, BadRequest, NotFound>> ExecuteAsync(DeleteFormTemplateRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new DeleteFormTemplateCommand(request.FormTemplateId),
            cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, template => template.Id.ToString())
            .SetTypedResults<Ok<string>, BadRequest, NotFound>();
    }
}
