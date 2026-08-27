using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
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
public class List(IMediator mediator) : Endpoint<ListRequest, Results<Ok<Paged<ThemeModel>>, ProblemHttpResult>>
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
            s.ResponseExamples[200] = new Paged<ThemeModel>(
                page: 1,
                pageSize: 20,
                totalRecords: 1,
                totalPages: 1,
                items:
                [
                    new ThemeModel
                    {
                        Id = "1",
                        Name = "Corporate Blue",
                        Description = "Default corporate theme",
                        JsonData = "{\"primaryColor\":\"#0066cc\"}",
                        CreatedAt = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc),
                        ModifiedAt = new DateTime(2024, 6, 1, 14, 30, 0, DateTimeKind.Utc),
                        FormsCount = 3,
                    },
                ]);
            s.Responses[200] = "Themes retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
        });
        Description(builder => builder
            .Produces<Paged<ThemeModel>>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<Paged<ThemeModel>>, ProblemHttpResult>> ExecuteAsync(
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
            .SetTypedResults<Ok<Paged<ThemeModel>>, ProblemHttpResult>();
    }

    private static Paged<ThemeModel> Map(Paged<Theme> paged) =>
        paged.MapToPaged(ThemeMapper.Map<ThemeModel>);
}
