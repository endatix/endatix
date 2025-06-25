using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities.Identity;

namespace Endatix.Core.UseCases.Identity.VerifyEmail;

/// <summary>
/// Handles the verification of a user's email address.
/// </summary>
public class VerifyEmailHandler(IEmailVerificationService emailVerificationService) : ICommandHandler<VerifyEmailCommand, Result<User>>
{
    /// <summary>
    /// Handles the VerifyEmailCommand to verify a user's email address.
    /// </summary>
    /// <param name="request">The VerifyEmailCommand containing the verification token.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the work.</param>
    /// <returns>A Result containing the verified User if successful.</returns>
    public async Task<Result<User>> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        return await emailVerificationService.VerifyEmailAsync(request.Token, cancellationToken);
    }
} 