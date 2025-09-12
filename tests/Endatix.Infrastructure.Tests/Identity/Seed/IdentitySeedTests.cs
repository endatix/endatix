using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Tests;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Seed;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Identity.Seed;

public class IdentitySeedTests
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IUserRegistrationService _userRegistrationService;
    private readonly DataOptions _dataOptions;
    private readonly ILogger _logger;

    public IdentitySeedTests()
    {
        _userManager = Substitute.For<UserManager<AppUser>>(
            Substitute.For<IUserStore<AppUser>>(),
            null, null, null, null, null, null, null, null);
        _userRegistrationService = Substitute.For<IUserRegistrationService>();
        _dataOptions = new DataOptions();
        _logger = Substitute.For<ILogger>();
    }

    [Fact]
    public async Task SeedInitialUser_NullUserManager_ThrowsArgumentNullException()
    {
        // Arrange
        UserManager<AppUser> userManager = null!;

        // Act
        var act = () => IdentitySeed.SeedInitialUser(userManager, _userRegistrationService, _dataOptions, _logger);

        // Assert
        var expectedMessage = ErrorMessages.GetErrorMessage(nameof(userManager), ErrorType.Null);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithMessage(expectedMessage);
    }

    [Fact]
    public async Task SeedInitialUser_NullUserRegistrationService_ThrowsArgumentNullException()
    {
        // Arrange
        IUserRegistrationService userRegistrationService = null!;

        // Act
        var act = () => IdentitySeed.SeedInitialUser(_userManager, userRegistrationService, _dataOptions, _logger);

        // Assert
        var expectedMessage = ErrorMessages.GetErrorMessage(nameof(userRegistrationService), ErrorType.Null);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithMessage(expectedMessage);
    }

    [Fact]
    public async Task SeedInitialUser_UsersExist_DoesNotCreateUser()
    {
        // Arrange
        var users = new List<AppUser> { new AppUser() }.AsQueryable();
        _userManager.Users.Returns(users);

        // Act
        await IdentitySeed.SeedInitialUser(_userManager, _userRegistrationService, _dataOptions, _logger);

        // Assert
        await _userRegistrationService.DidNotReceive()
            .RegisterUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedInitialUser_NoUsersAndNoDataOptions_CreatesDefaultUser()
    {
        // Arrange
        var users = Enumerable.Empty<AppUser>().AsQueryable();
        _userManager.Users.Returns(users);
        DataOptions? nullOptions = null;

        var expectedEmail = "admin@endatix.com";
        var expectedPassword = "P@ssw0rd";
        
        // Setup mock return value
        _userRegistrationService.RegisterUserAsync(expectedEmail, expectedPassword, 1L, true, Arg.Any<CancellationToken>())
            .Returns(Result<User>.Success(new User(1L, expectedEmail, expectedEmail, true)));

        // Act
        await IdentitySeed.SeedInitialUser(_userManager, _userRegistrationService, nullOptions!, _logger);

        // Assert
        await _userRegistrationService.Received(1)
            .RegisterUserAsync(expectedEmail, expectedPassword, 1L, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedInitialUser_NoUsersWithDataOptions_CreatesCustomUser()
    {
        // Arrange
        var users = Enumerable.Empty<AppUser>().AsQueryable();
        _userManager.Users.Returns(users);

        var expectedEmail = "custom@example.com";
        var expectedPassword = "CustomPass123";

        _dataOptions.InitialUser = new InitialUserOptions
        {
            Email = expectedEmail,
            Password = expectedPassword
        };
        
        // Setup mock return value
        _userRegistrationService.RegisterUserAsync(expectedEmail, expectedPassword, 1L, true, Arg.Any<CancellationToken>())
            .Returns(Result<User>.Success(new User(1L, expectedEmail, expectedEmail, true)));

        // Act
        await IdentitySeed.SeedInitialUser(_userManager, _userRegistrationService, _dataOptions, _logger);

        // Assert
        await _userRegistrationService.Received(1)
            .RegisterUserAsync(expectedEmail, expectedPassword, 1L, true, Arg.Any<CancellationToken>());
    }
}
