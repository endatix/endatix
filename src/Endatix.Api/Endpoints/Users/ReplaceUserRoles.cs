using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Identity.ReplaceUserRoles;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Users;

/// <summary>
/// Endpoint for replacing a user's editable tenant role set.
/// </summary>
public sealed class ReplaceUserRoles(IMediator mediator)
    : Endpoint<ReplaceUserRolesRequest, Results<Ok<UserOperation>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Put("users/{userId}/roles");
        Permissions(Actions.Tenant.ManageRoles);
        Summary(s =>
        {
            s.Summary = "Replace user roles";
            s.Description = "Replaces the user's editable tenant role set in one atomic operation.";
            s.Responses[200] = "User roles updated successfully.";
            s.Responses[400] = "Invalid request or role replacement failed.";
            s.Responses[404] = "User not found.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<UserOperation>, ProblemHttpResult>> ExecuteAsync(
        ReplaceUserRolesRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ReplaceUserRolesCommand(request.UserId, request.RoleNames);
        var result = await mediator.Send(command, cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, message => UserOperation.Success(message))
            .SetTypedResults<Ok<UserOperation>, ProblemHttpResult>();
    }
}

/// <summary>
/// Request for replacing a user's editable tenant role set.
/// </summary>
public sealed record ReplaceUserRolesRequest
{
    /// <summary>
    /// The ID of the user whose roles will be replaced.
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// The full replacement set of role names.
    /// </summary>
    public List<string> RoleNames { get; init; } = [];
}

/// <summary>
/// Validator for <see cref="ReplaceUserRolesRequest"/>.
/// </summary>
public sealed class ReplaceUserRolesValidator : Validator<ReplaceUserRolesRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReplaceUserRolesValidator"/> class.
    /// </summary>
    public ReplaceUserRolesValidator()
    {
        RuleFor(request => request.UserId)
            .GreaterThan(0);

        RuleFor(request => request.RoleNames)
            .NotNull();

        RuleForEach(request => request.RoleNames)
            .NotEmpty();
    }
}
