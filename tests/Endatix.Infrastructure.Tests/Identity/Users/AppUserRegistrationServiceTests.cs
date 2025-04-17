using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Identity.Users;

public class AppUserRegistrationServiceTests
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IUserStore<AppUser> _userStore;
    private readonly IUserEmailStore<AppUser> _emailStore;
    private readonly AppUserRegistrationService _sut;

    public AppUserRegistrationServiceTests()
    {
        _userStore = Substitute.For<IUserStore<AppUser>, IUserEmailStore<AppUser>>();
        _emailStore = (IUserEmailStore<AppUser>)_userStore;
        _userManager = Substitute.For<UserManager<AppUser>>(_userStore, null, null, null, null, null, null, null, null);
        _sut = new AppUserRegistrationService(_userManager, _userStore);
    }

    [Fact]
    public async Task RegisterUserAsync_EmailStoreNotSupported_ThrowsNotSupportedException()
    {
        // Arrange
        var tenantId = 1L;
        var email = "test@example.com";
        var password = "P@ssw0rd";
        _userManager.SupportsUserEmail.Returns(false);

        // Act
        var act = () => _sut.RegisterUserAsync(email, password, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("Registration logic requires a user store with email support. Please check your email settings");
    }

    [Fact]
    public async Task RegisterUserAsync_UserCreationFails_ReturnsErrorResult()
    {
        // Arrange
        var tenantId = 1L;
        var email = "test@example.com";
        var password = "P@ssw0rd";
        var identityError = new IdentityError { Code = "Error", Description = "Failed to create user" };
        
        _userManager.SupportsUserEmail.Returns(true);
        _userManager.CreateAsync(Arg.Any<AppUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Failed(identityError));

        // Act
        var result = await _sut.RegisterUserAsync(email, password, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain($"Error code: {identityError.Code}. {identityError.Description}");
    }

    [Fact]
    public async Task RegisterUserAsync_ValidRequest_CreatesUserSuccessfully()
    {
        // Arrange
        var tenantId = 1L;
        var userId = 123L;
        var email = "test@example.com";
        var password = "P@ssw0rd";
        var cancellationToken = CancellationToken.None;

        _userManager.SupportsUserEmail.Returns(true);
        _userManager.CreateAsync(Arg.Any<AppUser>(), Arg.Any<string>())
            .Returns(callInfo =>
            {
                var user = callInfo.Arg<AppUser>();
                user.Id = userId;
                return Task.FromResult(IdentityResult.Success);
            });

        _userStore.SetUserNameAsync(Arg.Any<AppUser>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var user = callInfo.Arg<AppUser>();
                var userName = callInfo.Arg<string>();
                user.UserName = userName;
                return Task.CompletedTask;
            });

        _emailStore.SetEmailAsync(Arg.Any<AppUser>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var user = callInfo.Arg<AppUser>();
                var emailArg = callInfo.Arg<string>();
                user.Email = emailArg;
                return Task.CompletedTask;
            });

        // Act
        var result = await _sut.RegisterUserAsync(email, password, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(userId);
        result.Value.TenantId.Should().Be(tenantId);
        result.Value.Email.Should().Be(email);
        result.Value.IsVerified.Should().BeTrue();

        await _userStore.Received(1).SetUserNameAsync(
            Arg.Is<AppUser>(u => u.TenantId == tenantId && u.EmailConfirmed), 
            email, 
            cancellationToken);
        
        await _emailStore.Received(1).SetEmailAsync(
            Arg.Is<AppUser>(u => u.TenantId == tenantId && u.EmailConfirmed), 
            email, 
            cancellationToken);

        _userManager.Received(1).CreateAsync(
            Arg.Is<AppUser>(u => 
                u.Id == userId &&
                u.TenantId == tenantId && 
                u.EmailConfirmed && 
                u.UserName == email && 
                u.Email == email),
            password);
    }
}
