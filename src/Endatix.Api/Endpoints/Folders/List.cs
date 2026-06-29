using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Folders.List;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Folders;

/// <summary>
/// Endpoint for listing folders.
/// </summary>
public sealed class List(ICurrentUserAuthorizationService authorizationService, IMediator mediator)
    : Endpoint<ListFoldersRequest, Results<Ok<IEnumerable<FolderModel>>, ProblemHttpResult>>
{

    /// <inheritdoc/>
    public override void Configure()
    {
        Get("folders");
        Permissions(Actions.Forms.View);
        Summary(s =>
        {
            s.Summary = "List folders";
            s.Description = "Lists folders for the current tenant. By default only active folders are returned. " +
                            "Use includeInactive=true to include inactive folders (requires folders.manage).";
            s.Responses[200] = "Folders listed successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[403] = "Forbidden. User does not have permission to manage folders.";
            s.Responses[404] = "Not found. The requested resource was not found.";
        });
    }

    public override async Task<Results<Ok<IEnumerable<FolderModel>>, ProblemHttpResult>> ExecuteAsync(
        ListFoldersRequest request,
        CancellationToken ct)
    {
        if (request.IncludeInactive)
        {
            var manageResult = await authorizationService.HasPermissionAsync(Actions.Folders.Manage, ct);
            if (!manageResult.IsSuccess || !manageResult.Value)
            {
                return manageResult.ToProblem();
            }
        }

        var result = await mediator.Send(new ListFoldersQuery(request.IncludeInactive), ct);

        return TypedResultsBuilder
            .MapResult(result, folders => folders.ToModelList())
            .SetTypedResults<Ok<IEnumerable<FolderModel>>, ProblemHttpResult>();
    }
}

/// <summary>
/// Query for listing folders. When <see cref="IncludeInactive"/> is true, callers must have <c>folders.manage</c>.
/// </summary>
public sealed class ListFoldersRequest
{
    /// <summary>
    /// When true, returns active and inactive folders (requires folders.manage). Default lists active folders only.
    /// </summary>
    public bool IncludeInactive { get; set; }
}

