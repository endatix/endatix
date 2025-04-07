using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Features.Themes;
using Endatix.Infrastructure.Identity.Authorization;
using System.Text.Json;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Api.Endpoints.Themes;

/// <summary>
/// Endpoint for updating an existing theme.
/// </summary>
public class Update(IThemeService themeService) : Endpoint<UpdateRequest, Results<Ok<UpdateResponse>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Put("themes/{themeId}");
        Permissions(Allow.AllowAll);
        Summary(s =>
        {
            s.Summary = "Update an existing theme";
            s.Description = "Updates an existing theme with the provided data.";
            s.Responses[200] = "Theme updated successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Theme not found.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<UpdateResponse>, BadRequest, NotFound>> ExecuteAsync(UpdateRequest request, CancellationToken cancellationToken)
    {
        ThemeData? themeData = null;
        
        // Parse JsonData if provided
        if (!string.IsNullOrEmpty(request.JsonData))
        {
            try
            {
                themeData = JsonSerializer.Deserialize<ThemeData>(request.JsonData);
            }
            catch (JsonException)
            {
                // Return bad request if JSON is invalid
                var invalidResult = Result<UpdateResponse>.Invalid(new ValidationError("Invalid JSON data provided."));
                return TypedResultsBuilder
                    .FromResult(invalidResult)
                    .SetTypedResults<Ok<UpdateResponse>, BadRequest, NotFound>();
            }
        }
        
        var result = await themeService.UpdateThemeAsync(
            request.ThemeId,
            request.Name,
            request.Description,
            themeData,
            cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, ThemeMapper.Map<UpdateResponse>)
            .SetTypedResults<Ok<UpdateResponse>, BadRequest, NotFound>();
    }
} 