using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.InviteUser;

namespace Endatix.Core.Tests.UseCases.Identity.InviteUser;

public class InviteUserHandlerTests
{
    private readonly IUserRegistrationService _userRegistrationService;
    private readonly IRoleManagementService _roleManagementService;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserAuthorizationService _currentUserAuthorizationService;
    private readonly InviteUserHandler _handler;

    public InviteUserHandlerTests()
    {
        _userRegistrationService = Substitute.For<IUserRegistrationService>();
        _roleManagementService = Substitute.For<IRoleManagementService>();
        _tenantContext = Substitute.For<ITenantContext>();
        _currentUserAuthorizationService = Substitute.For<ICurrentUserAuthorizationService>();
        _currentUserAuthorizationService
            .ValidateAccessAsync(Actions.Tenant.InviteUsers, Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _handler = new InviteUserHandler(
            _userRegistrationService,
            _roleManagementService,
            _tenantContext,
            _currentUserAuthorizationService);
    }

    [Fact]
    public async Task Handle_WhenPlatformAdminRoleRequested_ReturnsInvalid()
    {
        // Arrange
        var command = new InviteUserCommand(
            "new-user@endatix.com",
            [SystemRole.PlatformAdmin.Name]);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsInvalid().Should().BeTrue();
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Contain(SystemRole.PlatformAdmin.Name);

        await _userRegistrationService.DidNotReceive()
            .RegisterInvitedUserAsync(
                Arg.Any<string>(),
                Arg.Any<long>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoRolesRequestedWithoutInviteUsersAccess_ReturnsForbidden()
    {
        // Arrange
        var command = new InviteUserCommand("new-user@endatix.com");
        _currentUserAuthorizationService
            .ValidateAccessAsync(Actions.Tenant.InviteUsers, Arg.Any<CancellationToken>())
            .Returns(Result.Forbidden("Invite users permission is required."));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Forbidden);
        await _roleManagementService.DidNotReceive()
            .GetMissingAssignableRoleNamesAsync(
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<CancellationToken>());
        await _userRegistrationService.DidNotReceive()
            .RegisterInvitedUserAsync(
                Arg.Any<string>(),
                Arg.Any<long>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRolesRequestedWithoutInviteUsersAccess_ReturnsForbidden()
    {
        // Arrange
        var command = new InviteUserCommand("new-user@endatix.com", [SystemRole.Creator.Name]);
        _currentUserAuthorizationService
            .ValidateAccessAsync(Actions.Tenant.InviteUsers, Arg.Any<CancellationToken>())
            .Returns(Result.Forbidden("Invite users permission is required."));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Forbidden);
        await _roleManagementService.DidNotReceive()
            .GetMissingAssignableRoleNamesAsync(
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<CancellationToken>());
        await _userRegistrationService.DidNotReceive()
            .RegisterInvitedUserAsync(
                Arg.Any<string>(),
                Arg.Any<long>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRolesRequestedWithInviteUsersAccess_AssignsRoles()
    {
        // Arrange
        var command = new InviteUserCommand("new-user@endatix.com", [SystemRole.Creator.Name]);
        var user = new User(
            id: 1,
            tenantId: 10,
            userName: "new-user@endatix.com",
            email: "new-user@endatix.com",
            isVerified: false);

        _tenantContext.TenantId.Returns(10);
        _currentUserAuthorizationService
            .ValidateAccessAsync(Actions.Tenant.InviteUsers, Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _roleManagementService
            .GetMissingAssignableRoleNamesAsync(
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<string>>.Success([]));
        _userRegistrationService
            .RegisterInvitedUserAsync(
                command.Email,
                10,
                Arg.Any<CancellationToken>())
            .Returns(Result<User>.Success(user));
        _roleManagementService
            .AssignRoleToUserAsync(user.Id, SystemRole.Creator.Name, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _roleManagementService.Received(1)
            .GetMissingAssignableRoleNamesAsync(
                Arg.Is<IReadOnlyList<string>>(names => names.SequenceEqual(new[] { SystemRole.Creator.Name })),
                Arg.Any<CancellationToken>());
        await _userRegistrationService.Received(1)
            .RegisterInvitedUserAsync(command.Email, 10, Arg.Any<CancellationToken>());
        await _roleManagementService.Received(1)
            .AssignRoleToUserAsync(user.Id, SystemRole.Creator.Name, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRoleAssignmentFails_RollsBackPreviouslyAssignedRoles()
    {
        // Arrange
        const string reviewerRoleName = "Reviewer";
        var command = new InviteUserCommand("new-user@endatix.com", [SystemRole.Creator.Name, reviewerRoleName]);
        var user = new User(
            id: 1,
            tenantId: 10,
            userName: "new-user@endatix.com",
            email: "new-user@endatix.com",
            isVerified: false);

        _tenantContext.TenantId.Returns(10);
        _roleManagementService
            .GetMissingAssignableRoleNamesAsync(
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<string>>.Success([]));
        _userRegistrationService
            .RegisterInvitedUserAsync(
                command.Email,
                10,
                Arg.Any<CancellationToken>())
            .Returns(Result<User>.Success(user));
        _roleManagementService
            .AssignRoleToUserAsync(user.Id, SystemRole.Creator.Name, Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _roleManagementService
            .AssignRoleToUserAsync(user.Id, reviewerRoleName, Arg.Any<CancellationToken>())
            .Returns(Result.Error("Could not assign role."));
        _roleManagementService
            .RemoveRoleFromUserAsync(user.Id, SystemRole.Creator.Name, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Error);
        await _roleManagementService.Received(1)
            .RemoveRoleFromUserAsync(user.Id, SystemRole.Creator.Name, Arg.Any<CancellationToken>());
        await _roleManagementService.DidNotReceive()
            .RemoveRoleFromUserAsync(user.Id, reviewerRoleName, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRequestedRoleIsMissing_ReturnsInvalid()
    {
        var command = new InviteUserCommand("new-user@endatix.com", ["MissingRole"]);
        _tenantContext.TenantId.Returns(10);
        _roleManagementService
            .GetMissingAssignableRoleNamesAsync(
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<string>>.Success(["MissingRole"]));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsInvalid().Should().BeTrue();
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Contain("MissingRole");
        await _userRegistrationService.DidNotReceive()
            .RegisterInvitedUserAsync(
                Arg.Any<string>(),
                Arg.Any<long>(),
                Arg.Any<CancellationToken>());
    }
}
