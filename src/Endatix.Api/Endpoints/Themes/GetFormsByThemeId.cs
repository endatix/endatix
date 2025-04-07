using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Features.Themes;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.Api.Endpoints.Forms;

namespace Endatix.Api.Endpoints.Themes;

/// <summary>
/// Endpoint for getting forms using a specific theme.
/// </summary>
public class GetFormsByThemeId(IThemeService themeService) : Endpoint<GetFormsByThemeIdRequest, Results<Ok<IEnumerable<FormModel>>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("themes/{themeId}/forms");
        Permissions(Allow.AllowAll);
        Summary(s =>
        {
            s.Summary = "Get forms by theme ID";
            s.Description = "Gets all forms using a specific theme.";
            s.Responses[200] = "Forms retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Theme not found.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<IEnumerable<FormModel>>, BadRequest, NotFound>> ExecuteAsync(GetFormsByThemeIdRequest request, CancellationToken cancellationToken)
    {
        var result = await themeService.GetFormsByThemeIdAsync(request.ThemeId, cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, forms => forms.ToFormModelList())
            .SetTypedResults<Ok<IEnumerable<FormModel>>, BadRequest, NotFound>();
    }
}