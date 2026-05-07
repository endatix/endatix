using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Folders.Delete;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Folders;

/// <summary>
/// Deletes a folder. Forms and templates in the folder are unassigned (folder id cleared).
/// </summary>
public sealed class Delete(IMediator mediator)
    : Endpoint<DeleteFolderRequest, Results<Ok<string>, ProblemHttpResult>>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Delete("folders/{folderId}");
        Permissions(Actions.Folders.Manage);
        Summary(s =>
        {
            s.Summary = "Delete folder";
            s.Description =
                "Deletes a folder. Forms and form templates assigned to it are updated to have no folder. Child folders become root-level.";
            s.Responses[200] = "Folder deleted.";
            s.Responses[404] = "Folder not found.";
            s.Responses[409] = "Folder is locked and cannot be deleted.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<string>, ProblemHttpResult>> ExecuteAsync(
        DeleteFolderRequest request,
        CancellationToken ct)
    {
        var command = new DeleteFolderCommand(request.FolderId);
        var result = await mediator.Send(command, ct);

        return TypedResultsBuilder
            .MapResult(result, static id => id)
            .SetTypedResults<Ok<string>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validator for delete folder request.
/// </summary>
public sealed class DeleteFolderValidator : Validator<DeleteFolderRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteFolderValidator"/> class.
    /// </summary>
    public DeleteFolderValidator()
    {
        RuleFor(x => x.FolderId).GreaterThan(0);
    }
}

/// <summary>
/// Request to delete a folder.
/// </summary>
public sealed class DeleteFolderRequest
{
    /// <summary>
    /// Folder id from route.
    /// </summary>
    public long FolderId { get; init; }
}
