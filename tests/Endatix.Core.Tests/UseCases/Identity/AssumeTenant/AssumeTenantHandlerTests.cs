using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
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
    private const string ValidSlug = "xk9mp2qr";

    private readonly IUserContext _userContext = Substitute.For<IUserContext>();
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly IRepository<Tenant> _tenantRepository = Substitute.For<IRepository<Tenant>>();
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
        var result = await _sut.Handle(new AssumeTenantCommand(TargetTenantId), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Unauthorized);
    }

    [Fact]
    public async Task Handle_AlreadyAssumedSession_ReturnsInvalidWithoutIssuingTokens()
    {
        // Arrange
        ArrangeActor();
        _userContext.GetActorUserId().Returns(ActorId);

        // Act
        var result = await _sut.Handle(new AssumeTenantCommand(TargetTenantId), CancellationToken.None);

        // Assert
        result.IsInvalid().Should().BeTrue();
        _tokenService.DidNotReceive().IssueRefreshToken();
    }

    [Fact]
    public async Task Handle_TenantMissing_ReturnsNotFound()
    {
        // Arrange
        ArrangeActor();
        ArrangeTenant(null);

        // Act
        var result = await _sut.Handle(new AssumeTenantCommand(TargetTenantId), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        _tokenService.DidNotReceive().IssueRefreshToken();
    }

    [Fact]
    public async Task Handle_RefreshTokenNotStored_ReturnsErrorWithoutRaisingEvent()
    {
        // Arrange
        var user = ArrangeActor();
        var tenant = ArrangeTenant(NewTargetTenant());
        ArrangeTokens(user, tenant!);
        _authService.StoreRefreshToken(ActorId, Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Result.Error());

        // Act
        var result = await _sut.Handle(new AssumeTenantCommand(TargetTenantId), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Error);
        tenant!.DomainEvents.Should().BeEmpty();
        await _tenantRepository.DidNotReceive().UpdateAsync(Arg.Any<Tenant>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidTenant_IssuesAssumedTokensAndRaisesEvent()
    {
        // Arrange
        var user = ArrangeActor();
        var tenant = ArrangeTenant(NewTargetTenant());
        var (access, _) = ArrangeTokens(user, tenant!);

        // Act
        var result = await _sut.Handle(new AssumeTenantCommand(TargetTenantId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be(access);
        var domainEvent = tenant!.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TenantContextChangedEvent>().Subject;
        domainEvent.ChangeKind.Should().Be(TenantContextChangedEvent.Assumed);
        domainEvent.FromTenantId.Should().Be(HomeTenantId);
        domainEvent.ToTenantId.Should().Be(TargetTenantId);
        await _tenantRepository.Received(1).UpdateAsync(tenant, Arg.Any<CancellationToken>());
        await _authorizationService.Received(1).InvalidateAuthorizationDataCacheAsync(
            ActorId.ToString(), TargetTenantId, Arg.Any<CancellationToken>());
    }

    private User ArrangeActor()
    {
        var user = new User(ActorId, HomeTenantId, "admin", "admin@example.com", true);
        _userContext.GetCurrentUser().Returns(user);
        _userContext.GetActorUserId().Returns((long?)null);
        _userService.GetUserAsync(ActorId, Arg.Any<CancellationToken>()).Returns(Result.Success(user));
        return user;
    }

    private static Tenant NewTargetTenant() => new("Acme", ValidSlug) { Id = TargetTenantId };

    private Tenant? ArrangeTenant(Tenant? tenant)
    {
        _tenantRepository
            .SingleOrDefaultAsync(Arg.Any<TenantSpecifications.ByIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(tenant);
        return tenant;
    }

    private (TokenDto Access, TokenDto Refresh) ArrangeTokens(User user, Tenant tenant)
    {
        var access = new TokenDto("assumed-access", DateTime.UtcNow.AddMinutes(15));
        var refresh = new TokenDto("refresh", DateTime.UtcNow.AddDays(7));
        _tokenService.IssueAccessToken(user, Arg.Is<AccessTokenIssueOptions>(
                options => options.TenantId == tenant.Id
                    && options.ActorUserId == ActorId
                    && options.AccessExpiryMinutes == AssumeTenantSession.AccessExpiryMinutes))
            .Returns(access);
        _tokenService.IssueRefreshToken().Returns(refresh);
        _authService.StoreRefreshToken(ActorId, refresh.Token, refresh.ExpireAt, Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        return (access, refresh);
    }
}
