using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.DataLists.Create;
using Endatix.Infrastructure.Data.Config;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.DataLists;

/// <summary>
/// Endpoint to create a data list.
/// </summary>
public sealed class Create(
    IMediator mediator)
    : Endpoint<CreateDataListRequest, Results<Created<DataListDetailsModel>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Post("data-lists");
        Permissions(Actions.Forms.Create);
        Summary(s =>
        {
            s.Summary = "Create a data list";
            s.Description = "Creates a data list for the current tenant.";
            s.ExampleRequest = new CreateDataListRequest
            {
                Name = "Cities",
                Description = "Major cities used in forms"
            };
            s.ResponseExamples[201] = new DataListDetailsModel
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
            s.Responses[201] = "Data list created successfully.";
            s.Responses[400] = "Invalid input data.";
        });
        Description(builder => builder
            .Produces<DataListDetailsModel>(StatusCodes.Status201Created, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest));
    }

    /// <inheritdoc />
    public override async Task<Results<Created<DataListDetailsModel>, ProblemHttpResult>> ExecuteAsync(CreateDataListRequest request, CancellationToken ct)
    {
        CreateDataListCommand command = new(request.Name!, request.Description);

        var result = await mediator.Send(command, ct);
        return TypedResultsBuilder
            .MapResult(result, MapCreatedModel)
            .SetTypedResults<Created<DataListDetailsModel>, ProblemHttpResult>();
    }

    private static DataListDetailsModel MapCreatedModel(DataList dataList) => new()
    {
        Id = dataList.Id,
        Name = dataList.Name,
        Description = dataList.Description,
        IsActive = dataList.IsActive,
        CreatedAt = dataList.CreatedAt,
        ModifiedAt = dataList.ModifiedAt,
        ItemsCount = 0,
        DefaultLocale = dataList.DefaultLocale,
        AvailableLocales = dataList.AvailableLocales,
        Items = []
    };
}


/// <summary>
/// Validator for the CreateDataListRequest.
/// </summary>
public sealed class CreateDataListValidator : Validator<CreateDataListRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateDataListValidator"/> class.
    /// </summary>
    public CreateDataListValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(DataSchemaConstants.MAX_NAME_LENGTH);

        RuleFor(x => x.Description)
            .MaximumLength(DataSchemaConstants.MAX_DESCRIPTION_LENGTH)
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}

/// <summary>
/// Request to create a data list.
/// </summary>
public sealed class CreateDataListRequest
{
    /// <summary>
    /// The name of the data list.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The description of the data list.
    /// </summary>
    public string? Description { get; init; }
}
