using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Themes.Create;

namespace Endatix.Api.Endpoints.Themes;

/// <summary>
/// Endpoint for creating a new theme.
/// </summary>
public class Create(IMediator mediator) : Endpoint<CreateRequest, Results<Created<CreateResponse>, BadRequest>>
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
            s.Responses[201] = "Theme created successfully.";
            s.Responses[400] = "Invalid input data.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Created<CreateResponse>, BadRequest>> ExecuteAsync(CreateRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateThemeCommand(
            request.Name!,
            request.Description,
            request.JsonData);

        var result = await mediator.Send(command, cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, ThemeMapper.Map<CreateResponse>)
            .SetTypedResults<Created<CreateResponse>, BadRequest>();
    }
}