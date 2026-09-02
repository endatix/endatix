using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Identity.Register;
using MediatR;
using CoreEntities = Endatix.Core.Entities;

namespace Endatix.Core.Tests.UseCases.Identity.Register;

public class RegisterHandlerTests
{
    private const string Slug = "xk9mp2qr";
    private const long TenantId = 99;
    private const string RegistrationRole = "Respondent";
    private const string Password = "Password123!";

    private readonly IUserRegistrationService _userRegistrationService;
    private readonly IRepository<CoreEntities.Tenant> _tenantRepository;
    private readonly IRepository<CoreEntities.TenantSettings> _tenantSettingsRepository;
    private readonly IRoleManagementService _roleManagementService;
    private readonly IMediator _mediator;
    private readonly RegisterHandler _handler;

    public RegisterHandlerTests()
    {
        _userRegistrationService = Substitute.For<IUserRegistrationService>();
        _tenantRepository = Substitute.For<IRepository<CoreEntities.Tenant>>();
        _tenantSettingsRepository = Substitute.For<IRepository<CoreEntities.TenantSettings>>();
        _roleManagementService = Substitute.For<IRoleManagementService>();
        _mediator = Substitute.For<IMediator>();
        _roleManagementService
            .GetMissingAssignableRoleNamesAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<string>>.Success([]));
        _handler = new RegisterHandler(
            _userRegistrationService,
            _tenantRepository,
            _tenantSettingsRepository,
            _roleManagementService,
            _mediator);
    }

    [Fact]
    public async Task Handle_RegistrationFails_ReturnsFailureResult()
    {
        // Arrange
        var email = "test@example.com";
        var password = "password";
        var request = new RegisterCommand(email, password);
        _userRegistrationService.RegisterUserAsync(email, password, Arg.Any<CancellationToken>())
            .Returns(Result<User>.Error("Registration failed"));

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Registration failed");
    }

    [Fact]
    public async Task Handle_SuccessfulRegistration_PublishesEventAndReturnsSuccess()
    {
        // Arrange
        var email = "test@example.com";
        var password = "password";
        var request = new RegisterCommand(email, password);
        var user = new User(1, 1L, email, email, true);
        _userRegistrationService.RegisterUserAsync(email, password, Arg.Any<CancellationToken>())
            .Returns(Result<User>.Success(user));

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(RegisterHandler.GENERAL_SUCCESS_MESSAGE);
        await _mediator.Received(1).Publish(
            Arg.Is<UserRegisteredEvent>(e => e.User == user),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownTenantSlug_ReturnsNotFound()
    {
        // Arrange
        ArrangeTenant(null);

        // Act
        var result = await _handler.Handle(
            new RegisterCommand("user@example.com", "Password123!", Slug),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        await DidNotRegister();
    }

    [Theory]
    [InlineData("acme")]
    [InlineData("acme-regional-surveys")]
    public async Task Handle_InvalidShortUrl_ReturnsNotFoundWithoutQuerying(string tenantSlug)
    {
        var result = await _handler.Handle(
            new RegisterCommand("user@example.com", "Password123!", tenantSlug),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.NotFound);
        await _tenantRepository.DidNotReceive().SingleOrDefaultAsync(
            Arg.Any<TenantSpecifications.LiveByShortUrlSpec>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SelfRegistrationDisabled_ReturnsForbidden()
    {
        // Arrange
        ArrangeTenant(NewTenant());
        ArrangeSettings(new CoreEntities.TenantSettings(TenantId));

        // Act
        var result = await _handler.Handle(
            new RegisterCommand("user@example.com", "Password123!", Slug),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Forbidden);
        await DidNotRegister();
    }

    [Fact]
    public async Task Handle_PlatformAdminDefaultRole_ReturnsInvalid()
    {
        // Arrange
        ArrangeTenant(NewTenant());
        var settings = EnabledSettings();
        typeof(CoreEntities.TenantSettings)
            .GetProperty(nameof(CoreEntities.TenantSettings.DefaultRegistrationRoleName))!
            .SetValue(settings, "PlatformAdmin");
        ArrangeSettings(settings);

        // Act
        var result = await _handler.Handle(
            new RegisterCommand("user@example.com", "Password123!", Slug),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(error =>
            error.Identifier == nameof(CoreEntities.TenantSettings.DefaultRegistrationRoleName));
        await DidNotRegister();
    }

    [Fact]
    public async Task Handle_DefaultRoleNotAssignable_ReturnsInvalidWithoutCreatingUser()
    {
        // Arrange
        ArrangeTenant(NewTenant());
        ArrangeSettings(EnabledSettings());
        _roleManagementService
            .GetMissingAssignableRoleNamesAsync(Arg.Any<IReadOnlyList<string>>(), TenantId, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<string>>.Success([RegistrationRole]));

        // Act
        var result = await _handler.Handle(
            new RegisterCommand("user@example.com", "Password123!", Slug),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(error =>
            error.Identifier == nameof(CoreEntities.TenantSettings.DefaultRegistrationRoleName));
        await DidNotRegister();
    }

    [Fact]
    public async Task Handle_TenantSlugEnabled_RegistersTenantUserWithDefaultRole()
    {
        // Arrange
        var email = "user@example.com";
        var password = "Password123!";
        var user = new User(7, TenantId, email, email, false);
        ArrangeTenant(NewTenant());
        ArrangeSettings(EnabledSettings());
        _userRegistrationService
            .RegisterTenantUserAsync(email, password, TenantId, RegistrationRole, Arg.Any<CancellationToken>())
            .Returns(Result<User>.Success(user));

        // Act
        var result = await _handler.Handle(
            new RegisterCommand(email, password, Slug),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().Be(RegisterHandler.GENERAL_SUCCESS_MESSAGE);
        await _userRegistrationService.Received(1).RegisterTenantUserAsync(
            email,
            password,
            TenantId,
            RegistrationRole,
            Arg.Any<CancellationToken>());
        await _mediator.Received(1).Publish(
            Arg.Is<UserRegisteredEvent>(e => e.User == user),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TenantRegistrationFails_DoesNotPublishUserRegisteredEvent()
    {
        // Arrange
        ArrangeTenant(NewTenant());
        ArrangeSettings(EnabledSettings());
        _userRegistrationService
            .RegisterTenantUserAsync(
                Arg.Any<string>(), Arg.Any<string>(), TenantId, RegistrationRole, Arg.Any<CancellationToken>())
            .Returns(Result<User>.Invalid(new ValidationError("The email is from a suspicious domain.")));

        // Act
        var result = await _handler.Handle(
            new RegisterCommand("user@example.com", "Password123!", Slug),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        await _mediator.DidNotReceive().Publish(Arg.Any<UserRegisteredEvent>(), Arg.Any<CancellationToken>());
    }

    #region Security and Privacy Tests

    [Fact]
    public async Task Handle_TenantSlug_EmailAlreadyRegistered_IsIndistinguishableFromNewAccount()
    {
        // Arrange
        var newAccount = new User(7, TenantId, "new@example.com", "new@example.com", false);
        ArrangeTenant(NewTenant());
        ArrangeSettings(EnabledSettings());
        _userRegistrationService
            .RegisterTenantUserAsync("new@example.com", Password, TenantId, RegistrationRole, Arg.Any<CancellationToken>())
            .Returns(Result<User>.Success(newAccount));
        _userRegistrationService
            .RegisterTenantUserAsync("taken@example.com", Password, TenantId, RegistrationRole, Arg.Any<CancellationToken>())
            .Returns(Result<User>.NoContent());

        // Act
        var created = await _handler.Handle(
            new RegisterCommand("new@example.com", Password, Slug), TestContext.Current.CancellationToken);
        var taken = await _handler.Handle(
            new RegisterCommand("taken@example.com", Password, Slug), TestContext.Current.CancellationToken);

        // Assert
        taken.Status.Should().Be(created.Status);
        taken.Value.Should().Be(created.Value);
        taken.Value.Should().NotContain("already");
        await _mediator.Received(1).Publish(Arg.Any<UserRegisteredEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Unattached_EmailAlreadyRegistered_IsIndistinguishableFromNewAccount()
    {
        // Arrange
        var newAccount = new User(1, "new@example.com", "new@example.com", false);
        _userRegistrationService.RegisterUserAsync("new@example.com", Password, Arg.Any<CancellationToken>())
            .Returns(Result<User>.Success(newAccount));
        _userRegistrationService.RegisterUserAsync("taken@example.com", Password, Arg.Any<CancellationToken>())
            .Returns(Result<User>.NoContent());

        // Act
        var created = await _handler.Handle(
            new RegisterCommand("new@example.com", Password), TestContext.Current.CancellationToken);
        var taken = await _handler.Handle(
            new RegisterCommand("taken@example.com", Password), TestContext.Current.CancellationToken);

        // Assert
        taken.Status.Should().Be(created.Status);
        taken.Value.Should().Be(created.Value);
        await _mediator.Received(1).Publish(Arg.Any<UserRegisteredEvent>(), Arg.Any<CancellationToken>());
    }

    #endregion

    private static CoreEntities.Tenant NewTenant() => new("Acme", Slug) { Id = TenantId };

    private static CoreEntities.TenantSettings EnabledSettings()
    {
        CoreEntities.TenantSettings settings = new(TenantId);
        settings.UpdateSelfRegistrationPolicy(true, ["endatix"], RegistrationRole);
        return settings;
    }

    private void ArrangeTenant(CoreEntities.Tenant? tenant) =>
        _tenantRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.LiveByShortUrlSpec>(), Arg.Any<CancellationToken>())
            .Returns(tenant);

    private void ArrangeSettings(CoreEntities.TenantSettings settings) =>
        _tenantSettingsRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.SettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(settings);

    private async Task DidNotRegister() =>
        await _userRegistrationService.DidNotReceive().RegisterTenantUserAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<long>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
}
