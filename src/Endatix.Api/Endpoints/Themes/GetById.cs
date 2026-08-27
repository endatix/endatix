using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Themes.GetById;

namespace Endatix.Api.Endpoints.Themes;

/// <summary>
/// Endpoint for getting a theme by ID.
/// </summary>
public class GetById(IMediator mediator) : Endpoint<GetByIdRequest, Results<Ok<ThemeModel>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("themes/{themeId}");
        Permissions(Actions.Themes.View);
        Summary(s =>
        {
            s.Summary = "Get a theme by ID";
            s.Description = "Gets a theme by its ID.";
            s.ExampleRequest = new GetByIdRequest { ThemeId = 1 };
            s.ResponseExamples[200] = new ThemeModel
            {
                Id = "1",
                Name = "Corporate Blue",
                Description = "Default corporate theme",
                JsonData = "{\"primaryColor\":\"#0066cc\"}",
                CreatedAt = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc),
                ModifiedAt = new DateTime(2024, 6, 1, 14, 30, 0, DateTimeKind.Utc),
                FormsCount = 3,
            };
            s.Responses[200] = "Theme retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Theme not found.";
        });
        Description(builder => builder
            .Produces<ThemeModel>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<ThemeModel>, ProblemHttpResult>> ExecuteAsync(
        GetByIdRequest request,
        CancellationToken ct)
    {
        var query = new GetThemeByIdQuery(request.ThemeId);
        var result = await mediator.Send(query, ct);

        return TypedResultsBuilder
            .MapResult(result, ThemeMapper.Map<ThemeModel>)
            .SetTypedResults<Ok<ThemeModel>, ProblemHttpResult>();
    }
}
