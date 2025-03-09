using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using MediatR;

namespace Endatix.Core.UseCases.Identity.Register;

/// <summary>
/// Handles the registration of a new user.
/// </summary>
/// <remarks>
/// This handler is responsible for processing the RegisterCommand and applying domain logic like raising events
/// </remarks>
public class RegisterHandler(
    IUserRegistrationService userRegistrationService,
    IMediator mediator,
    ITenantContext tenantContext
    ) : ICommandHandler<RegisterCommand, Result<User>>
{
    /// <summary>
    /// Handles the RegisterCommand to register a new user.
    /// </summary>
    /// <param name="request">The RegisterCommand containing the user's email and password.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the work.</param>
    /// <returns>A Result containing the newly registered User if successful, or an error if registration fails.</returns>
    public async Task<Result<User>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(tenantContext.TenantId);
        
        var tenantId = tenantContext.TenantId!.Value;
        var registerResult = await userRegistrationService.RegisterUserAsync(tenantId, request.Email, request.Password, cancellationToken);

        if (registerResult.IsSuccess && registerResult.Value is { } user)
        {
            await mediator.Publish(new UserRegisteredEvent(user), cancellationToken);
        }

        return registerResult;
    }
}
