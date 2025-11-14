using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Themes.GetById;

namespace Endatix.Api.Endpoints.Themes;

/// <summary>
/// Endpoint for getting a theme by ID.
/// </summary>
public class GetById(IMediator mediator) : Endpoint<GetByIdRequest, Results<Ok<ThemeModel>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("themes/{themeId}");
        Permissions(Actions.Themes.View);
        Summary(s =>
        {
            s.Summary = "Get a theme by ID";
            s.Description = "Gets a theme by its ID.";
            s.Responses[200] = "Theme retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Theme not found.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<ThemeModel>, BadRequest, NotFound>> ExecuteAsync(GetByIdRequest request, CancellationToken cancellationToken)
    {
        var query = new GetThemeByIdQuery(request.ThemeId);
        var result = await mediator.Send(query, cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, ThemeMapper.Map<ThemeModel>)
            .SetTypedResults<Ok<ThemeModel>, BadRequest, NotFound>();
    }
}