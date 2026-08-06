using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.DataLists.Locales;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.DataLists;

/// <summary>
/// Removes a locale from a data list catalog.
/// </summary>
public sealed class RemoveLocale(IMediator mediator)
    : Endpoint<RemoveDataListLocaleRequest, Results<Ok<DataListDetailsModel>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Delete("data-lists/{dataListId}/locales/{locale}");
        Permissions(Actions.Forms.Edit);
        Summary(s =>
        {
            s.Summary = "Remove a data list locale";
            s.Description = "Removes a locale from AvailableLocales and strips that key from all item Labels.";
            s.ExampleRequest = new RemoveDataListLocaleRequest
            {
                DataListId = 1,
                Locale = "es"
            };
            s.ResponseExamples[200] = new DataListDetailsModel
            {
                Id = 1,
                Name = "Cities",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ItemsCount = 0,
                DefaultLocale = "en",
                AvailableLocales = [],
                Items = []
            };
            s.Responses[200] = "Locale removed successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Data list not found.";
        });
        Description(builder => builder
            .Produces<DataListDetailsModel>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<DataListDetailsModel>, ProblemHttpResult>> ExecuteAsync(
        RemoveDataListLocaleRequest request,
        CancellationToken ct)
    {
        RemoveDataListLocaleCommand command = new(request.DataListId, request.Locale!);
        var result = await mediator.Send(command, ct);

        return TypedResultsBuilder
            .MapResult(result, DataListMapper.MapDetails)
            .SetTypedResults<Ok<DataListDetailsModel>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validator for the RemoveDataListLocaleRequest.
/// </summary>
public sealed class RemoveDataListLocaleValidator : Validator<RemoveDataListLocaleRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RemoveDataListLocaleValidator"/> class.
    /// </summary>
    public RemoveDataListLocaleValidator()
    {
        RuleFor(x => x.DataListId).GreaterThan(0);
        RuleFor(x => x.Locale).NotEmpty().MaximumLength(16);
    }
}

/// <summary>
/// Request to remove a locale from a data list catalog.
/// </summary>
public sealed class RemoveDataListLocaleRequest
{
    /// <summary>
    /// The ID of the data list.
    /// </summary>
    public long DataListId { get; init; }

    /// <summary>
    /// Locale code to remove.
    /// </summary>
    public string? Locale { get; init; }
}
