using Endatix.Api.Common.FeatureFlags;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.DataLists.Create;
using Endatix.Framework.FeatureFlags;
using Endatix.Infrastructure.Data.Config;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.DataLists;

/// <summary>
/// Endpoint to create a data list.
/// </summary>
public sealed class Create(
    IMediator mediator)
    : Endpoint<CreateDataListRequest, Results<Created<DataListModel>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("data-lists");
        Permissions(Actions.Forms.Create);
        Summary(s =>
        {
            s.Summary = "Create data list";
            s.Description = "Creates a data list for current tenant.";
            s.Responses[201] = "Data list created.";
            s.Responses[400] = "Validation failed.";
            s.Responses[404] = "Feature disabled.";
        });
        FeatureFlag<EndpointFeatureGate>(FeatureFlags.DataLists);
    }

    public override async Task<Results<Created<DataListModel>, ProblemHttpResult>> ExecuteAsync(CreateDataListRequest request, CancellationToken ct)
    {
        CreateDataListCommand command = new(request.Name!, request.Description);

        var result = await mediator.Send(command, ct);
        return TypedResultsBuilder
            .MapResult(result, MapCreatedModel)
            .SetTypedResults<Created<DataListModel>, ProblemHttpResult>();
    }

    private static DataListModel MapCreatedModel(DataList dataList) => new()
    {
        Id = dataList.Id,
        Name = dataList.Name,
        Description = dataList.Description,
        IsActive = dataList.IsActive,
        Items = []
    };
}


/// <summary>
/// Validator for the CreateDataListRequest.
/// </summary>
public sealed class CreateDataListValidator : Validator<CreateDataListRequest>
{
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


