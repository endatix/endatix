using MediatR;
using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.UseCases.FormTemplates.List;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.FormTemplates;

/// <summary>
/// Endpoint for listing form templates.
/// </summary>
public class List(IMediator mediator) : Endpoint<FormTemplatesListRequest, Results<Ok<IEnumerable<FormTemplateModelWithoutJsonData>>, BadRequest>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("form-templates");
        Permissions(Actions.Templates.View);
        Summary(s =>
        {
            s.Summary = "List form templates";
            s.Description = "Lists all form templates with optional pagination.";
            s.Responses[200] = "Form templates retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<IEnumerable<FormTemplateModelWithoutJsonData>>, BadRequest>> ExecuteAsync(FormTemplatesListRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new ListFormTemplatesQuery(request.Page, request.PageSize),
            cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, formTemplates => formTemplates.ToFormTemplateModelList())
            .SetTypedResults<Ok<IEnumerable<FormTemplateModelWithoutJsonData>>, BadRequest>();
    }
}
