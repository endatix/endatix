using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Identity.ListMyTenants;

namespace Endatix.Core.Tests.UseCases.Identity.ListMyTenants;

public class ListMyTenantsHandlerTests
{
    private const long UserId = 7;
    private const long HomeTenantId = 10;
    private const long OtherTenantId = 20;
    private const string HomeSlug = "xK9mP2qR8vNw";
    private const string OtherSlug = "aB3dE5fG7hIj";

    private readonly IUserContext _userContext = Substitute.For<IUserContext>();
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly IRepository<Tenant> _tenantRepository = Substitute.For<IRepository<Tenant>>();
    private readonly ListMyTenantsHandler _sut;

    public ListMyTenantsHandlerTests()
    {
        _sut = new ListMyTenantsHandler(_userContext, _userService, _tenantRepository);
    }

    [Fact]
    public async Task Handle_Anonymous_ReturnsUnauthorized()
    {
        _userContext.GetCurrentUser().Returns((User?)null);

        var result = await _sut.Handle(new ListMyTenantsQuery(), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Unauthorized);
    }

    [Fact]
    public async Task Handle_Memberships_MarksActiveFromCurrentTenant()
    {
        var user = new User(UserId, HomeTenantId, "member", "member@example.com", true);
        _userContext.GetCurrentUser().Returns(user);
        _userService.ListMembershipTenantIdsAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<long>>([HomeTenantId, OtherTenantId]));

        Tenant home = new("Home", HomeSlug) { Id = HomeTenantId };
        Tenant other = new("Other", OtherSlug) { Id = OtherTenantId };
        _tenantRepository
            .ListAsync(Arg.Any<TenantSpecifications.ByIdsSpec>(), Arg.Any<CancellationToken>())
            .Returns([other, home]);

        var result = await _sut.Handle(new ListMyTenantsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Should().Contain(dto => dto.Id == HomeTenantId && dto.IsActive);
        result.Value.Items.Should().Contain(dto => dto.Id == OtherTenantId && !dto.IsActive);
    }
}
