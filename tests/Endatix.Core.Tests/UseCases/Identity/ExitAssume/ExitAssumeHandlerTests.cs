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
using Endatix.Core.UseCases.Identity.ExitAssume;

namespace Endatix.Core.Tests.UseCases.Identity.ExitAssume;

public class ExitAssumeHandlerTests
{
    private const long ActorId = 7;
    private const long HomeTenantId = 1;
    private const long AssumedTenantId = 99;
    private const string ValidSlug = "xK9mP2qR8vNw";

    private readonly IUserContext _userContext = Substitute.For<IUserContext>();
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly IRepository<Tenant> _tenantRepository = Substitute.For<IRepository<Tenant>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IUserTokenService _tokenService = Substitute.For<IUserTokenService>();
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly ICurrentUserAuthorizationService _authorizationService = Substitute.For<ICurrentUserAuthorizationService>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly ExitAssumeHandler _sut;

    public ExitAssumeHandlerTests()
    {
        _dateTimeProvider.Now.Returns(new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero));
        _sut = new ExitAssumeHandler(
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
    public async Task Handle_NotAssumed_ReturnsInvalid()
    {
        var user = new User(ActorId, AssumedTenantId, "admin", "admin@example.com", true);
        _userContext.GetCurrentUser().Returns(user);
        _userContext.GetActorUserId().Returns((long?)null);

        var result = await _sut.Handle(new ExitAssumeCommand(), CancellationToken.None);

        result.IsInvalid().Should().BeTrue();
        _tokenService.DidNotReceive().IssueRefreshToken();
    }

    [Fact]
    public async Task Handle_AssumedSession_RemintsHomeTenantWithoutAct()
    {
        var sessionUser = new User(ActorId, AssumedTenantId, "admin", "admin@example.com", true);
        var homeUser = new User(ActorId, HomeTenantId, "admin", "admin@example.com", true);
        Tenant assumedTenant = new("Acme", ValidSlug) { Id = AssumedTenantId };
        _userContext.GetCurrentUser().Returns(sessionUser);
        _userContext.GetActorUserId().Returns(ActorId);
        _userService.GetUserAsync(ActorId, Arg.Any<CancellationToken>()).Returns(Result.Success(homeUser));
        _tenantRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.ByIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(assumedTenant);
        var access = new TokenDto("home-access", DateTime.UtcNow.AddMinutes(30));
        var refresh = new TokenDto("refresh", DateTime.UtcNow.AddDays(7));
        _tokenService.IssueAccessToken(homeUser).Returns(access);
        _tokenService.IssueRefreshToken().Returns(refresh);
        _authService.StoreRefreshToken(ActorId, refresh.Token, refresh.ExpireAt, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var result = await _sut.Handle(new ExitAssumeCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be(access);
        assumedTenant.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TenantContextChangedEvent>()
            .Which.ChangeKind.Should().Be(TenantContextChangedEvent.Exited);
        _tokenService.DidNotReceive().IssueAccessToken(homeUser, Arg.Any<AccessTokenIssueOptions>());
    }
}
