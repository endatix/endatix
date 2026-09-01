using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Common;
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
    private const string GeneratedShortUrl = "xk9mp2qr";
    private const string CollidingShortUrl = "aaaaaaaa";

    private readonly IRepository<CoreEntities.Tenant> _tenantRepository;
    private readonly IRepository<CoreEntities.TenantSettings> _tenantSettingsRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdGenerator<long> _idGenerator;
    private readonly IShortUrlGenerator _shortUrlGenerator;
    private readonly IUniqueConstraintViolationChecker _uniqueConstraintViolationChecker;
    private readonly CreateTenantHandler _sut;

    public CreateTenantHandlerTests()
    {
        _tenantRepository = Substitute.For<IRepository<CoreEntities.Tenant>>();
        _tenantSettingsRepository = Substitute.For<IRepository<CoreEntities.TenantSettings>>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _idGenerator = Substitute.For<IIdGenerator<long>>();
        _idGenerator.CreateId().Returns(NEW_TENANT_ID);
        _shortUrlGenerator = Substitute.For<IShortUrlGenerator>();
        _shortUrlGenerator.Create(ShortUrlKind.Standard).Returns(GeneratedShortUrl);
        _uniqueConstraintViolationChecker = Substitute.For<IUniqueConstraintViolationChecker>();
        _uniqueConstraintViolationChecker.AnalyzeUniqueConstraint(Arg.Any<Exception>())
            .Returns(new UniqueConstraintViolationResult(false, null, null));
        _sut = new CreateTenantHandler(
            _tenantRepository,
            _tenantSettingsRepository,
            _unitOfWork,
            _idGenerator,
            _shortUrlGenerator,
            _uniqueConstraintViolationChecker);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsCreatedTenantWithGeneratedShortUrl()
    {
        // Arrange
        ShortUrlIsFree();
        CreateTenantCommand command = new(
            "Acme Surveys",
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
        result.Value.ShortUrl.Should().Be(GeneratedShortUrl);
        result.Value.ShortUrl.Should().NotBe(UrlSlugNormalizer.FromDisplayName("Acme Surveys"));
        result.Value.Description.Should().Be("Primary tenant");
        result.Value.AllowSelfRegistration.Should().BeTrue();
        result.Value.AllowedAuthProviderKeys.Should().BeEquivalentTo(["google", "keycloak"]);
        result.Value.DefaultRegistrationRoleName.Should().Be("Respondent");
    }

    [Fact]
    public async Task Handle_ValidCommand_PersistsTenantAndSettingsInOneTransaction()
    {
        // Arrange
        ShortUrlIsFree();

        // Act
        await _sut.Handle(new CreateTenantCommand("Acme"), TestContext.Current.CancellationToken);

        // Assert
        await _tenantRepository.Received(1).AddAsync(
            Arg.Is<CoreEntities.Tenant>(tenant => tenant.Id == NEW_TENANT_ID && tenant.ShortUrl == GeneratedShortUrl),
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
        ShortUrlIsFree();
        CoreEntities.Tenant? addedTenant = null;
        _tenantRepository.AddAsync(Arg.Any<CoreEntities.Tenant>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                addedTenant = call.Arg<CoreEntities.Tenant>();
                return addedTenant;
            });

        // Act
        await _sut.Handle(new CreateTenantCommand("Acme"), TestContext.Current.CancellationToken);

        // Assert
        addedTenant!.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TenantCreatedEvent>()
            .Which.EventType.Should().Be("tenant.created");
    }

    [Fact]
    public async Task Handle_FirstShortUrlTaken_RetriesUntilFree()
    {
        // Arrange
        _shortUrlGenerator.Create(ShortUrlKind.Standard).Returns(CollidingShortUrl, GeneratedShortUrl);
        _tenantRepository
            .AnyAsync(Arg.Any<TenantSpecifications.ExistsByShortUrlSpec>(), Arg.Any<CancellationToken>())
            .Returns(true, false);

        // Act
        var result = await _sut.Handle(new CreateTenantCommand("Acme"), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Created);
        result.Value.ShortUrl.Should().Be(GeneratedShortUrl);
        _shortUrlGenerator.Received(2).Create(ShortUrlKind.Standard);
    }

    [Fact]
    public async Task Handle_ShortUrlLostRaceAtUniqueIndex_RollsBackAndRetriesWithNewCandidate()
    {
        // Arrange - the pre-check clears both candidates, but a concurrent create already took the first.
        ShortUrlIsFree();
        _shortUrlGenerator.Create(ShortUrlKind.Standard).Returns(CollidingShortUrl, GeneratedShortUrl);
        ShortUrlRejectedByUniqueIndex(CollidingShortUrl);

        // Act
        var result = await _sut.Handle(new CreateTenantCommand("Acme"), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Created);
        result.Value.ShortUrl.Should().Be(GeneratedShortUrl);
        await _unitOfWork.Received(1).RollbackTransactionAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EveryShortUrlRejectedByUniqueIndex_ReturnsUnavailableAndRollsBackEachAttempt()
    {
        // Arrange
        ShortUrlIsFree();
        ShortUrlRejectedByUniqueIndex(GeneratedShortUrl);

        // Act
        var result = await _sut.Handle(new CreateTenantCommand("Acme"), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Unavailable);
        await _unitOfWork.Received(ShortUrl.CollisionRetries).RollbackTransactionAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AllShortUrlRetriesTaken_ReturnsUnavailableAndDoesNotPersist()
    {
        // Arrange
        _tenantRepository
            .AnyAsync(Arg.Any<TenantSpecifications.ExistsByShortUrlSpec>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _sut.Handle(new CreateTenantCommand("Acme"), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Unavailable);
        await _tenantRepository.DidNotReceive().AddAsync(Arg.Any<CoreEntities.Tenant>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
        _shortUrlGenerator.Received(ShortUrl.CollisionRetries).Create(ShortUrlKind.Standard);
    }

    [Fact]
    public async Task Handle_EmptyName_ReturnsInvalid()
    {
        // Arrange
        ShortUrlIsFree();

        // Act
        var result = await _sut.Handle(
            new CreateTenantCommand("   "),
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
        ShortUrlIsFree();

        // Act
        var result = await _sut.Handle(
            new CreateTenantCommand("Acme", DefaultRegistrationRoleName: roleName),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle()
            .Which.Identifier.Should().Be(nameof(CreateTenantCommand.DefaultRegistrationRoleName));
        await _tenantRepository.DidNotReceive().AddAsync(Arg.Any<CoreEntities.Tenant>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnrelatedPersistenceFailure_RollsBackAndRethrows()
    {
        // Arrange
        ShortUrlIsFree();
        _tenantSettingsRepository
            .When(repository => repository.AddAsync(Arg.Any<CoreEntities.TenantSettings>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("save failed"));

        // Act
        var act = async () => await _sut.Handle(
            new CreateTenantCommand("Acme"),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await _unitOfWork.Received(1).RollbackTransactionAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitTransactionAsync(Arg.Any<CancellationToken>());
    }

    private void ShortUrlIsFree() =>
        _tenantRepository
            .AnyAsync(Arg.Any<TenantSpecifications.ExistsByShortUrlSpec>(), Arg.Any<CancellationToken>())
            .Returns(false);

    private void ShortUrlRejectedByUniqueIndex(string shortUrl)
    {
        var conflict = new InvalidOperationException("duplicate key value violates unique constraint");
        _uniqueConstraintViolationChecker.AnalyzeUniqueConstraint(conflict)
            .Returns(new UniqueConstraintViolationResult(true, CoreEntities.Tenant.UniqueConstraints.ShortUrl, null));
        _tenantRepository
            .When(repository => repository.AddAsync(
                Arg.Is<CoreEntities.Tenant>(tenant => tenant.ShortUrl == shortUrl),
                Arg.Any<CancellationToken>()))
            .Do(_ => throw conflict);
    }
}
