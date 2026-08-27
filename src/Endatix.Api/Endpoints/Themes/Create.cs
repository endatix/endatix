using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Themes.Create;

namespace Endatix.Api.Endpoints.Themes;

/// <summary>
/// Endpoint for creating a new theme.
/// </summary>
public class Create(IMediator mediator) : Endpoint<CreateRequest, Results<Created<CreateResponse>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Post("themes");
        Permissions(Actions.Themes.Create);
        Summary(s =>
        {
            s.Summary = "Create a new theme";
            s.Description = "Creates a new theme with the provided data.";
            s.ExampleRequest = new CreateRequest
            {
                Name = "Corporate Blue",
                Description = "Default corporate theme",
                JsonData = "{\"primaryColor\":\"#0066cc\"}",
            };
            s.ResponseExamples[201] = new CreateResponse
            {
                Id = "1",
                Name = "Corporate Blue",
                Description = "Default corporate theme",
                JsonData = "{\"primaryColor\":\"#0066cc\"}",
                CreatedAt = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc),
                FormsCount = 0,
            };
            s.Responses[201] = "Theme created successfully.";
            s.Responses[400] = "Invalid input data.";
        });
        Description(builder => builder
            .Produces<CreateResponse>(StatusCodes.Status201Created, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest));
    }

    /// <inheritdoc/>
    public override async Task<Results<Created<CreateResponse>, ProblemHttpResult>> ExecuteAsync(
        CreateRequest request,
        CancellationToken ct)
    {
        var command = new CreateThemeCommand(
            request.Name!,
            request.Description,
            request.JsonData);

        var result = await mediator.Send(command, ct);

        return TypedResultsBuilder
            .MapResult(result, ThemeMapper.Map<CreateResponse>)
            .SetTypedResults<Created<CreateResponse>, ProblemHttpResult>();
    }
}
