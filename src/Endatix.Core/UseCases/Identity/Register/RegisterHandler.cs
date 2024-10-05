using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Register;

/// <summary>
/// Handles the registration of a new user.
/// </summary>
/// <remarks>
/// This handler is responsible for processing the RegisterCommand and applying domain logic like raising events
/// </remarks>
public class RegisterHandler(IUserRegistrationService userRegistrationService) : ICommandHandler<RegisterCommand, Result<User>>
{
    /// <summary>
    /// Handles the RegisterCommand to register a new user.
    /// </summary>
    /// <param name="request">The RegisterCommand containing the user's email and password.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the work.</param>
    /// <returns>A Result containing the newly registered User if successful, or an error if registration fails.</returns>
    public async Task<Result<User>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var registerResult = await userRegistrationService.RegisterUserAsync(request.Email, request.Password, cancellationToken);

        return registerResult;
    }
}
