using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Features.Themes;
using Endatix.Infrastructure.Identity.Authorization;

namespace Endatix.Api.Endpoints.Themes;

/// <summary>
/// Endpoint for listing themes.
/// </summary>
public class List(IThemeService themeService) : Endpoint<ListRequest, Results<Ok<IEnumerable<ThemeModelWithoutJsonData>>, BadRequest>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("themes");
        Permissions(Allow.AllowAll);
        Summary(s =>
        {
            s.Summary = "List themes";
            s.Description = "Lists all themes with optional pagination and filtering.";
            s.Responses[200] = "Themes retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<IEnumerable<ThemeModelWithoutJsonData>>, BadRequest>> ExecuteAsync(ListRequest request, CancellationToken cancellationToken)
    {
        var result = await themeService.GetThemesAsync(cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, themes => themes.ToThemeModelList())
            .SetTypedResults<Ok<IEnumerable<ThemeModelWithoutJsonData>>, BadRequest>();
    }
}