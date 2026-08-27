using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Themes.Update;

namespace Endatix.Api.Endpoints.Themes;

/// <summary>
/// Endpoint for updating a theme.
/// </summary>
public class Update(IMediator mediator) : Endpoint<UpdateRequest, Results<Ok<UpdateResponse>, ProblemHttpResult>>
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
            s.ExampleRequest = new UpdateRequest
            {
                ThemeId = 1,
                Name = "Corporate Blue",
                Description = "Updated corporate theme",
                JsonData = "{\"primaryColor\":\"#004499\"}",
            };
            s.ResponseExamples[200] = new UpdateResponse
            {
                Id = "1",
                Name = "Corporate Blue",
                Description = "Updated corporate theme",
                JsonData = "{\"primaryColor\":\"#004499\"}",
                CreatedAt = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc),
                ModifiedAt = new DateTime(2024, 6, 1, 14, 30, 0, DateTimeKind.Utc),
                FormsCount = 3,
            };
            s.Responses[200] = "Theme updated successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Theme not found.";
        });
        Description(builder => builder
            .Produces<UpdateResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<UpdateResponse>, ProblemHttpResult>> ExecuteAsync(
        UpdateRequest request,
        CancellationToken ct)
    {
        var command = new UpdateThemeCommand(
            request.ThemeId,
            request.Name!,
            request.Description,
            request.JsonData);

        var result = await mediator.Send(command, ct);

        return TypedResultsBuilder
            .MapResult(result, ThemeMapper.Map<UpdateResponse>)
            .SetTypedResults<Ok<UpdateResponse>, ProblemHttpResult>();
    }
}
