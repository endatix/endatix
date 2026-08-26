using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.UseCases.FormDefinitions.List;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Endpoint for listing form definitions.
/// </summary>
public class List(IMediator mediator) : Endpoint<FormDefinitionsListRequest, Results<Ok<IEnumerable<FormDefinitionModel>>, BadRequest, NotFound>>
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
                "Lists form definitions for a given form with optional pagination, sort, and created/modified date bounds.";
            s.Responses[200] = "Form definitions retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found.";
        });
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<IEnumerable<FormDefinitionModel>>, BadRequest, NotFound>> ExecuteAsync(FormDefinitionsListRequest request, CancellationToken cancellationToken)
    {
        var sort = request.ToSortRequest(FormDefinitionListSortBy.CreatedAt, SortDirection.Desc);
        var result = await mediator.Send(
            new ListFormDefinitionsQuery(
                request.FormId,
                request.Page,
                request.PageSize,
                sort.Field,
                sort.Direction == SortDirection.Desc,
                request.ToCreatedRange(),
                request.ToModifiedRange()),
            cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, FormDefinitionMapper.Map<FormDefinitionModel>)
            .SetTypedResults<Ok<IEnumerable<FormDefinitionModel>>, BadRequest, NotFound>();
    }
}
