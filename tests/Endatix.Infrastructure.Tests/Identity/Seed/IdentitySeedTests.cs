using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Tests;
using Endatix.Framework.Logging;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Tests.Identity.Seed;

public class IdentitySeedTests
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IUserRegistrationService _userRegistrationService;

    private readonly IRoleManagementService _roleManagementService;
    private readonly DataOptions _dataOptions;
    private readonly ILogger _logger;

    public IdentitySeedTests()
    {
        _userManager = Substitute.For<UserManager<AppUser>>(
            Substitute.For<IUserStore<AppUser>>(),
            null, null, null, null, null, null, null, null);
        _userRegistrationService = Substitute.For<IUserRegistrationService>();
        _roleManagementService = Substitute.For<IRoleManagementService>();
        _dataOptions = new DataOptions();
        _logger = Substitute.For<ILogger>();
    }

    [Fact]
    public async Task SeedInitialUser_NullUserManager_ThrowsArgumentNullException()
    {
        // Arrange
        UserManager<AppUser> userManager = null!;

        // Act
        var act = () => IdentitySeed.SeedInitialUser(userManager, _userRegistrationService, _roleManagementService, _dataOptions, _logger);

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
        var act = () => IdentitySeed.SeedInitialUser(_userManager, userRegistrationService, _roleManagementService, _dataOptions, _logger);

        // Assert
        var expectedMessage = ErrorMessages.GetErrorMessage(nameof(userRegistrationService), ErrorType.Null);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithMessage(expectedMessage);
    }

    [Fact]
    public async Task SeedInitialUser_NullRoleManagementService_ThrowsArgumentNullException()
    {
        // Arrange
        IRoleManagementService roleManagementService = null!;

        // Act
        var act = () => IdentitySeed.SeedInitialUser(_userManager, _userRegistrationService, roleManagementService, _dataOptions, _logger);

        // Assert
        var expectedMessage = ErrorMessages.GetErrorMessage(nameof(roleManagementService), ErrorType.Null);
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
        await IdentitySeed.SeedInitialUser(_userManager, _userRegistrationService, _roleManagementService, _dataOptions, _logger);

        // Assert
        await _userRegistrationService.DidNotReceive()
            .RegisterUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedInitialUser_NoUsersAndNoInitialUserSection_SkipsSeedingAndLogsError()
    {
        // Arrange
        _userManager.Users.Returns(Enumerable.Empty<AppUser>().AsQueryable());
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        _dataOptions.InitialUser = null;

        // Act
        await IdentitySeed.SeedInitialUser(_userManager, _userRegistrationService, _roleManagementService, _dataOptions, _logger);

        // Assert
        await AssertSkippedWithPasswordNotConfiguredError();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SeedInitialUser_NoUsersAndEmailWithoutPassword_SkipsSeedingAndLogsError(string password)
    {
        // Arrange
        _userManager.Users.Returns(Enumerable.Empty<AppUser>().AsQueryable());
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        _dataOptions.InitialUser = new InitialUserOptions { Email = "admin@example.com", Password = password };

        // Act
        await IdentitySeed.SeedInitialUser(_userManager, _userRegistrationService, _roleManagementService, _dataOptions, _logger);

        // Assert
        await AssertSkippedWithPasswordNotConfiguredError();
    }

    [Fact]
    public async Task SeedInitialUser_PasswordWithoutEmail_CreatesUserWithDefaultEmail()
    {
        // Arrange
        _userManager.Users.Returns(Enumerable.Empty<AppUser>().AsQueryable());
        const string expectedEmail = "admin@endatix.com";
        const string expectedPassword = "Configured1!";
        _dataOptions.InitialUser = new InitialUserOptions { Password = expectedPassword };
        _userRegistrationService.RegisterUserAsync(expectedEmail, expectedPassword, 1L, true, Arg.Any<CancellationToken>())
            .Returns(Result<User>.Success(new User(2L, expectedEmail, expectedEmail, true)));
        _roleManagementService.AssignRoleToUserAsync(2L, SystemRole.PlatformAdmin.Name, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        await IdentitySeed.SeedInitialUser(_userManager, _userRegistrationService, _roleManagementService, _dataOptions, _logger);

        // Assert
        await _userRegistrationService.Received(1)
            .RegisterUserAsync(expectedEmail, expectedPassword, 1L, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void WebHostBaseSettings_DoNotCarryInitialUserPassword()
    {
        var dataOptions = BindWebHostDataOptions("appsettings.json");

        dataOptions.InitialUser?.Password.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task SeedInitialUser_WebHostDevelopmentSettings_CreatesInitialUser()
    {
        // Arrange
        _userManager.Users.Returns(Enumerable.Empty<AppUser>().AsQueryable());
        var dataOptions = BindWebHostDataOptions("appsettings.json", "appsettings.Development.json");
        var email = dataOptions.InitialUser!.Email;
        var password = dataOptions.InitialUser.Password;
        _userRegistrationService.RegisterUserAsync(email, password, 1L, true, Arg.Any<CancellationToken>())
            .Returns(Result<User>.Success(new User(2L, email, email, true)));
        _roleManagementService.AssignRoleToUserAsync(2L, SystemRole.PlatformAdmin.Name, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        await IdentitySeed.SeedInitialUser(_userManager, _userRegistrationService, _roleManagementService, dataOptions, _logger);

        // Assert
        password.Should().NotBeNullOrWhiteSpace();
        await _userRegistrationService.Received(1)
            .RegisterUserAsync(email, password, 1L, true, Arg.Any<CancellationToken>());
        await _roleManagementService.Received(1)
            .AssignRoleToUserAsync(2L, SystemRole.PlatformAdmin.Name, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedInitialUser_NoUsersWithDataOptions_CreatesCustomUser()
    {
        // Arrange
        var users = Enumerable.Empty<AppUser>().AsQueryable();
        _userManager.Users.Returns(users);

        var expectedEmail = "custom@example.com";
        var expectedPassword = "CustomPass123";
        var expectedUserId = 2L;

        _dataOptions.InitialUser = new InitialUserOptions
        {
            Email = expectedEmail,
            Password = expectedPassword
        };

        // Setup mock return value
        _userRegistrationService.RegisterUserAsync(expectedEmail, expectedPassword, 1L, true, Arg.Any<CancellationToken>())
            .Returns(Result<User>.Success(new User(expectedUserId, expectedEmail, expectedEmail, true)));
        _roleManagementService.AssignRoleToUserAsync(expectedUserId, SystemRole.PlatformAdmin.Name, Arg.Any<CancellationToken>())
           .Returns(Result.Success());

        // Act
        await IdentitySeed.SeedInitialUser(_userManager, _userRegistrationService, _roleManagementService, _dataOptions, _logger);

        // Assert
        await _userRegistrationService.Received(1)
            .RegisterUserAsync(expectedEmail, expectedPassword, 1L, true, Arg.Any<CancellationToken>());

        await _roleManagementService.Received(1)
            .AssignRoleToUserAsync(expectedUserId, SystemRole.PlatformAdmin.Name, Arg.Any<CancellationToken>());
    }

    private async Task AssertSkippedWithPasswordNotConfiguredError()
    {
        await _userRegistrationService.DidNotReceive()
            .RegisterUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        var errorLog = _logger.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(ILogger.Log))
            .Select(call => call.GetArguments())
            .Should().ContainSingle(args => (LogLevel)args[0]! == LogLevel.Error).Subject;
        ((EventId)errorLog[1]!).Id.Should().Be(EndatixEventIds.IdentitySeed.PasswordNotConfigured);
        var message = ((Delegate)errorLog[4]!).DynamicInvoke(errorLog[2], errorLog[3]) as string;
        message.Should().Contain("Endatix:Data:InitialUser:Password");
    }

    private static DataOptions BindWebHostDataOptions(params string[] settingsFiles)
    {
        var webHostDirectory = Path.Combine(RepositoryRoot(), "src", "Endatix.WebHost");
        var builder = new ConfigurationBuilder().SetBasePath(webHostDirectory);
        foreach (var settingsFile in settingsFiles)
        {
            builder.AddJsonFile(settingsFile, optional: false);
        }

        var dataOptions = new DataOptions();
        builder.Build().GetSection(dataOptions.FullSectionName).Bind(dataOptions);
        return dataOptions;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Endatix.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
