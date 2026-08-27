using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Identity;
using Endatix.Core.UseCases.Identity.AssumeTenant;

namespace Endatix.Core.Tests.UseCases.Identity.AssumeTenant;

public class AssumeTenantHandlerTests
{
    private const long ActorId = 7;
    private const long HomeTenantId = 1;
    private const long TargetTenantId = 99;
    private const string ValidSlug = "xK9mP2qR";

    private readonly IUserContext _userContext = Substitute.For<IUserContext>();
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly IRepository<Tenant> _tenantRepository = Substitute.For<IRepository<Tenant>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IUserTokenService _tokenService = Substitute.For<IUserTokenService>();
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly ICurrentUserAuthorizationService _authorizationService = Substitute.For<ICurrentUserAuthorizationService>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly AssumeTenantHandler _sut;

    public AssumeTenantHandlerTests()
    {
        _dateTimeProvider.Now.Returns(new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero));
        _sut = new AssumeTenantHandler(
            _userContext,
            _userService,
            _tenantRepository,
            _unitOfWork,
            _tokenService,
            _authService,
            _authorizationService,
            _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_Anonymous_ReturnsUnauthorized()
    {
        _userContext.GetCurrentUser().Returns((User?)null);

        var result = await _sut.Handle(new AssumeTenantCommand(TargetTenantId), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Unauthorized);
    }

    [Fact]
    public async Task Handle_TenantMissing_ReturnsNotFound()
    {
        ArrangeActor();
        _tenantRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.ByIdSpec>(), Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);

        var result = await _sut.Handle(new AssumeTenantCommand(TargetTenantId), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
        _tokenService.DidNotReceive().IssueRefreshToken();
    }

    [Fact]
    public async Task Handle_ValidTenant_IssuesAssumedTokensAndRaisesEvent()
    {
        var user = ArrangeActor();
        Tenant tenant = new("Acme", ValidSlug) { Id = TargetTenantId };
        _tenantRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.ByIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(tenant);
        var access = new TokenDto("assumed-access", DateTime.UtcNow.AddMinutes(15));
        var refresh = new TokenDto("refresh", DateTime.UtcNow.AddDays(7));
        _tokenService.IssueAccessToken(user, Arg.Is<AccessTokenIssueOptions>(
                options => options.TenantId == TargetTenantId
                    && options.ActorUserId == ActorId
                    && options.AccessExpiryMinutes == AssumeTenantSession.AccessExpiryMinutes))
            .Returns(access);
        _tokenService.IssueRefreshToken().Returns(refresh);
        _authService.StoreRefreshToken(ActorId, refresh.Token, refresh.ExpireAt, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var result = await _sut.Handle(new AssumeTenantCommand(TargetTenantId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be(access);
        tenant.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TenantContextChangedEvent>()
            .Which.ChangeKind.Should().Be(TenantContextChangedEvent.Assumed);
        await _authorizationService.Received().InvalidateAuthorizationDataCacheAsync(
            ActorId.ToString(), HomeTenantId, Arg.Any<CancellationToken>());
        await _authorizationService.Received().InvalidateAuthorizationDataCacheAsync(
            ActorId.ToString(), TargetTenantId, Arg.Any<CancellationToken>());
    }

    private User ArrangeActor()
    {
        var user = new User(ActorId, HomeTenantId, "admin", "admin@example.com", true);
        _userContext.GetCurrentUser().Returns(user);
        _userService.GetUserAsync(ActorId, Arg.Any<CancellationToken>()).Returns(Result.Success(user));
        return user;
    }
}
