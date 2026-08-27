using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.FormDefinitions.List;

namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Endpoint for listing form definitions.
/// </summary>
public class List(IMediator mediator) : Endpoint<FormDefinitionsListRequest, Results<Ok<Paged<FormDefinitionModel>>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("forms/{formId}/definitions");
        Permissions(Actions.Forms.View);
        Summary(s =>
        {
            s.Summary = "List form definitions";
            s.Description =
                "Lists form definitions for a given form as a paged envelope (items, page, pageSize, totalRecords, totalPages). Optional sort and created/modified date bounds. Empty form returns 404.";
            s.ExampleRequest = new FormDefinitionsListRequest
            {
                FormId = 1,
                Page = 1,
                PageSize = 20,
                SortBy = FormDefinitionListSortBy.CreatedAt,
                SortDir = SortDirection.Desc,
            };
            s.Responses[200] = "Form definitions retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found.";
        });
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<Paged<FormDefinitionModel>>, BadRequest, NotFound>> ExecuteAsync(
        FormDefinitionsListRequest request,
        CancellationToken ct)
    {
        var sort = request.ToSortRequest(FormDefinitionListSortBy.CreatedAt, SortDirection.Desc);
        var result = await mediator.Send(
            new ListFormDefinitionsQuery(
                request.FormId,
                request.Page,
                request.PageSize,
                sort.Field,
                sort.IsDescending,
                request.ToCreatedRange(),
                request.ToModifiedRange()),
            ct);

        return TypedResultsBuilder
            .MapResult(result, Map)
            .SetTypedResults<Ok<Paged<FormDefinitionModel>>, BadRequest, NotFound>();
    }

    private static Paged<FormDefinitionModel> Map(Paged<FormDefinition> paged) =>
        paged.MapToPaged(FormDefinitionMapper.Map<FormDefinitionModel>);
}
