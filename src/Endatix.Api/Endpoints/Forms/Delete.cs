using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Forms.Delete;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Endpoint for deleting a form.
/// </summary>
public class Delete(IMediator mediator) : Endpoint<DeleteFormRequest, Results<Ok<string>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Delete("forms/{formId}");
        Permissions(Actions.Forms.Delete);
        Summary(s =>
        {
            s.Summary = "Delete a form";
            s.Description = "Deletes a form and all its definitions and submissions.";
            s.Responses[204] = "Form deleted successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<string>, BadRequest, NotFound>> ExecuteAsync(DeleteFormRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new DeleteFormCommand(request.FormId),
            cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, form => form.Id.ToString())
            .SetTypedResults<Ok<string>, BadRequest, NotFound>();
    }
}
