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

    private readonly IUserRegistrationService _userRegistrationService;
    private readonly IUserService _userService;
    private readonly IRepository<CoreEntities.Tenant> _tenantRepository;
    private readonly IRepository<CoreEntities.TenantSettings> _tenantSettingsRepository;
    private readonly IRoleManagementService _roleManagementService;
    private readonly IMediator _mediator;
    private readonly RegisterHandler _handler;

    public RegisterHandlerTests()
    {
        _userRegistrationService = Substitute.For<IUserRegistrationService>();
        _userService = Substitute.For<IUserService>();
        _tenantRepository = Substitute.For<IRepository<CoreEntities.Tenant>>();
        _tenantSettingsRepository = Substitute.For<IRepository<CoreEntities.TenantSettings>>();
        _roleManagementService = Substitute.For<IRoleManagementService>();
        _mediator = Substitute.For<IMediator>();
        _userService.GetUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<User>.NotFound());
        _handler = new RegisterHandler(
            _userRegistrationService,
            _userService,
            _tenantRepository,
            _tenantSettingsRepository,
            _roleManagementService,
            _mediator);
    }

    [Fact]
    public async Task Handle_RegistrationFails_ReturnsFailureResult()
    {
        var email = "test@example.com";
        var password = "password";
        var request = new RegisterCommand(email, password);
        var failureResult = Result<User>.Error("Registration failed");

        _userRegistrationService.RegisterUserAsync(email, password, Arg.Any<CancellationToken>())
            .Returns(failureResult);

        var result = await _handler.Handle(request, CancellationToken.None);

        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Registration failed");
    }

    [Fact]
    public async Task Handle_SuccessfulRegistration_PublishesEventAndReturnsSuccess()
    {
        var email = "test@example.com";
        var password = "password";
        var tenantId = 1L;
        var request = new RegisterCommand(email, password);
        var user = new User(1, tenantId, email, email, true);
        var successResult = Result<User>.Success(user);

        _userRegistrationService.RegisterUserAsync(email, password, Arg.Any<CancellationToken>())
            .Returns(successResult);

        var result = await _handler.Handle(request, CancellationToken.None);

        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(user);

        await _mediator.Received(1).Publish(
            Arg.Is<UserRegisteredEvent>(e => e.User == user),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Handle_UnknownTenantSlug_ReturnsNotFound()
    {
        _tenantRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.LiveByShortUrlSpec>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CoreEntities.Tenant?>(null));

        var result = await _handler.Handle(
            new RegisterCommand("user@example.com", "Password123!", Slug),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.NotFound);
        await _userRegistrationService.DidNotReceive().RegisterUserAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<long>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NameLikeSlug_ReturnsNotFound()
    {
        var result = await _handler.Handle(
            new RegisterCommand("user@example.com", "Password123!", "acme"),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.NotFound);
        await _tenantRepository.DidNotReceive().SingleOrDefaultAsync(
            Arg.Any<TenantSpecifications.LiveByShortUrlSpec>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SelfRegistrationDisabled_ReturnsForbidden()
    {
        CoreEntities.Tenant tenant = new("Acme", Slug) { Id = TenantId };
        CoreEntities.TenantSettings settings = new(TenantId);
        _tenantRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.LiveByShortUrlSpec>(), Arg.Any<CancellationToken>())
            .Returns(tenant);
        _tenantSettingsRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.SettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(settings);

        var result = await _handler.Handle(
            new RegisterCommand("user@example.com", "Password123!", Slug),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Forbidden);
        await _userRegistrationService.DidNotReceive().RegisterUserAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<long>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PlatformAdminDefaultRole_ReturnsInvalid()
    {
        CoreEntities.Tenant tenant = new("Acme", Slug) { Id = TenantId };
        CoreEntities.TenantSettings settings = new(TenantId);
        settings.UpdateSelfRegistrationPolicy(true, ["endatix"], "Respondent");
        typeof(CoreEntities.TenantSettings)
            .GetProperty(nameof(CoreEntities.TenantSettings.DefaultRegistrationRoleName))!
            .SetValue(settings, "PlatformAdmin");
        _tenantRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.LiveByShortUrlSpec>(), Arg.Any<CancellationToken>())
            .Returns(tenant);
        _tenantSettingsRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.SettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(settings);

        var result = await _handler.Handle(
            new RegisterCommand("user@example.com", "Password123!", Slug),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(error =>
            error.Identifier == nameof(CoreEntities.TenantSettings.DefaultRegistrationRoleName));
        await _userRegistrationService.DidNotReceive().RegisterUserAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<long>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TenantSlug_ExistingEmail_ReturnsInvalidAndDoesNotRegister()
    {
        var email = "existing@example.com";
        CoreEntities.Tenant tenant = new("Acme", Slug) { Id = TenantId };
        CoreEntities.TenantSettings settings = new(TenantId);
        settings.UpdateSelfRegistrationPolicy(true, ["endatix"], "Respondent");
        _tenantRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.LiveByShortUrlSpec>(), Arg.Any<CancellationToken>())
            .Returns(tenant);
        _tenantSettingsRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.SettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(settings);
        _userService.GetUserAsync(email, Arg.Any<CancellationToken>())
            .Returns(Result<User>.Success(new User(7, TenantId, email, email, true)));

        var result = await _handler.Handle(
            new RegisterCommand(email, "Password123!", Slug),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Invalid);
        await _userRegistrationService.DidNotReceive().RegisterUserAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<long>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
        await _roleManagementService.DidNotReceive().AssignRoleToUserAsync(
            Arg.Any<long>(),
            Arg.Any<string>(),
            Arg.Any<long>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TenantSlugEnabled_RegistersAndAssignsTenantScopedRole()
    {
        var email = "user@example.com";
        var password = "Password123!";
        CoreEntities.Tenant tenant = new("Acme", Slug) { Id = TenantId };
        CoreEntities.TenantSettings settings = new(TenantId);
        settings.UpdateSelfRegistrationPolicy(true, ["endatix"], "Respondent");
        var user = new User(7, TenantId, email, email, false);
        _tenantRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.LiveByShortUrlSpec>(), Arg.Any<CancellationToken>())
            .Returns(tenant);
        _tenantSettingsRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.SettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(settings);
        _userRegistrationService
            .RegisterUserAsync(email, password, TenantId, false, Arg.Any<CancellationToken>())
            .Returns(Result<User>.Success(user));
        _roleManagementService
            .AssignRoleToUserAsync(user.Id, "Respondent", TenantId, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var result = await _handler.Handle(
            new RegisterCommand(email, password, Slug),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        await _roleManagementService.Received(1).AssignRoleToUserAsync(
            user.Id,
            "Respondent",
            TenantId,
            Arg.Any<CancellationToken>());
    }
}
