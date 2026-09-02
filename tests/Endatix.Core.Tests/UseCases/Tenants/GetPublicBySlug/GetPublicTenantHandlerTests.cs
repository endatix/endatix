using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Tenants.GetPublicBySlug;
using CoreEntities = Endatix.Core.Entities;

namespace Endatix.Core.Tests.UseCases.Tenants.GetPublicBySlug;

public class GetPublicTenantHandlerTests
{
    private const string Slug = "xk9mp2qr";
    private const long TenantId = 4242;

    private readonly IRepository<CoreEntities.Tenant> _tenantRepository;
    private readonly IRepository<CoreEntities.TenantSettings> _tenantSettingsRepository;
    private readonly GetPublicTenantHandler _sut;

    public GetPublicTenantHandlerTests()
    {
        _tenantRepository = Substitute.For<IRepository<CoreEntities.Tenant>>();
        _tenantSettingsRepository = Substitute.For<IRepository<CoreEntities.TenantSettings>>();
        _sut = new GetPublicTenantHandler(_tenantRepository, _tenantSettingsRepository);
    }

    [Theory]
    [InlineData("acme")]
    [InlineData("acme-regional-surveys")]
    [InlineData("xk9mp2qr8")]
    [InlineData("xk9mp2!r")]
    public async Task Handle_InvalidShortUrl_ReturnsNotFoundWithoutQuerying(string slug)
    {
        var result = await _sut.Handle(new GetPublicTenantQuery(slug), TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.NotFound);
        await _tenantRepository.DidNotReceive().SingleOrDefaultAsync(
            Arg.Any<TenantSpecifications.LiveByShortUrlSpec>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidEightLetterIdentifier_StillQueries()
    {
        _tenantRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.LiveByShortUrlSpec>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CoreEntities.Tenant?>(null));

        var result = await _sut.Handle(new GetPublicTenantQuery("abcdefgh"), TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.NotFound);
        await _tenantRepository.Received(1).SingleOrDefaultAsync(
            Arg.Any<TenantSpecifications.LiveByShortUrlSpec>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownSlug_ReturnsNotFound()
    {
        // Arrange
        _tenantRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.LiveByShortUrlSpec>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CoreEntities.Tenant?>(null));

        // Act
        var result = await _sut.Handle(new GetPublicTenantQuery(Slug), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Handle_TenantExists_ReturnsDtoWithoutNumericId()
    {
        // Arrange
        CoreEntities.Tenant tenant = new("Acme", Slug, "Primary") { Id = TenantId };
        CoreEntities.TenantSettings settings = new(TenantId);
        settings.UpdateSelfRegistrationPolicy(true, ["google", "endatix"], "Respondent");
        _tenantRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.LiveByShortUrlSpec>(), Arg.Any<CancellationToken>())
            .Returns(tenant);
        _tenantSettingsRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.SettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(settings);

        // Act
        var result = await _sut.Handle(new GetPublicTenantQuery(Slug), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Slug.Should().Be(Slug);
        result.Value.Name.Should().Be("Acme");
        result.Value.SelfRegistrationEnabled.Should().BeTrue();
        result.Value.AllowedAuthProviders.Should().BeEquivalentTo(["google", "endatix"]);
        result.Value.GetType().GetProperty("Id").Should().BeNull();
    }
}
