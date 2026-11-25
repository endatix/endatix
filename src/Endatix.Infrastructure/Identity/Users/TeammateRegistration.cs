using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.InviteUser;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Endatix.Infrastructure.Identity.Users;

internal sealed class TeammateRegistration(IUserRegistrationService registration) : IUserRegistrationService
{
    internal static void AddInviteHandler(IServiceCollection services) =>
        services.TryAddScoped<IRequestHandler<InviteUserCommand, Result<User>>>(provider =>
        {
            var configured = provider.GetRequiredService<IUserRegistrationService>();
            var teammate = configured is not AppUserRegistrationService && provider.GetRequiredService<AccountStanding>().IsEnforced
                ? new TeammateRegistration(configured)
                : configured;
            return ActivatorUtilities.CreateInstance<InviteUserHandler>(provider, teammate);
        });

    // Enforced hosts currently support only the built-in registration service for teammate invitations.
    public Task<Result<User>> RegisterInvitedUserAsync(string email, long tenantId, CancellationToken cancellationToken) =>
        Task.FromResult(Result<User>.Unavailable("Invitation eligibility is unavailable. Try again."));

    public Task<Result<User>> RegisterUserAsync(string email, string password, CancellationToken cancellationToken) =>
        registration.RegisterUserAsync(email, password, cancellationToken);

    public Task<Result<User>> RegisterUserAsync(string email, string password, long tenantId, bool isEmailConfirmed, CancellationToken cancellationToken) =>
        registration.RegisterUserAsync(email, password, tenantId, isEmailConfirmed, cancellationToken);

    public Task<Result<User>> RegisterTenantUserAsync(string email, string password, long tenantId, string roleName, CancellationToken cancellationToken) =>
        registration.RegisterTenantUserAsync(email, password, tenantId, roleName, cancellationToken);
}
