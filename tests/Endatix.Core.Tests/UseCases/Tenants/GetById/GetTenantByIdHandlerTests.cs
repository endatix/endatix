using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Tenants.GetById;
using CoreEntities = Endatix.Core.Entities;

namespace Endatix.Core.Tests.UseCases.Tenants.GetById;

public class GetTenantByIdHandlerTests
{
    private const long TENANT_ID = 4242;

    private readonly IRepository<CoreEntities.Tenant> _tenantRepository;
    private readonly IRepository<CoreEntities.TenantSettings> _tenantSettingsRepository;
    private readonly GetTenantByIdHandler _sut;

    public GetTenantByIdHandlerTests()
    {
        _tenantRepository = Substitute.For<IRepository<CoreEntities.Tenant>>();
        _tenantSettingsRepository = Substitute.For<IRepository<CoreEntities.TenantSettings>>();
        _sut = new GetTenantByIdHandler(_tenantRepository, _tenantSettingsRepository);
    }

    [Fact]
    public async Task Handle_TenantNotFound_ReturnsNotFound()
    {
        _tenantRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.ByIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CoreEntities.Tenant?>(null));

        var result = await _sut.Handle(new GetTenantByIdQuery(TENANT_ID), TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Handle_TenantExists_ReturnsDtoWithSettings()
    {
        CoreEntities.Tenant tenant = new("Acme", "acme", "Primary") { Id = TENANT_ID };
        CoreEntities.TenantSettings settings = new(TENANT_ID);
        settings.UpdateSelfRegistrationPolicy(true, ["google"], "Respondent");
        _tenantRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.ByIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(tenant);
        _tenantSettingsRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.SettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(settings);

        var result = await _sut.Handle(new GetTenantByIdQuery(TENANT_ID), TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Slug.Should().Be("acme");
        result.Value.AllowSelfRegistration.Should().BeTrue();
        result.Value.AllowedAuthProviderKeys.Should().BeEquivalentTo(["google"]);
    }
}
