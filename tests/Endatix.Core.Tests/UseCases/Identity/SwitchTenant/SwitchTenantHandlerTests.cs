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
using Endatix.Core.UseCases.Identity.SwitchTenant;

namespace Endatix.Core.Tests.UseCases.Identity.SwitchTenant;

public class SwitchTenantHandlerTests
{
    private const long UserId = 7;
    private const long HomeTenantId = 10;
    private const long TargetTenantId = 20;
    private const string ValidSlug = "xK9mP2qR8vNw";

    private readonly IUserContext _userContext = Substitute.For<IUserContext>();
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly IRepository<Tenant> _tenantRepository = Substitute.For<IRepository<Tenant>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IUserTokenService _tokenService = Substitute.For<IUserTokenService>();
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly ICurrentUserAuthorizationService _authorizationService = Substitute.For<ICurrentUserAuthorizationService>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly SwitchTenantHandler _sut;

    public SwitchTenantHandlerTests()
    {
        _dateTimeProvider.Now.Returns(new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero));
        _sut = new SwitchTenantHandler(
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

        var result = await _sut.Handle(new SwitchTenantCommand(TargetTenantId), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Unauthorized);
    }

    [Fact]
    public async Task Handle_AssumedSession_ReturnsInvalid()
    {
        ArrangeActor();
        _userContext.GetActorUserId().Returns(UserId);

        var result = await _sut.Handle(new SwitchTenantCommand(TargetTenantId), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Invalid);
        await _userService.DidNotReceive().SetActiveTenantAsync(
            Arg.Any<long>(),
            Arg.Any<long>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithoutMembership_ReturnsForbidden()
    {
        ArrangeActor();
        _userService.SetActiveTenantAsync(UserId, TargetTenantId, Arg.Any<CancellationToken>())
            .Returns(Result<User>.Forbidden("User does not belong to the requested tenant."));

        var result = await _sut.Handle(new SwitchTenantCommand(TargetTenantId), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Forbidden);
        _tokenService.DidNotReceive().IssueRefreshToken();
    }

    [Fact]
    public async Task Handle_WithMembership_IssuesTokensAndRaisesSwitchedEvent()
    {
        ArrangeActor();
        var switchedUser = new User(UserId, TargetTenantId, "member", "member@example.com", true);
        _userService.SetActiveTenantAsync(UserId, TargetTenantId, Arg.Any<CancellationToken>())
            .Returns(Result.Success(switchedUser));
        Tenant tenant = new("Acme", ValidSlug) { Id = TargetTenantId };
        _tenantRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.ByIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(tenant);
        var access = new TokenDto("switched-access", DateTime.UtcNow.AddMinutes(30));
        var refresh = new TokenDto("refresh", DateTime.UtcNow.AddDays(7));
        _tokenService.IssueAccessToken(switchedUser).Returns(access);
        _tokenService.IssueRefreshToken().Returns(refresh);
        _authService.StoreRefreshToken(UserId, refresh.Token, refresh.ExpireAt, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var result = await _sut.Handle(new SwitchTenantCommand(TargetTenantId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be(access);
        tenant.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TenantContextChangedEvent>()
            .Which.ChangeKind.Should().Be(TenantContextChangedEvent.KindSwitched);
    }

    private User ArrangeActor()
    {
        var user = new User(UserId, HomeTenantId, "member", "member@example.com", true);
        _userContext.GetCurrentUser().Returns(user);
        return user;
    }
}
