using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Themes.Delete;
using Endatix.Api.Infrastructure;

namespace Endatix.Api.Endpoints.Themes;

/// <summary>
/// Endpoint for deleting a theme.
/// </summary>
public class Delete(IMediator mediator) : Endpoint<DeleteRequest, Results<Ok<string>, ProblemHttpResult>>
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
            s.ExampleRequest = new DeleteRequest { ThemeId = 1 };
            s.ResponseExamples[200] = "1";
            s.Responses[200] = "Theme deleted successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Theme not found.";
        });
        Description(builder => builder
            .Produces<string>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<string>, ProblemHttpResult>> ExecuteAsync(
        DeleteRequest request,
        CancellationToken ct)
    {
        var command = new DeleteThemeCommand(request.ThemeId);
        var result = await mediator.Send(command, ct);

        return TypedResultsBuilder
            .FromResult(result)
            .SetTypedResults<Ok<string>, ProblemHttpResult>();
    }
}
