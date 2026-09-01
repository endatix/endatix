using Endatix.Core.Abstractions.Data;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Tenants.Update;
using CoreEntities = Endatix.Core.Entities;

namespace Endatix.Core.Tests.UseCases.Tenants.Update;

public class UpdateTenantHandlerTests
{
    private const long TENANT_ID = 4242;

    private readonly IRepository<CoreEntities.Tenant> _tenantRepository;
    private readonly IRepository<CoreEntities.TenantSettings> _tenantSettingsRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UpdateTenantHandler _sut;

    public UpdateTenantHandlerTests()
    {
        _tenantRepository = Substitute.For<IRepository<CoreEntities.Tenant>>();
        _tenantSettingsRepository = Substitute.For<IRepository<CoreEntities.TenantSettings>>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _sut = new UpdateTenantHandler(_tenantRepository, _tenantSettingsRepository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_TenantNotFound_ReturnsNotFound()
    {
        // Arrange
        _tenantRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.ByIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CoreEntities.Tenant?>(null));

        // Act
        var result = await _sut.Handle(
            new UpdateTenantCommand(TENANT_ID, Name: "Renamed"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NameAndDescription_UpdatesTenantAndSavesOnce()
    {
        // Arrange
        var tenant = ExistingTenant();
        ExistingSettings();

        // Act
        var result = await _sut.Handle(
            new UpdateTenantCommand(TENANT_ID, Name: " Renamed ", Description: " New description "),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        tenant.Name.Should().Be("Renamed");
        tenant.Description.Should().Be("New description");
        result.Value.Name.Should().Be("Renamed");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmptyDescription_ClearsDescription()
    {
        // Arrange
        var tenant = ExistingTenant();
        ExistingSettings();

        // Act
        var result = await _sut.Handle(
            new UpdateTenantCommand(TENANT_ID, Description: "  "),
            TestContext.Current.CancellationToken);

        // Assert
        tenant.Description.Should().BeNull();
        result.Value.Description.Should().BeNull();
    }

    [Fact]
    public async Task Handle_AnyCommand_NeverChangesShortUrl()
    {
        // Arrange
        var tenant = ExistingTenant();
        ExistingSettings();

        // Act
        var result = await _sut.Handle(
            new UpdateTenantCommand(TENANT_ID, Name: "Renamed"),
            TestContext.Current.CancellationToken);

        // Assert
        tenant.ShortUrl.Should().Be("xk9mp2qr");
        result.Value.ShortUrl.Should().Be("xk9mp2qr");
    }

    [Fact]
    public async Task Handle_SelfRegistrationFields_UpdatesPolicy()
    {
        // Arrange
        ExistingTenant();
        var settings = ExistingSettings();

        // Act
        var result = await _sut.Handle(
            new UpdateTenantCommand(
                TENANT_ID,
                AllowSelfRegistration: true,
                AllowedAuthProviderKeys: ["google"],
                DefaultRegistrationRoleName: "Respondent"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        settings.AllowSelfRegistration.Should().BeTrue();
        settings.AllowedAuthProviderKeys.Should().BeEquivalentTo(["google"]);
        result.Value.AllowSelfRegistration.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_OnlyAllowSelfRegistration_KeepsExistingProviderKeysAndRole()
    {
        // Arrange
        ExistingTenant();
        var settings = ExistingSettings();
        settings.UpdateSelfRegistrationPolicy(false, ["keycloak"], "Respondent");

        // Act
        await _sut.Handle(
            new UpdateTenantCommand(TENANT_ID, AllowSelfRegistration: true),
            TestContext.Current.CancellationToken);

        // Assert
        settings.AllowSelfRegistration.Should().BeTrue();
        settings.AllowedAuthProviderKeys.Should().BeEquivalentTo(["keycloak"]);
        settings.DefaultRegistrationRoleName.Should().Be("Respondent");
    }

    [Theory]
    [InlineData("PlatformAdmin")]
    [InlineData("Public")]
    public async Task Handle_ForbiddenRegistrationRole_ReturnsInvalidAndDoesNotSave(string roleName)
    {
        // Arrange
        ExistingTenant();
        var settings = ExistingSettings();

        // Act
        var result = await _sut.Handle(
            new UpdateTenantCommand(TENANT_ID, DefaultRegistrationRoleName: roleName),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle()
            .Which.Identifier.Should().Be(nameof(UpdateTenantCommand.DefaultRegistrationRoleName));
        settings.DefaultRegistrationRoleName.Should().Be(CoreEntities.TenantSettings.DefaultRegistrationRole);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmptyName_ReturnsInvalidAndDoesNotSave()
    {
        // Arrange
        var tenant = ExistingTenant();
        ExistingSettings();

        // Act
        var result = await _sut.Handle(
            new UpdateTenantCommand(TENANT_ID, Name: "   "),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        tenant.Name.Should().Be("Acme");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SelfRegistrationUpdateWithoutSettings_ReturnsNotFound()
    {
        // Arrange
        ExistingTenant();
        _tenantSettingsRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.SettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CoreEntities.TenantSettings?>(null));

        // Act
        var result = await _sut.Handle(
            new UpdateTenantCommand(TENANT_ID, AllowSelfRegistration: true),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidNameButForbiddenRole_LeavesTenantUnmutated()
    {
        // Arrange
        var tenant = ExistingTenant();
        var settings = ExistingSettings();

        // Act
        var result = await _sut.Handle(
            new UpdateTenantCommand(
                TENANT_ID,
                Name: "Renamed",
                Description: "New description",
                DefaultRegistrationRoleName: "PlatformAdmin"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        tenant.Name.Should().Be("Acme");
        tenant.Description.Should().Be("Original description");
        settings.DefaultRegistrationRoleName.Should().Be(CoreEntities.TenantSettings.DefaultRegistrationRole);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidNameButMissingSettings_LeavesTenantUnmutated()
    {
        // Arrange
        var tenant = ExistingTenant();
        _tenantSettingsRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.SettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CoreEntities.TenantSettings?>(null));

        // Act
        var result = await _sut.Handle(
            new UpdateTenantCommand(TENANT_ID, Name: "Renamed", AllowSelfRegistration: true),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        tenant.Name.Should().Be("Acme");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private CoreEntities.Tenant ExistingTenant()
    {
        CoreEntities.Tenant tenant = new("Acme", "xk9mp2qr", "Original description") { Id = TENANT_ID };
        _tenantRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.ByIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(tenant);

        return tenant;
    }

    private CoreEntities.TenantSettings ExistingSettings()
    {
        CoreEntities.TenantSettings settings = new(TENANT_ID);
        _tenantSettingsRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.SettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(settings);

        return settings;
    }
}
