using Endatix.Api.Endpoints.Common;
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
/// Adds a locale to a data list catalog.
/// </summary>
public sealed class AddLocale(IMediator mediator)
    : Endpoint<AddDataListLocaleRequest, Results<Ok<DataListDetailsModel>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Post("data-lists/{dataListId}/locales");
        Permissions(Actions.Forms.Edit);
        Summary(s =>
        {
            s.Summary = "Add a data list locale";
            s.Description = "Adds a locale code to the data list AvailableLocales catalog.";
            s.ExampleRequest = new AddDataListLocaleRequest
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
                AvailableLocales = ["es"],
                Items = []
            };
            s.Responses[200] = "Locale added successfully.";
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
        AddDataListLocaleRequest request,
        CancellationToken ct)
    {
        AddDataListLocaleCommand command = new(request.DataListId, request.Locale!);
        var result = await mediator.Send(command, ct);

        return TypedResultsBuilder
            .MapResult(result, DataListMapper.MapDetails)
            .SetTypedResults<Ok<DataListDetailsModel>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validator for the AddDataListLocaleRequest.
/// </summary>
public sealed class AddDataListLocaleValidator : Validator<AddDataListLocaleRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddDataListLocaleValidator"/> class.
    /// </summary>
    public AddDataListLocaleValidator()
    {
        RuleFor(x => x.DataListId).GreaterThan(0);
        RuleFor(x => x.Locale).IsCultureCode();
    }
}

/// <summary>
/// Request to add a locale to a data list catalog.
/// </summary>
public sealed class AddDataListLocaleRequest
{
    /// <summary>
    /// The ID of the data list.
    /// </summary>
    public long DataListId { get; init; }

    /// <summary>
    /// Locale code to add (for example <c>es</c> or <c>en-US</c>).
    /// </summary>
    public string? Locale { get; init; }
}
