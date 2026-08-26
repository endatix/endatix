using MediatR;
using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.UseCases.FormTemplates.List;
using Endatix.Api.Infrastructure;
using Endatix.Api.Common;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Paging;

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
            s.Description =
                "Lists all form templates with optional pagination, sort, and created/modified date bounds.";
            s.ExampleRequest = new FormTemplatesListRequest
            {
                Page = 1,
                PageSize = 20,
                SortBy = FormTemplateListSortBy.Name,
                SortDir = SortDirection.Asc,
            };
            s.Responses[200] = "Form templates retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<IEnumerable<FormTemplateModelWithoutJsonData>>, BadRequest>> ExecuteAsync(
        FormTemplatesListRequest request,
        CancellationToken ct)
    {
        var sort = request.ToSortRequest(FormTemplateListSortBy.CreatedAt, SortDirection.Desc);
        var result = await mediator.Send(
            new ListFormTemplatesQuery(
                request.Page,
                request.PageSize,
                request.Filter,
                request.FolderId,
                sort.Field,
                sort.IsDescending,
                request.ToCreatedRange(),
                request.ToModifiedRange()),
            ct);

        return TypedResultsBuilder
            .MapResult(result, formTemplates => formTemplates.ToFormTemplateModelList())
            .SetTypedResults<Ok<IEnumerable<FormTemplateModelWithoutJsonData>>, BadRequest>();
    }
}
