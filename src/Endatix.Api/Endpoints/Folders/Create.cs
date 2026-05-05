using Endatix.Api.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Folders.Create;
using Endatix.Infrastructure.Data.Config;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Folders;

public sealed class Create(IMediator mediator)
    : Endpoint<CreateFolderRequest, Results<Created<FolderModel>, ProblemHttpResult>>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Post("folders");
        Permissions(Actions.Folders.Manage);
        Summary(s =>
        {
            s.Summary = "Create folder";
            s.Description = "Creates a tenant folder for organizing forms.";
            s.Responses[201] = "Folder created successfully.";
            s.Responses[400] = "Invalid input data.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Created<FolderModel>, ProblemHttpResult>> ExecuteAsync(CreateFolderRequest request, CancellationToken ct)
    {
        var command = new CreateFolderCommand(request.Name!, request.Slug, request.Description, request.Metadata, request.Immutable);
        var result = await mediator.Send(command, ct);

        return TypedResultsBuilder
            .MapResult(result, folder => folder.ToModel())
            .SetTypedResults<Created<FolderModel>, ProblemHttpResult>();
    }
}



/// <summary>
/// Validation rules for the <c>CreateFolderRequest</c> class.
/// </summary>
public sealed class CreateFolderValidator : Validator<CreateFolderRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public CreateFolderValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MinimumLength(DataSchemaConstants.MIN_NAME_LENGTH)
            .MaximumLength(DataSchemaConstants.MAX_NAME_LENGTH);

        RuleFor(x => x.Slug)
            .NotEmpty()
            .MaximumLength(DataSchemaConstants.MAX_SLUG_LENGTH);

        RuleFor(x => x.Description)
            .MaximumLength(DataSchemaConstants.MAX_DESCRIPTION_LENGTH)
            .When(x => x.Description != null);

        RuleFor(x => x.Metadata)
            .ValidJsonString()
            .When(x => x.Metadata != null);
    }
}

/// <summary>
/// Request model for creating a folder.
/// </summary>
public sealed class CreateFolderRequest
{
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
    /// Indicates if the folder is immutable.
    /// </summary>
    public bool Immutable { get; set; }
}