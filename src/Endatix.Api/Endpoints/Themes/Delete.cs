using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Themes.Delete;
using Endatix.Api.Infrastructure;

namespace Endatix.Api.Endpoints.Themes;

/// <summary>
/// Endpoint for deleting a theme.
/// </summary>
public class Delete(IMediator mediator) : Endpoint<DeleteRequest, Results<Ok<string>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Delete("themes/{themeId}");
        Permissions(Actions.Themes.Delete);
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
    public override async Task<Results<Ok<string>, BadRequest, NotFound>> ExecuteAsync(DeleteRequest request, CancellationToken cancellationToken)
    {
        var command = new DeleteThemeCommand(request.ThemeId);
        var result = await mediator.Send(command, cancellationToken);

        return TypedResultsBuilder
            .FromResult(result)
            .SetTypedResults<Ok<string>, BadRequest, NotFound>();
    }
}