using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.DataLists.UpdateDetails;
using Endatix.Infrastructure.Data.Config;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.DataLists;

/// <summary>
/// Endpoint to partially update a data list name and/or description.
/// </summary>
public sealed class PartialUpdate(IMediator mediator)
    : Endpoint<PartialUpdateDataListRequest, Results<Ok<DataListDetailsModel>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Patch("data-lists/{dataListId}");
        Permissions(Actions.Forms.Edit);
        Summary(s =>
        {
            s.Summary = "Partially update a data list";
            s.Description = "Updates the name and/or description of a data list. Omitted fields keep their current value.";
            s.ExampleRequest = new PartialUpdateDataListRequest
            {
                DataListId = 1,
                Name = "Cities",
                Description = "Major cities used in forms"
            };
            s.ResponseExamples[200] = new DataListDetailsModel
            {
                Id = 1,
                Name = "Cities",
                Description = "Major cities used in forms",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ItemsCount = 0,
                DefaultLocale = "en",
                AvailableLocales = [],
                Items = []
            };
            s.Responses[200] = "Data list updated successfully.";
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
        PartialUpdateDataListRequest request,
        CancellationToken ct)
    {
        UpdateDataListDetailsCommand command = new(request.DataListId, request.Name, request.Description);
        var result = await mediator.Send(command, ct);

        return TypedResultsBuilder
            .MapResult(result, DataListMapper.MapDetails)
            .SetTypedResults<Ok<DataListDetailsModel>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validator for the <see cref="PartialUpdateDataListRequest"/>.
/// </summary>
public sealed class PartialUpdateDataListValidator : Validator<PartialUpdateDataListRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PartialUpdateDataListValidator"/> class.
    /// </summary>
    public PartialUpdateDataListValidator()
    {
        RuleFor(x => x.DataListId).GreaterThan(0);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(DataSchemaConstants.MAX_NAME_LENGTH)
            .When(x => x.Name is not null);

        RuleFor(x => x.Description)
            .MaximumLength(DataSchemaConstants.MAX_DESCRIPTION_LENGTH)
            .When(x => x.Description is not null);
    }
}

/// <summary>
/// Request to partially update a data list.
/// </summary>
public sealed class PartialUpdateDataListRequest
{
    /// <summary>
    /// The ID of the data list to update.
    /// </summary>
    public long DataListId { get; init; }

    /// <summary>
    /// When set, replaces the data list name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// When set, replaces the data list description.
    /// </summary>
    public string? Description { get; init; }
}
