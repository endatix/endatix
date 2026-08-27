using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Forms.GetById;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Endpoint for getting a form by ID.
/// </summary>
public class GetById(IMediator mediator) : Endpoint<GetFormByIdRequest, Results<Ok<FormModel>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("forms/{formId}");
        Permissions(Actions.Forms.View);
        Summary(s =>
        {
            s.Summary = "Get a form by ID";
            s.Description = "Gets a form by its ID.";
            s.Responses[200] = "Form retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found.";
        });
        Description(builder => builder
            .Produces<FormModel>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<FormModel>, ProblemHttpResult>> ExecuteAsync(GetFormByIdRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new GetFormByIdQuery(request.FormId),
            ct);

        return TypedResultsBuilder
            .MapResult(result, form => form.ToFormModel(includeWebHookSettings: true))
            .SetTypedResults<Ok<FormModel>, ProblemHttpResult>();
    }
}
