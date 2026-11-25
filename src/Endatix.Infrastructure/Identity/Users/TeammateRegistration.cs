using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.InviteUser;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Endatix.Infrastructure.Identity.Users;

internal sealed class TeammateRegistration(IUserRegistrationService registration, AccountStanding standing) : IUserRegistrationService
{
    internal static void AddInviteHandler(IServiceCollection services) =>
        services.TryAddScoped<IRequestHandler<InviteUserCommand, Result<User>>>(provider =>
        {
            var configured = provider.GetRequiredService<IUserRegistrationService>();
            var decision = provider.GetRequiredService<AccountStanding>();
            var teammate = decision.IsEnforced ? new TeammateRegistration(configured, decision) : configured;
            return ActivatorUtilities.CreateInstance<InviteUserHandler>(provider, teammate);
        });

    // A replacement service owns its target eligibility; its public registration call offers no pre-write standing check.
    public Task<Result<User>> RegisterInvitedUserAsync(string email, long tenantId, CancellationToken cancellationToken) =>
        registration is AppUserRegistrationService builtIn
            ? builtIn.RegisterTeammateAsync(email, tenantId, standing, cancellationToken)
            : Task.FromResult(Result<User>.Unavailable("Invitation eligibility is unavailable. Try again."));

    public Task<Result<User>> RegisterUserAsync(string email, string password, CancellationToken cancellationToken) =>
        registration.RegisterUserAsync(email, password, cancellationToken);

    public Task<Result<User>> RegisterUserAsync(string email, string password, long tenantId, bool isEmailConfirmed, CancellationToken cancellationToken) =>
        registration.RegisterUserAsync(email, password, tenantId, isEmailConfirmed, cancellationToken);

    public Task<Result<User>> RegisterTenantUserAsync(string email, string password, long tenantId, string roleName, CancellationToken cancellationToken) =>
        registration.RegisterTenantUserAsync(email, password, tenantId, roleName, cancellationToken);
}
