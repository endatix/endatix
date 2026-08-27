using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Logging;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.UseCases.Identity.Login;

/// <summary>
/// Handles the login command by validating credentials, persisting session state, and issuing tokens.
/// </summary>
internal sealed class LoginHandler(
    IAuthService authService,
    IUserTokenService tokenService,
    ICurrentUserAuthorizationService authorizationService,
    IMediator mediator,
    ILogger<LoginHandler> logger
    ) : ICommandHandler<LoginCommand, Result<AuthTokensDto>>
{
    /// <summary>
    /// The single client-facing message for every authentication failure.
    /// </summary>
    public const string INVALID_CREDENTIALS_MESSAGE = "The supplied credentials are invalid";

    public async Task<Result<AuthTokensDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await authService.ValidateCredentials(
            request.Email,
            request.Password,
            cancellationToken);

        if (!validationResult.IsSuccess)
        {
            // OWASP (A07 - Identification and Authentication Failures): every credential
            // failure must be indistinguishable to the caller. Unknown account, unconfirmed
            // email and wrong password all collapse to one status and one message, so the
            // response cannot be used to enumerate accounts. IAuthService is a public
            // abstraction - never propagate an implementation's status (e.g. NotFound -> 404)
            // to the client here. The real reason is logged instead.
            logger.LogWarning(
                "Login failed for {Email}. Status: {Status}. Reason: {Reason}",
                SensitiveValue.Email(request.Email),
                validationResult.Status,
                DescribeFailure(validationResult));

            return Result<AuthTokensDto>.Invalid(new ValidationError(INVALID_CREDENTIALS_MESSAGE));
        }

        var user = validationResult.Value;
        var refreshToken = tokenService.IssueRefreshToken();
        var persistResult = await authService.PersistLoginSessionAsync(
            user.Id,
            refreshToken.Token,
            refreshToken.ExpireAt,
            cancellationToken);

        if (!persistResult.IsSuccess)
        {
            // Post-authentication infrastructure failure: it reveals nothing about the
            // account, so the underlying status and message are propagated verbatim.
            return persistResult.ToErrorResult<AuthTokensDto>();
        }

        var accessToken = tokenService.IssueAccessToken(user);
        await authorizationService.InvalidateAuthorizationDataCacheAsync(
            user.Id.ToString(),
            user.TenantId,
            cancellationToken);

        await mediator.Publish(new UserLoggedInEvent(user), cancellationToken);

        return Result.Success(new AuthTokensDto(accessToken, refreshToken));
    }

    private static string DescribeFailure(Result<User> result)
    {
        var messages = result.Errors
            .Concat(result.ValidationErrors.Select(error => error.ErrorMessage))
            .Where(message => !string.IsNullOrWhiteSpace(message));

        var description = string.Join("; ", messages);
        return string.IsNullOrWhiteSpace(description) ? "No reason supplied." : description;
    }
}
