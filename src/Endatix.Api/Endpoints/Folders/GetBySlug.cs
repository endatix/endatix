using Endatix.Api.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Folders.GetBySlug;
using Endatix.Infrastructure.Data.Config;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Folders;

/// <summary>
/// Endpoint for getting a folder by slug.
/// </summary>
public sealed class GetBySlug(IMediator mediator)
    : Endpoint<GetBySlugRequest, Results<Ok<FolderModel>, ProblemHttpResult>>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Get("folders/by-slug/{slug}");
        Permissions(Actions.Forms.View);
        Summary(s =>
        {
            s.Summary = "Get folder by slug";
            s.Description = "Resolves an active folder by URL slug.";
            s.Responses[200] = "Folder found successfully.";
            s.Responses[400] = "Invalid request or access data.";
            s.Responses[404] = "Folder not found.";
        });
    }

    public override async Task<Results<Ok<FolderModel>, ProblemHttpResult>> ExecuteAsync(GetBySlugRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetFolderBySlugQuery(request.Slug), cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, dto => dto.ToModel())
            .SetTypedResults<Ok<FolderModel>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validation rules for the <c>GetBySlugRequest</c> class.
/// </summary>
public sealed class GetBySlugValidator : Validator<GetBySlugRequest>
{
    public GetBySlugValidator()
    {
        RuleFor(x => x.Slug)
            .NotEmpty()
            .ValidUrlSlug();
    }
}

/// <summary>
/// Request model for getting a folder by slug.
/// </summary>
public sealed class GetBySlugRequest
{
    /// <summary>
    /// The slug of the folder.
    /// </summary>
    public string Slug { get; set; } = string.Empty;
}