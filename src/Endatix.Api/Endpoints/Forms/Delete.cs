using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Forms.Delete;
using Endatix.Core.Abstractions.Authorization;
using Microsoft.AspNetCore.Http;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Endpoint for deleting a form.
/// </summary>
public class Delete(IMediator mediator) : Endpoint<DeleteFormRequest, Results<Ok<string>, ProblemHttpResult>>
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
            s.ExampleRequest = new DeleteFormRequest { FormId = 1 };
            s.ResponseExamples[200] = "1";
            s.Responses[200] = "Form deleted successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found.";
        });
        Description(builder => builder
            .Produces<string>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<string>, ProblemHttpResult>> ExecuteAsync(DeleteFormRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new DeleteFormCommand(request.FormId),
            ct);

        return TypedResultsBuilder
            .MapResult(result, form => form.Id.ToString())
            .SetTypedResults<Ok<string>, ProblemHttpResult>();
    }
}
