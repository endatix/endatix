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
/// Sets the default locale for a data list.
/// </summary>
public sealed class SetDefaultLocale(IMediator mediator)
    : Endpoint<SetDataListDefaultLocaleRequest, Results<Ok<DataListDetailsModel>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Put("data-lists/{dataListId}/default-locale");
        Permissions(Actions.Forms.Edit);
        Summary(s =>
        {
            s.Summary = "Set the data list default locale";
            s.Description = "Sets which real locale the SurveyJS default label key represents.";
            s.ExampleRequest = new SetDataListDefaultLocaleRequest
            {
                DataListId = 1,
                DefaultLocale = "en"
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
            s.Responses[200] = "Default locale updated successfully.";
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
        SetDataListDefaultLocaleRequest request,
        CancellationToken ct)
    {
        SetDataListDefaultLocaleCommand command = new(request.DataListId, request.DefaultLocale!);
        var result = await mediator.Send(command, ct);

        return TypedResultsBuilder
            .MapResult(result, DataListMapper.MapDetails)
            .SetTypedResults<Ok<DataListDetailsModel>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validator for the SetDataListDefaultLocaleRequest.
/// </summary>
public sealed class SetDataListDefaultLocaleValidator : Validator<SetDataListDefaultLocaleRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SetDataListDefaultLocaleValidator"/> class.
    /// </summary>
    public SetDataListDefaultLocaleValidator()
    {
        RuleFor(x => x.DataListId).GreaterThan(0);
        RuleFor(x => x.DefaultLocale).NotEmpty().MaximumLength(16);
    }
}

/// <summary>
/// Request to set the default locale for a data list.
/// </summary>
public sealed class SetDataListDefaultLocaleRequest
{
    /// <summary>
    /// The ID of the data list.
    /// </summary>
    public long DataListId { get; init; }

    /// <summary>
    /// Real locale represented by the SurveyJS <c>default</c> label key (for example <c>en</c>).
    /// </summary>
    public string? DefaultLocale { get; init; }
}
