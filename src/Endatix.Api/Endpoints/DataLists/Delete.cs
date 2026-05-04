using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.DataLists.Delete;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.DataLists;

/// <summary>
/// Endpoint to delete a data list.
/// </summary>
public sealed class Delete(
    IMediator mediator)
    : Endpoint<DeleteDataListRequest, Results<Ok<string>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Delete("data-lists/{dataListId}");
        Permissions(Actions.Forms.Delete);
        Summary(s =>
        {
            s.Summary = "Delete a data list";
            s.Description = "Deletes a data list by its ID.";
            s.Responses[200] = "Data list deleted successfully.";
            s.Responses[400] = "Invalid request or access data.";
            s.Responses[404] = "Data list not found.";
        });
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<string>, ProblemHttpResult>> ExecuteAsync(DeleteDataListRequest request, CancellationToken ct)
    {
        DeleteDataListCommand deleteCommand = new(request.DataListId);
        var result = await mediator.Send(deleteCommand, ct);

        return TypedResultsBuilder
            .MapResult(result, dataList => dataList.Id.ToString())
            .SetTypedResults<Ok<string>, ProblemHttpResult>();
    }
}


/// <summary>
/// Validator for the DeleteDataListRequest.
/// </summary>
public sealed class DeleteDataListValidator : Validator<DeleteDataListRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteDataListValidator"/> class.
    /// </summary>
    public DeleteDataListValidator()
    {
        RuleFor(x => x.DataListId).GreaterThan(0);
    }
}

/// <summary>
/// Request to delete a data list.
/// </summary>
public sealed class DeleteDataListRequest
{
    /// <summary>
    /// The ID of the data list to delete.
    /// </summary>
    public long DataListId { get; init; }
}

