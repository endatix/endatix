using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Users;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Features.Email;
using Endatix.Core.Infrastructure.Result;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Endatix.Core;

namespace Endatix.Infrastructure.Tests.Identity.Users;

public class AppUserRegistrationServiceTests
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IUserStore<AppUser> _userStore;
    private readonly IUserEmailStore<AppUser> _emailStore;
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly IEmailSender _emailSender;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly ILogger<AppUserRegistrationService> _logger;
    private readonly AppUserRegistrationService _sut;

    public AppUserRegistrationServiceTests()
    {
        _userStore = Substitute.For<IUserStore<AppUser>, IUserEmailStore<AppUser>>();
        _emailStore = (IUserEmailStore<AppUser>)_userStore;
        _userManager = Substitute.For<UserManager<AppUser>>(_userStore, null, null, null, null, null, null, null, null);
        _emailVerificationService = Substitute.For<IEmailVerificationService>();
        _emailSender = Substitute.For<IEmailSender>();
        _emailTemplateService = Substitute.For<IEmailTemplateService>();
        _logger = Substitute.For<ILogger<AppUserRegistrationService>>();
        
        _sut = new AppUserRegistrationService(_userManager, _userStore, _emailVerificationService, _emailSender, _emailTemplateService, _logger);
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
        var tenantId = 1;
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

        // Mock the email verification service
        _emailVerificationService.CreateVerificationTokenAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new EmailVerificationToken(userId, "test-token", DateTime.UtcNow.AddHours(24))));

        // Act
        var result = await _sut.RegisterUserAsync(email, password, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(userId);
        result.Value.TenantId.Should().Be(0); // Should be 0 for new users without tenant
        result.Value.Email.Should().Be(email);
        result.Value.IsVerified.Should().BeFalse(); // Should be false since email is not confirmed

        await _userStore.Received(1).SetUserNameAsync(
            Arg.Is<AppUser>(u => u.TenantId == 0 && !u.EmailConfirmed), 
            email, 
            cancellationToken);
        
        await _emailStore.Received(1).SetEmailAsync(
            Arg.Is<AppUser>(u => u.TenantId == 0 && !u.EmailConfirmed), 
            email, 
            cancellationToken);

        _userManager.Received(1).CreateAsync(
            Arg.Is<AppUser>(u => 
                u.TenantId == 0 && 
                !u.EmailConfirmed && 
                u.UserName == email && 
                u.Email == email),
            password);

        await _emailVerificationService.Received(1).CreateVerificationTokenAsync(userId, cancellationToken);
    }

    [Theory]
    [InlineData("test@mailinator.com")]
    [InlineData("test@10minutemail.com")]
    [InlineData("test@tempmail.com")]
    [InlineData("test@guerrillamail.com")]
    [InlineData("test@yopmail.com")]
    [InlineData("test@YOPMAIL.com")] // Test case insensitivity
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid-email")]
    [InlineData("test@")]
    [InlineData("@domain.com")]
    public async Task RegisterUserAsync_DisposableEmail_ReturnsInvalidResult(string email)
    {
        // Arrange
        var password = "P@ssw0rd";
        _userManager.SupportsUserEmail.Returns(true);

        // Act
        var result = await _sut.RegisterUserAsync(email, password, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainSingle(ve => ve.ErrorMessage == "The email is from a suspicious domain.");
    }

    [Theory]
    [InlineData("test@gmail.com")]
    [InlineData("test@outlook.com")]
    [InlineData("test@company.com")]
    [InlineData("test@university.edu")]
    public async Task RegisterUserAsync_NonDisposableEmail_ProceedsWithRegistration(string email)
    {
        // Arrange
        var password = "P@ssw0rd";
        var userId = 123L;
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

        // Mock the email verification service
        _emailVerificationService.CreateVerificationTokenAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new EmailVerificationToken(userId, "test-token", DateTime.UtcNow.AddHours(24))));

        // Act
        var result = await _sut.RegisterUserAsync(email, password, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Email.Should().Be(email);
    }

    [Fact]
    public async Task RegisterUserAsync_ValidRequest_SendsVerificationEmailWithTemplate()
    {
        // Arrange
        var userId = 123L;
        var email = "test@example.com";
        var password = "P@ssw0rd";
        var cancellationToken = CancellationToken.None;
        var token = "test-token";
        var emailWithTemplate = new EmailWithTemplate
        {
            To = email,
            From = "noreply@example.com",
            Subject = "Verify Your Email Address",
            TemplateId = "email-verification",
            Metadata = new Dictionary<string, object>
            {
                ["verificationToken"] = token,
                ["hubUrl"] = "https://app.example.com"
            }
        };

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
        _emailVerificationService.CreateVerificationTokenAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new EmailVerificationToken(userId, token, DateTime.UtcNow.AddHours(24))));
        _emailTemplateService.CreateVerificationEmail(email, token).Returns(emailWithTemplate);

        // Act
        var result = await _sut.RegisterUserAsync(email, password, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        _emailTemplateService.Received(1).CreateVerificationEmail(email, token);
        await _emailSender.Received(1).SendEmailAsync(emailWithTemplate, cancellationToken);
    }
}
