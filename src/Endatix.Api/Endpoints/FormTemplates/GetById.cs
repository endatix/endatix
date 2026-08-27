using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.FormTemplates.GetById;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.FormTemplates;

/// <summary>
/// Endpoint for getting a form template by ID. 
/// </summary>
public class GetById(IMediator mediator) : Endpoint<GetFormTemplateByIdRequest, Results<Ok<FormTemplateModel>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("form-templates/{formTemplateId}");
        Permissions(Actions.Templates.View);
        Summary(s =>
        {
            s.Summary = "Get a form template by ID";
            s.Description = "Gets a form template by its ID.";
            s.ExampleRequest = new GetFormTemplateByIdRequest { FormTemplateId = 1 };
            s.ResponseExamples[200] = new FormTemplateModel
            {
                Id = "1",
                Name = "Customer satisfaction",
                JsonData = "{}",
            };
            s.Responses[200] = "Form template retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form template not found.";
        });
        Description(builder => builder
            .Produces<FormTemplateModel>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<FormTemplateModel>, ProblemHttpResult>> ExecuteAsync(
        GetFormTemplateByIdRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new GetFormTemplateByIdQuery(request.FormTemplateId),
            ct);

        return TypedResultsBuilder
            .MapResult(result, formTemplate => formTemplate.ToFormTemplateModel<FormTemplateModel>())
            .SetTypedResults<Ok<FormTemplateModel>, ProblemHttpResult>();
    }
}
