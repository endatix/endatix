using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Tenants.GetPublicBySlug;
using CoreEntities = Endatix.Core.Entities;

namespace Endatix.Core.Tests.UseCases.Tenants.GetPublicBySlug;

public class GetPublicTenantHandlerTests
{
    private const string Slug = "xK9mP2qR8vNw";
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

    [Fact]
    public async Task Handle_NameLikeSlug_ReturnsNotFoundWithoutQuerying()
    {
        var result = await _sut.Handle(new GetPublicTenantQuery("acme"), TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.NotFound);
        await _tenantRepository.DidNotReceive().SingleOrDefaultAsync(
            Arg.Any<TenantSpecifications.LiveBySlugSpec>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownSlug_ReturnsNotFound()
    {
        _tenantRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.LiveBySlugSpec>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CoreEntities.Tenant?>(null));

        var result = await _sut.Handle(new GetPublicTenantQuery(Slug), TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Handle_TenantExists_ReturnsDtoWithoutNumericId()
    {
        CoreEntities.Tenant tenant = new("Acme", Slug, "Primary") { Id = TenantId };
        CoreEntities.TenantSettings settings = new(TenantId);
        settings.UpdateSelfRegistrationPolicy(true, ["google", "endatix"], "Respondent");
        _tenantRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.LiveBySlugSpec>(), Arg.Any<CancellationToken>())
            .Returns(tenant);
        _tenantSettingsRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.SettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(settings);

        var result = await _sut.Handle(new GetPublicTenantQuery(Slug), TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Slug.Should().Be(Slug);
        result.Value.Name.Should().Be("Acme");
        result.Value.SelfRegistrationEnabled.Should().BeTrue();
        result.Value.AllowedAuthProviders.Should().BeEquivalentTo(["google", "endatix"]);
        result.Value.GetType().GetProperty("Id").Should().BeNull();
    }
}
