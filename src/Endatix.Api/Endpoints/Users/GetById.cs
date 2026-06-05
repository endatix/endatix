using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Core.UseCases.Identity.GetUserById;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Users;

/// <summary>
/// Endpoint for getting a tenant user by ID.
/// </summary>
public sealed class GetById(IMediator mediator)
    : Endpoint<GetUserByIdRequest, Results<Ok<GetUserByIdResponse>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("users/{userId}");
        Permissions(Actions.Tenant.ViewUsers);
        Summary(s =>
        {
            s.Summary = "Get user by ID";
            s.Description = "Gets a single user for the current tenant, including assigned role names.";
            s.ExampleRequest = new GetUserByIdRequest { UserId = 1507759960832868352L };
            s.Responses[200] = "User retrieved successfully.";
            s.Responses[400] = "Invalid user ID.";
            s.Responses[404] = "User not found.";
        });
        Description(builder => builder
            .Produces<GetUserByIdResponse>(200, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<GetUserByIdResponse>, ProblemHttpResult>> ExecuteAsync(
        GetUserByIdRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetUserByIdQuery(request.UserId), ct);

        return TypedResultsBuilder
            .MapResult(result, Map)
            .SetTypedResults<Ok<GetUserByIdResponse>, ProblemHttpResult>();
    }

    private static GetUserByIdResponse Map(UserWithRoles user)
        => new()
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            IsVerified = user.IsVerified,
            Roles = user.Roles
        };
}

/// <summary>
/// Request for getting a tenant user by ID.
/// </summary>
public sealed record GetUserByIdRequest
{
    /// <summary>
    /// The user ID.
    /// </summary>
    public long UserId { get; init; }
}

/// <summary>
/// Response model for a tenant user.
/// </summary>
public sealed record GetUserByIdResponse
{
    /// <summary>
    /// The user's unique identifier.
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
    /// Indicates whether the user's email is verified.
    /// </summary>
    public bool IsVerified { get; init; }

    /// <summary>
    /// The role names assigned to the user.
    /// </summary>
    public IReadOnlyList<string> Roles { get; init; } = [];
}

/// <summary>
/// Validator for the <see cref="GetUserByIdRequest"/>.
/// </summary>
public sealed class GetUserByIdValidator : Validator<GetUserByIdRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetUserByIdValidator"/> class.
    /// </summary>
    public GetUserByIdValidator()
    {
        RuleFor(request => request.UserId)
            .GreaterThan(0)
            .WithMessage("User ID must be greater than zero.");
    }
}
