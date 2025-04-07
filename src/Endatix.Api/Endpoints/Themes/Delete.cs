using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http;
using Endatix.Api.Infrastructure;
using Endatix.Core.Features.Themes;
using Endatix.Infrastructure.Identity.Authorization;

namespace Endatix.Api.Endpoints.Themes;

/// <summary>
/// Endpoint for deleting a theme.
/// </summary>
public class Delete(IThemeService themeService) : Endpoint<DeleteRequest, Results<Ok<string>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Delete("themes/{themeId}");
        Permissions(Allow.AllowAll);
        Summary(s =>
        {
            s.Summary = "Delete a theme";
            s.Description = "Deletes a theme by its ID.";
            s.Responses[200] = "Theme deleted successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Theme not found.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<string>, BadRequest, NotFound>> ExecuteAsync(DeleteRequest request, CancellationToken cancellationToken)
    {
        var result = await themeService.DeleteThemeAsync(request.ThemeId, cancellationToken);

        if (result.IsSuccess)
        {
            return TypedResults.Ok($"Theme with ID {request.ThemeId} was deleted successfully.");
        }
        else if (result.Status == Endatix.Core.Infrastructure.Result.ResultStatus.NotFound)
        {
            return TypedResults.NotFound();
        }
        else
        {
            return TypedResults.BadRequest();
        }
    }
} 