using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.FormDefinitions.GetFields;

namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Endpoint for retrieving all fields across all definitions for a form.
/// </summary>
public class GetFields(IMediator mediator) : Endpoint<GetFieldsRequest, Results<Ok<IEnumerable<DefinitionFieldModel>>, ProblemHttpResult>>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Get("forms/{formId}/definition/fields");
        Permissions(Actions.Forms.View);
        Summary(s =>
        {
            s.Summary = "Get form definition fields";
            s.Description = "Returns the union of all fields across all definitions for a form. " +
                            "Each field includes name, title, and type (SurveyJS question type).";
            s.Responses[200] = "List of definition fields.";
            s.Responses[400] = "Invalid form ID.";
            s.Responses[404] = "Form not found.";
        });
        Description(builder => builder
            .Produces<IEnumerable<DefinitionFieldModel>>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<IEnumerable<DefinitionFieldModel>>, ProblemHttpResult>> ExecuteAsync(
        GetFieldsRequest request,
        CancellationToken ct)
    {
        var query = new GetFormDefinitionFieldsQuery(request.FormId);
        var result = await mediator.Send(query, ct);

        return TypedResultsBuilder
            .MapResult(result, FormDefinitionMapper.Map)
            .SetTypedResults<Ok<IEnumerable<DefinitionFieldModel>>, ProblemHttpResult>();
    }
}
