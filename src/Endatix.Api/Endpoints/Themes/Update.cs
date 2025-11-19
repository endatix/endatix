using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Models.Themes;
using Endatix.Core.Abstractions.Authorization;
using System.Text.Json;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Themes.Update;

namespace Endatix.Api.Endpoints.Themes;

/// <summary>
/// Endpoint for updating a theme.
/// </summary>
public class Update(IMediator mediator) : Endpoint<UpdateRequest, Results<Ok<UpdateResponse>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Put("themes/{themeId}");
        Permissions(Actions.Themes.Edit);
        Summary(s =>
        {
            s.Summary = "Update a theme";
            s.Description = "Updates a theme with the provided data.";
            s.Responses[200] = "Theme updated successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Theme not found.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<UpdateResponse>, BadRequest, NotFound>> ExecuteAsync(UpdateRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateThemeCommand(
            request.ThemeId,
            request.Name!,
            request.Description,
            request.JsonData);

        var result = await mediator.Send(command, cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, ThemeMapper.Map<UpdateResponse>)
            .SetTypedResults<Ok<UpdateResponse>, BadRequest, NotFound>();
    }
} 