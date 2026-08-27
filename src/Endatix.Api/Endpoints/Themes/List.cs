using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Api.Common;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Themes.List;

namespace Endatix.Api.Endpoints.Themes;

/// <summary>
/// Endpoint for listing themes.
/// </summary>
public class List(IMediator mediator) : Endpoint<ListRequest, Results<Ok<Paged<ThemeModel>>, BadRequest>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("themes");
        Permissions(Actions.Themes.View);
        Summary(s =>
        {
            s.Summary = "List themes";
            s.Description =
                "Lists themes with paging, optional sort, and created/modified date bounds.";
            s.ExampleRequest = new ListRequest
            {
                Page = 1,
                PageSize = 20,
                SortBy = ThemeListSortBy.Name,
                SortDir = SortDirection.Asc,
            };
            s.Responses[200] = "Themes retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<Paged<ThemeModel>>, BadRequest>> ExecuteAsync(
        ListRequest request,
        CancellationToken ct)
    {
        var sort = request.ToSortRequest(ThemeListSortBy.ModifiedAt, SortDirection.Desc);
        var query = new ListThemesQuery(
            request.Page,
            request.PageSize,
            sort.Field,
            sort.IsDescending,
            request.ToCreatedRange(),
            request.ToModifiedRange());
        var result = await mediator.Send(query, ct);

        return TypedResultsBuilder
            .MapResult(result, Map)
            .SetTypedResults<Ok<Paged<ThemeModel>>, BadRequest>();
    }

    private static Paged<ThemeModel> Map(Paged<Theme> paged) =>
        paged.MapToPaged(ThemeMapper.Map<ThemeModel>);
}
