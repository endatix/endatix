using Endatix.Api.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Folders.Update;
using Endatix.Infrastructure.Data.Config;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Folders;

/// <summary>
/// Endpoint for partially updating a folder.
/// </summary>
public sealed class PartialUpdate(IMediator mediator)
    : Endpoint<PartialUpdateFolderRequest, Results<Ok<FolderModel>, ProblemHttpResult>>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Patch("folders/{folderId}");
        Permissions(Actions.Folders.Manage);
        Summary(s =>
        {
            s.Summary = "Partially update folder";
            s.Description = "Partially updates folder fields (name, slug, description, metadata, active, immutable).";
            s.Responses[200] = "Folder updated successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Folder not found.";
            s.Responses[409] = "Folder is locked and cannot be updated.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<FolderModel>, ProblemHttpResult>> ExecuteAsync(PartialUpdateFolderRequest request, CancellationToken ct)
    {
        var command = new UpdateFolderCommand(request.FolderId)
        {
            Name = request.Name,
            Slug = request.Slug,
            Description = request.Description,
            Metadata = request.Metadata,
            IsActive = request.IsActive,
            Immutable = request.Immutable,
        };

        var result = await mediator.Send(command, ct);

        return TypedResultsBuilder
            .MapResult(result, folder => folder.ToModel())
            .SetTypedResults<Ok<FolderModel>, ProblemHttpResult>();
    }
}


/// <summary>
/// Request for updating a folder.
/// </summary>
public sealed class PartialUpdateFolderRequest
{
    /// <summary>
    /// The ID of the folder.
    /// </summary>
    public long FolderId { get; set; }
    /// <summary>
    /// The name of the folder.
    /// </summary>
    public string? Name { get; set; }
    /// <summary>
    /// The slug of the folder.
    /// </summary>
    public string? Slug { get; set; }
    /// <summary>
    /// The description of the folder.
    /// </summary>
    public string? Description { get; set; }
    /// <summary>
    /// The metadata of the folder.
    /// </summary>
    public string? Metadata { get; set; }
    /// <summary>
    /// Whether the folder is active.
    /// </summary>
    public bool? IsActive { get; set; }
    /// <summary>
    /// Whether the folder is immutable.
    /// </summary>
    public bool? Immutable { get; set; }
}


/// <summary>
/// Validator for update folder request.
/// </summary>
public sealed class PartialUpdateFolderValidator : Validator<PartialUpdateFolderRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PartialUpdateFolderValidator"/> class.
    /// </summary>
    public PartialUpdateFolderValidator()
    {
        RuleFor(x => x.FolderId)
            .GreaterThan(0);

        RuleFor(x => x.Name)
            .MinimumLength(DataSchemaConstants.MIN_NAME_LENGTH)
            .MaximumLength(DataSchemaConstants.MAX_NAME_LENGTH)
            .When(x => x.Name is not null);

        RuleFor(x => x.Slug)
            .ValidUrlSlug()
            .When(x => x.Slug != null);

        RuleFor(x => x.Description)
            .MaximumLength(DataSchemaConstants.MAX_DESCRIPTION_LENGTH)
            .When(x => x.Description != null);

        RuleFor(x => x.Metadata)
            .ValidJsonString()
            .When(x => x.Metadata != null);

        RuleFor(x => x)
            .Must(request =>
                request.Name is not null ||
                request.Slug is not null ||
                request.Description is not null ||
                request.Metadata is not null ||
                request.IsActive.HasValue ||
                request.Immutable.HasValue)
            .WithMessage("At least one field must be provided for partial update.");
    }
}