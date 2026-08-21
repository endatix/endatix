using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Tenants.Create;
using CoreEntities = Endatix.Core.Entities;

namespace Endatix.Core.Tests.UseCases.Tenants.Create;

public class CreateTenantHandlerTests
{
    private const long NEW_TENANT_ID = 4242;

    private readonly IRepository<CoreEntities.Tenant> _tenantRepository;
    private readonly IRepository<CoreEntities.TenantSettings> _tenantSettingsRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdGenerator<long> _idGenerator;
    private readonly CreateTenantHandler _sut;

    public CreateTenantHandlerTests()
    {
        _tenantRepository = Substitute.For<IRepository<CoreEntities.Tenant>>();
        _tenantSettingsRepository = Substitute.For<IRepository<CoreEntities.TenantSettings>>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _idGenerator = Substitute.For<IIdGenerator<long>>();
        _idGenerator.CreateId().Returns(NEW_TENANT_ID);
        _sut = new CreateTenantHandler(_tenantRepository, _tenantSettingsRepository, _unitOfWork, _idGenerator);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsCreatedTenantWithSettings()
    {
        // Arrange
        SlugIsFree();
        CreateTenantCommand command = new(
            "Acme Surveys",
            "acme-surveys",
            "Primary tenant",
            AllowSelfRegistration: true,
            AllowedAuthProviderKeys: ["google", "google", " keycloak "],
            DefaultRegistrationRoleName: "Respondent");

        // Act
        var result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Created);
        result.Value.Id.Should().Be(NEW_TENANT_ID);
        result.Value.Name.Should().Be("Acme Surveys");
        result.Value.Slug.Should().Be("acme-surveys");
        result.Value.Description.Should().Be("Primary tenant");
        result.Value.AllowSelfRegistration.Should().BeTrue();
        result.Value.AllowedAuthProviderKeys.Should().BeEquivalentTo(["google", "keycloak"]);
        result.Value.DefaultRegistrationRoleName.Should().Be("Respondent");
    }

    [Fact]
    public async Task Handle_ValidCommand_PersistsTenantAndSettingsInOneTransaction()
    {
        // Arrange
        SlugIsFree();

        // Act
        await _sut.Handle(new CreateTenantCommand("Acme", "acme"), TestContext.Current.CancellationToken);

        // Assert
        await _tenantRepository.Received(1).AddAsync(
            Arg.Is<CoreEntities.Tenant>(tenant => tenant.Id == NEW_TENANT_ID && tenant.Slug == "acme"),
            Arg.Any<CancellationToken>());
        await _tenantSettingsRepository.Received(1).AddAsync(
            Arg.Is<CoreEntities.TenantSettings>(settings => settings.TenantId == NEW_TENANT_ID),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).BeginTransactionAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitTransactionAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().RollbackTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidCommand_RaisesTenantCreatedIntegrationEvent()
    {
        // Arrange
        SlugIsFree();
        CoreEntities.Tenant? addedTenant = null;
        _tenantRepository.AddAsync(Arg.Any<CoreEntities.Tenant>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                addedTenant = call.Arg<CoreEntities.Tenant>();
                return addedTenant;
            });

        // Act
        await _sut.Handle(new CreateTenantCommand("Acme", "acme"), TestContext.Current.CancellationToken);

        // Assert
        addedTenant!.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TenantCreatedEvent>()
            .Which.EventType.Should().Be("tenant.created");
    }

    [Fact]
    public async Task Handle_UnnormalizedSlug_NormalizesBeforeUniquenessCheck()
    {
        // Arrange
        SlugIsFree();

        // Act
        var result = await _sut.Handle(
            new CreateTenantCommand("Acme Surveys", " Acme Surveys "),
            TestContext.Current.CancellationToken);

        // Assert
        result.Value.Slug.Should().Be("acme-surveys");
    }

    [Fact]
    public async Task Handle_DuplicateSlug_ReturnsInvalidAndDoesNotPersist()
    {
        // Arrange
        _tenantRepository
            .AnyAsync(Arg.Any<TenantSpecifications.ExistsBySlugSpec>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _sut.Handle(
            new CreateTenantCommand("Acme", "acme"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle()
            .Which.Identifier.Should().Be(nameof(CreateTenantCommand.Slug));
        await _tenantRepository.DidNotReceive().AddAsync(Arg.Any<CoreEntities.Tenant>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReservedSlug_ReturnsInvalid()
    {
        // Arrange
        SlugIsFree();

        // Act
        var result = await _sut.Handle(
            new CreateTenantCommand("Admin", "admin"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Contain("reserved");
        await _tenantRepository.DidNotReceive().AddAsync(Arg.Any<CoreEntities.Tenant>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmptyName_ReturnsInvalid()
    {
        // Arrange
        SlugIsFree();

        // Act
        var result = await _sut.Handle(
            new CreateTenantCommand("   ", "acme"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle()
            .Which.Identifier.Should().Be(nameof(CreateTenantCommand.Name));
    }

    [Theory]
    [InlineData("PlatformAdmin")]
    [InlineData("Public")]
    public async Task Handle_ForbiddenRegistrationRole_ReturnsInvalidAndDoesNotPersist(string roleName)
    {
        // Arrange
        SlugIsFree();

        // Act
        var result = await _sut.Handle(
            new CreateTenantCommand("Acme", "acme", DefaultRegistrationRoleName: roleName),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle()
            .Which.Identifier.Should().Be(nameof(CreateTenantCommand.DefaultRegistrationRoleName));
        await _tenantRepository.DidNotReceive().AddAsync(Arg.Any<CoreEntities.Tenant>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PersistenceFailure_RollsBackTransaction()
    {
        // Arrange
        SlugIsFree();
        _tenantSettingsRepository
            .When(repository => repository.AddAsync(Arg.Any<CoreEntities.TenantSettings>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("save failed"));

        // Act
        var act = async () => await _sut.Handle(
            new CreateTenantCommand("Acme", "acme"),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await _unitOfWork.Received(1).RollbackTransactionAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitTransactionAsync(Arg.Any<CancellationToken>());
    }

    private void SlugIsFree() =>
        _tenantRepository
            .AnyAsync(Arg.Any<TenantSpecifications.ExistsBySlugSpec>(), Arg.Any<CancellationToken>())
            .Returns(false);
}
