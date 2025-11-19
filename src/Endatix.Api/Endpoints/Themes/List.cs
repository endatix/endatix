using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Themes.List;

namespace Endatix.Api.Endpoints.Themes;

/// <summary>
/// Endpoint for listing themes.
/// </summary>
public class List(IMediator mediator) : Endpoint<ListRequest, Results<Ok<IEnumerable<ThemeModel>>, BadRequest>>
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
            s.Description = "Lists all themes with optional pagination and filtering.";
            s.Responses[200] = "Themes retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<IEnumerable<ThemeModel>>, BadRequest>> ExecuteAsync(ListRequest request, CancellationToken cancellationToken)
    {
        var query = new ListThemesQuery(request.Page, request.PageSize);
        var result = await mediator.Send(query, cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, ThemeMapper.Map<ThemeModel>)
            .SetTypedResults<Ok<IEnumerable<ThemeModel>>, BadRequest>();
    }
}