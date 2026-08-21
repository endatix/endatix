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

    private readonly IRepository<CoreEntities.Tenant> _tenantRepository;
    private readonly IRepository<CoreEntities.TenantSettings> _tenantSettingsRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdGenerator<long> _idGenerator;
    private readonly IShortUrlGenerator _shortUrlGenerator;
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
        _sut = new CreateTenantHandler(
            _tenantRepository,
            _tenantSettingsRepository,
            _unitOfWork,
            _idGenerator,
            _shortUrlGenerator);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsCreatedTenantWithGeneratedShortUrl()
    {
        ShortUrlIsFree();
        CreateTenantCommand command = new(
            "Acme Surveys",
            "Primary tenant",
            AllowSelfRegistration: true,
            AllowedAuthProviderKeys: ["google", "google", " keycloak "],
            DefaultRegistrationRoleName: "Respondent");

        var result = await _sut.Handle(command, TestContext.Current.CancellationToken);

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
        ShortUrlIsFree();

        await _sut.Handle(new CreateTenantCommand("Acme"), TestContext.Current.CancellationToken);

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
        ShortUrlIsFree();
        CoreEntities.Tenant? addedTenant = null;
        _tenantRepository.AddAsync(Arg.Any<CoreEntities.Tenant>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                addedTenant = call.Arg<CoreEntities.Tenant>();
                return addedTenant;
            });

        await _sut.Handle(new CreateTenantCommand("Acme"), TestContext.Current.CancellationToken);

        addedTenant!.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TenantCreatedEvent>()
            .Which.EventType.Should().Be("tenant.created");
    }

    [Fact]
    public async Task Handle_FirstShortUrlTaken_RetriesUntilFree()
    {
        _shortUrlGenerator.Create(ShortUrlKind.Standard).Returns("aaaaaaaa", GeneratedShortUrl);
        _tenantRepository
            .AnyAsync(Arg.Any<TenantSpecifications.ExistsByShortUrlSpec>(), Arg.Any<CancellationToken>())
            .Returns(true, false);

        var result = await _sut.Handle(new CreateTenantCommand("Acme"), TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Created);
        result.Value.ShortUrl.Should().Be(GeneratedShortUrl);
        await _shortUrlGenerator.Received(2).Create(ShortUrlKind.Standard);
    }

    [Fact]
    public async Task Handle_AllShortUrlRetriesTaken_ReturnsErrorAndDoesNotPersist()
    {
        _tenantRepository
            .AnyAsync(Arg.Any<TenantSpecifications.ExistsByShortUrlSpec>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _sut.Handle(new CreateTenantCommand("Acme"), TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Error);
        await _tenantRepository.DidNotReceive().AddAsync(Arg.Any<CoreEntities.Tenant>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
        await _shortUrlGenerator.Received(ShortUrl.CollisionRetries).Create(ShortUrlKind.Standard);
    }

    [Fact]
    public async Task Handle_EmptyName_ReturnsInvalid()
    {
        ShortUrlIsFree();

        var result = await _sut.Handle(
            new CreateTenantCommand("   "),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle()
            .Which.Identifier.Should().Be(nameof(CreateTenantCommand.Name));
    }

    [Theory]
    [InlineData("PlatformAdmin")]
    [InlineData("Public")]
    public async Task Handle_ForbiddenRegistrationRole_ReturnsInvalidAndDoesNotPersist(string roleName)
    {
        ShortUrlIsFree();

        var result = await _sut.Handle(
            new CreateTenantCommand("Acme", DefaultRegistrationRoleName: roleName),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle()
            .Which.Identifier.Should().Be(nameof(CreateTenantCommand.DefaultRegistrationRoleName));
        await _tenantRepository.DidNotReceive().AddAsync(Arg.Any<CoreEntities.Tenant>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PersistenceFailure_RollsBackTransaction()
    {
        ShortUrlIsFree();
        _tenantSettingsRepository
            .When(repository => repository.AddAsync(Arg.Any<CoreEntities.TenantSettings>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("save failed"));

        var act = async () => await _sut.Handle(
            new CreateTenantCommand("Acme"),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _unitOfWork.Received(1).RollbackTransactionAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitTransactionAsync(Arg.Any<CancellationToken>());
    }

    private void ShortUrlIsFree() =>
        _tenantRepository
            .AnyAsync(Arg.Any<TenantSpecifications.ExistsByShortUrlSpec>(), Arg.Any<CancellationToken>())
            .Returns(false);
}
