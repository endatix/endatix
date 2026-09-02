using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
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
    private const string ValidSlug = "xk9mp2qr";

    private readonly IUserContext _userContext = Substitute.For<IUserContext>();
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly IRepository<Tenant> _tenantRepository = Substitute.For<IRepository<Tenant>>();
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
            _tokenService,
            _authService,
            _authorizationService,
            _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_Anonymous_ReturnsUnauthorized()
    {
        // Arrange
        _userContext.GetCurrentUser().Returns((User?)null);

        // Act
        var result = await _sut.Handle(new ExitAssumeCommand(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Unauthorized);
    }

    [Fact]
    public async Task Handle_NotAssumed_ReturnsInvalid()
    {
        // Arrange
        _userContext.GetCurrentUser().Returns(new User(ActorId, AssumedTenantId, "admin", "admin@example.com", true));
        _userContext.GetActorUserId().Returns((long?)null);

        // Act
        var result = await _sut.Handle(new ExitAssumeCommand(), CancellationToken.None);

        // Assert
        result.IsInvalid().Should().BeTrue();
        _tokenService.DidNotReceive().IssueRefreshToken();
    }

    [Fact]
    public async Task Handle_AssumedTenantDeleted_StillRemintsHomeSession()
    {
        // Arrange
        var homeUser = ArrangeAssumedSession();
        ArrangeTenant(null);
        ArrangeTokens(homeUser);

        // Act
        var result = await _sut.Handle(new ExitAssumeCommand(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _tenantRepository.DidNotReceive().UpdateAsync(Arg.Any<Tenant>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AssumedSession_RemintsHomeTenantWithoutAct()
    {
        // Arrange
        var homeUser = ArrangeAssumedSession();
        var assumedTenant = ArrangeTenant(new Tenant("Acme", ValidSlug) { Id = AssumedTenantId });
        var access = ArrangeTokens(homeUser);

        // Act
        var result = await _sut.Handle(new ExitAssumeCommand(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be(access);
        var domainEvent = assumedTenant!.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TenantContextChangedEvent>().Subject;
        domainEvent.ChangeKind.Should().Be(TenantContextChangedEvent.Exited);
        domainEvent.FromTenantId.Should().Be(AssumedTenantId);
        domainEvent.ToTenantId.Should().Be(HomeTenantId);
        _tokenService.DidNotReceive().IssueAccessToken(homeUser, Arg.Any<AccessTokenIssueOptions>());
        await _authorizationService.Received(1).InvalidateAuthorizationDataCacheAsync(
            ActorId.ToString(), HomeTenantId, Arg.Any<CancellationToken>());
    }

    private User ArrangeAssumedSession()
    {
        var sessionUser = new User(ActorId, AssumedTenantId, "admin", "admin@example.com", true);
        var homeUser = new User(ActorId, HomeTenantId, "admin", "admin@example.com", true);
        _userContext.GetCurrentUser().Returns(sessionUser);
        _userContext.GetActorUserId().Returns(ActorId);
        _userService.GetUserAsync(ActorId, Arg.Any<CancellationToken>()).Returns(Result.Success(homeUser));
        return homeUser;
    }

    private Tenant? ArrangeTenant(Tenant? tenant)
    {
        _tenantRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.ByIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(tenant);
        return tenant;
    }

    private TokenDto ArrangeTokens(User homeUser)
    {
        var access = new TokenDto("home-access", DateTime.UtcNow.AddMinutes(30));
        var refresh = new TokenDto("refresh", DateTime.UtcNow.AddDays(7));
        _tokenService.IssueAccessToken(homeUser).Returns(access);
        _tokenService.IssueRefreshToken().Returns(refresh);
        _authService.StoreRefreshToken(ActorId, refresh.Token, refresh.ExpireAt, Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        return access;
    }
}
