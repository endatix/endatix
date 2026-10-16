using FastEndpoints;
using FluentValidation;
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
            s.Responses[503] = "Submissions are being added to the form. Try again in a moment.";
        });
        Description(builder => builder
            .Produces<string>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable));
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

/// <summary>
/// Request model for deleting a form.
/// </summary>
public class DeleteFormRequest
{
    /// <summary>
    /// The ID of the form to delete.
    /// </summary>
    public long FormId { get; init; }
}

/// <summary>
/// Validation rules for the <c>DeleteFormRequest</c> class.
/// </summary>
public class DeleteFormValidator : Validator<DeleteFormRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public DeleteFormValidator()
    {
        RuleFor(x => x.FormId)
            .GreaterThan(0);
    }
}
