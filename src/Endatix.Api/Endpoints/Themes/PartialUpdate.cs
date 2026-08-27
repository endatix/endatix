using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Themes.PartialUpdate;

namespace Endatix.Api.Endpoints.Themes;

/// <summary>
/// Endpoint for partially updating a theme.
/// </summary>
public class PartialUpdate(IMediator mediator) : Endpoint<PartialUpdateRequest, Results<Ok<PartialUpdateResponse>, ProblemHttpResult>>
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
            s.ExampleRequest = new PartialUpdateRequest
            {
                ThemeId = 1,
                Name = "Corporate Blue",
            };
            s.ResponseExamples[200] = new PartialUpdateResponse
            {
                Id = "1",
                Name = "Corporate Blue",
                Description = "Default corporate theme",
                JsonData = "{\"primaryColor\":\"#0066cc\"}",
                CreatedAt = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc),
                ModifiedAt = new DateTime(2024, 6, 1, 14, 30, 0, DateTimeKind.Utc),
                FormsCount = 3,
            };
            s.Responses[200] = "Theme updated successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Theme not found.";
        });
        Description(builder => builder
            .Produces<PartialUpdateResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<PartialUpdateResponse>, ProblemHttpResult>> ExecuteAsync(
        PartialUpdateRequest request,
        CancellationToken ct)
    {
        var command = new PartialUpdateThemeCommand(
            request.ThemeId,
            request.Name,
            request.Description,
            request.JsonData);

        var result = await mediator.Send(command, ct);

        return TypedResultsBuilder
            .MapResult(result, ThemeMapper.Map<PartialUpdateResponse>)
            .SetTypedResults<Ok<PartialUpdateResponse>, ProblemHttpResult>();
    }
}
