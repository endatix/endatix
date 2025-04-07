using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Features.Themes;
using Endatix.Infrastructure.Identity.Authorization;
using System.Text.Json;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Api.Endpoints.Themes;

/// <summary>
/// Endpoint for creating a new theme.
/// </summary>
public class Create(IThemeService themeService) : Endpoint<CreateRequest, Results<Created<CreateResponse>, BadRequest>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Post("themes");
        Permissions(Allow.AllowAll);
        Summary(s =>
        {
            s.Summary = "Create a new theme";
            s.Description = "Creates a new theme with the provided data.";
            s.Responses[201] = "Theme created successfully.";
            s.Responses[400] = "Invalid input data.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Created<CreateResponse>, BadRequest>> ExecuteAsync(CreateRequest request, CancellationToken cancellationToken)
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
                var invalidResult = Result<CreateResponse>.Invalid(new ValidationError("Invalid JSON data provided."));
                return TypedResultsBuilder
                    .FromResult(invalidResult)
                    .SetTypedResults<Created<CreateResponse>, BadRequest>();
            }
        }

        var result = await themeService.CreateThemeAsync(
            request.Name!,
            request.Description,
            themeData,
            cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, ThemeMapper.Map<CreateResponse>)
            .SetTypedResults<Created<CreateResponse>, BadRequest>();
    }
}