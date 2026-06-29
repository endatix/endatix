using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Core.UseCases.Identity.InviteUser;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Users;

/// <summary>
/// Endpoint for inviting a new tenant user. Creates or reattaches a tenant user and sends an activation email when needed.
/// </summary>
public sealed class InviteUser(IMediator mediator, IRoleManagementService roleManagementService)
    : Endpoint<InviteUserRequest, Results<Ok<InviteUserResponse>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Post("users");
        Permissions(Actions.Tenant.InviteUsers);
        Summary(s =>
        {
            s.Summary = "Invite a user";
            s.Description = "Creates or reattaches a tenant user and sends an activation email when needed.";
            s.ExampleRequest = new InviteUserRequest
            {
                Email = "newuser@example.com",
                Roles = ["Editor"]
            };
            s.Responses[200] = "User invited successfully.";
            s.Responses[400] = "Invalid input data.";
        });
        Description(builder => builder
            .Produces<InviteUserResponse>(200, "application/json")
            .ProducesProblem(400));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<InviteUserResponse>, ProblemHttpResult>> ExecuteAsync(
        InviteUserRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(new InviteUserCommand(request.Email!, request.Roles), ct);

        IReadOnlyList<string> roles = [];
        if (result.IsSuccess && result.Value is not null)
        {
            var rolesResult = await roleManagementService.GetUserRolesAsync(result.Value.Id, ct);

            if (!rolesResult.IsSuccess)
            {
                return TypedResults.Problem(
                    detail: "User was invited, but assigned roles could not be loaded.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            roles = [.. rolesResult.Value];
        }

        return TypedResultsBuilder
            .MapResult(result, user => Map(user, roles))
            .SetTypedResults<Ok<InviteUserResponse>, ProblemHttpResult>();
    }

    private static InviteUserResponse Map(User user, IReadOnlyList<string> roles)
        => new()
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            IsVerified = user.IsVerified,
            Roles = roles
        };
}

/// <summary>
/// Request to invite a new tenant user.
/// </summary>
public sealed record InviteUserRequest
{
    /// <summary>
    /// The email address of the user to invite.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// The roles to assign to the user.
    /// </summary>
    public List<string>? Roles { get; init; }
}

/// <summary>
/// Response returned after inviting a user.
/// </summary>
public sealed record InviteUserResponse
{
    /// <summary>
    /// The user identifier.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// The user's display name.
    /// </summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>
    /// The user's email address.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Whether the user's email has been verified.
    /// </summary>
    public bool IsVerified { get; init; }

    /// <summary>
    /// The roles assigned to the user.
    /// </summary>
    public IReadOnlyList<string> Roles { get; init; } = [];
}

/// <summary>
/// Validator for the <see cref="InviteUserRequest"/> class.
/// </summary>
public sealed class InviteUserValidator : Validator<InviteUserRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InviteUserValidator"/> class.
    /// </summary>
    public InviteUserValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        When(request => request.Roles is not null, () =>
        {
            RuleFor(request => request.Roles)
                .Must(roles => roles is null || roles.Count <= 25)
                .WithMessage("A user can be invited with at most 25 roles.");

            RuleForEach(request => request.Roles)
                .NotEmpty()
                .MaximumLength(256);
        });
    }
}
