using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.FormTemplates.Create;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Api.Common;

namespace Endatix.Api.Endpoints.FormTemplates;

/// <summary>
/// Endpoint for creating a new form template.
/// </summary>
public class Create(IMediator mediator) : Endpoint<CreateFormTemplateRequest, Results<Created<CreateFormTemplateResponse>, ProblemHttpResult>>
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
            s.ExampleRequest = new CreateFormTemplateRequest
            {
                Name = "Customer satisfaction",
                Description = "A reusable customer satisfaction survey template.",
                JsonData = "{}",
            };
            s.ResponseExamples[201] = new CreateFormTemplateResponse
            {
                Id = "1",
                Name = "Customer satisfaction",
                JsonData = "{}",
            };
            s.Responses[201] = "Form template created successfully.";
            s.Responses[400] = "Invalid input data.";
        });
        Description(builder => builder
            .Produces<CreateFormTemplateResponse>(StatusCodes.Status201Created, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest));
    }

    /// <inheritdoc/>
    public override async Task<Results<Created<CreateFormTemplateResponse>, ProblemHttpResult>> ExecuteAsync(
        CreateFormTemplateRequest request,
        CancellationToken ct)
    {
        var folderId = request.FolderId.ParseToLong();

        var createCommand = new CreateFormTemplateCommand(request.Name!, request.Description, request.JsonData!, folderId);
        var result = await mediator.Send(createCommand, ct);

        return TypedResultsBuilder
            .MapResult(result, formTemplate => formTemplate.ToFormTemplateModel<CreateFormTemplateResponse>())
            .SetTypedResults<Created<CreateFormTemplateResponse>, ProblemHttpResult>();
    }
}
