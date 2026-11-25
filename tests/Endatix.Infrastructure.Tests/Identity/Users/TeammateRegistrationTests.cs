using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.InviteUser;
using Endatix.Infrastructure.Identity.Users;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Tests.Identity.Users;

public class TeammateRegistrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Invite_CustomRegistration_IsUsedOnlyWhenStandingIsDormant(bool enforced)
    {
        const string email = "teammate@example.test";
        var ct = TestContext.Current.CancellationToken;
        var registration = Substitute.For<IUserRegistrationService>();
        var authorization = Substitute.For<ICurrentUserAuthorizationService>();
        authorization.ValidateAccessAsync(Actions.Tenant.InviteUsers, ct).Returns(Result.Success());
        registration.RegisterInvitedUserAsync(email, 1, ct).Returns(Result.Success(new User(2, 1, email, email, false)));
        var tenant = Substitute.For<ITenantContext>();
        tenant.TenantId.Returns(1);
        var services = new ServiceCollection()
            .AddSingleton(registration).AddSingleton(authorization).AddSingleton(tenant)
            .AddSingleton(Substitute.For<IRoleManagementService>())
            .AddSingleton(enforced ? new AccountStanding(authorization) : new AccountStanding());
        TeammateRegistration.AddInviteHandler(services);
        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IRequestHandler<InviteUserCommand, Result<User>>>();

        var result = await handler.Handle(new InviteUserCommand(email), ct);

        Assert.Equal(enforced ? ResultStatus.Unavailable : ResultStatus.Ok, result.Status);
        await registration.Received(enforced ? 0 : 1).RegisterInvitedUserAsync(email, 1, ct);
    }
}
