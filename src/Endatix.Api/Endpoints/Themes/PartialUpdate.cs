using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Models.Themes;
using Endatix.Core.Abstractions.Authorization;
using System.Text.Json;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Themes.PartialUpdate;

namespace Endatix.Api.Endpoints.Themes;

/// <summary>
/// Endpoint for partially updating a theme.
/// </summary>
public class PartialUpdate(IMediator mediator) : Endpoint<PartialUpdateRequest, Results<Ok<PartialUpdateResponse>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Patch("themes/{themeId}");
        Permissions(Actions.Themes.Edit);
        Summary(s =>
        {
            s.Summary = "Partially update a theme";
            s.Description = "Updates specific fields of a theme as provided in the request.";
            s.Responses[200] = "Theme updated successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Theme not found.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<PartialUpdateResponse>, BadRequest, NotFound>> ExecuteAsync(PartialUpdateRequest request, CancellationToken cancellationToken)
    {
        var command = new PartialUpdateThemeCommand(
            request.ThemeId,
            request.Name,
            request.Description,
            request.JsonData);

        var result = await mediator.Send(command, cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, ThemeMapper.Map<PartialUpdateResponse>)
            .SetTypedResults<Ok<PartialUpdateResponse>, BadRequest, NotFound>();
    }
}