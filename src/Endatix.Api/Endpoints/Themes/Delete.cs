using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.Core.UseCases.Themes.Delete;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Api.Endpoints.Themes;

/// <summary>
/// Endpoint for deleting a theme.
/// </summary>
public class Delete(IMediator mediator) : Endpoint<DeleteRequest, Results<NoContent, BadRequest, NotFound>>
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
            s.Responses[204] = "Theme deleted successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Theme not found.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<NoContent, BadRequest, NotFound>> ExecuteAsync(DeleteRequest request, CancellationToken cancellationToken)
    {
        var command = new DeleteThemeCommand(request.ThemeId);
        var result = await mediator.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            return TypedResults.NoContent();
        }
        else if (result.Status == ResultStatus.NotFound)
        {
            return TypedResults.NotFound();
        }
        else
        {
            return TypedResults.BadRequest();
        }
    }
}