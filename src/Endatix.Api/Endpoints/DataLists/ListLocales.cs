using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.DataLists.List;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.DataLists;

/// <summary>
/// Lists distinct culture codes stored on tenant data lists.
/// </summary>
public sealed class ListLocales(IMediator mediator)
    : EndpointWithoutRequest<Results<Ok<IReadOnlyList<string>>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Get("data-lists/locales");
        Permissions(Actions.Forms.View);
        Summary(s =>
        {
            s.Summary = "List distinct data list locales";
            s.Description =
                "Returns distinct culture codes from DefaultLocale and AvailableLocales across all tenant data lists.";
            s.Responses[200] = "Distinct locales retrieved successfully.";
        });
        Description(builder => builder
            .Produces<IReadOnlyList<string>>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<IReadOnlyList<string>>, ProblemHttpResult>> ExecuteAsync(
        CancellationToken ct)
    {
        var result = await mediator.Send(new ListDistinctDataListLocalesQuery(), ct);
        if (!result.IsSuccess)
        {
            return result.ToProblem();
        }

        return TypedResults.Ok(result.Value);
    }
}
