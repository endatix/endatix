using Endatix.Core.Abstractions;
using Endatix.Core.Branding;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.TenantBranding.Publishing;
using Endatix.Core.UseCases.TenantBranding.Rollback;
using TenantBrandingEntity = Endatix.Core.Entities.TenantBranding;

namespace Endatix.Core.Tests.UseCases.TenantBranding;

public sealed class TenantBrandingHandlerTests
{
    private const long TENANT_ID = 42;
    private const string BLUE = """{"version":1,"tokens":{"color":{"primary":{"$type":"color","$value":"#0052d1"}}}}""";
    private const string GREEN = """{"version":1,"tokens":{"color":{"primary":{"$type":"color","$value":"#16a34a"}}}}""";

    private readonly IRepository<TenantBrandingEntity> _repository = Substitute.For<IRepository<TenantBrandingEntity>>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ITenantEntitlements _entitlements = Substitute.For<ITenantEntitlements>();
    private readonly PublishTenantBrandingHandler _publish;
    private readonly RollbackTenantBrandingHandler _rollback;

    public TenantBrandingHandlerTests()
    {
        _tenantContext.TenantId.Returns(TENANT_ID);
        _entitlements.HasAsync(TENANT_ID, Entitlements.BRANDING, Arg.Any<CancellationToken>()).Returns(true);
        _publish = new PublishTenantBrandingHandler(_repository, _tenantContext, _entitlements);
        _rollback = new RollbackTenantBrandingHandler(_repository, _tenantContext, _entitlements);
    }

    [Fact]
    public async Task Publish_NotEntitled_IsForbiddenAndWritesNothing()
    {
        // Arrange
        _entitlements.HasAsync(TENANT_ID, Entitlements.BRANDING, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var result = await _publish.Handle(new PublishTenantBrandingCommand(BLUE, 0), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Forbidden);
        await _repository.DidNotReceive().FirstOrDefaultAsync(Arg.Any<TenantBrandingByTenantIdSpec>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rollback_NotEntitled_IsForbidden()
    {
        // Arrange
        _entitlements.HasAsync(TENANT_ID, Entitlements.BRANDING, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var result = await _rollback.Handle(new RollbackTenantBrandingCommand(1), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Forbidden);
    }

    [Fact]
    public async Task NoTenant_IsUnauthorizedAndReadsNothing()
    {
        // Arrange
        _tenantContext.TenantId.Returns(0);

        // Act
        var publish = await _publish.Handle(new PublishTenantBrandingCommand(BLUE, 0), TestContext.Current.CancellationToken);
        var rollback = await _rollback.Handle(new RollbackTenantBrandingCommand(1), TestContext.Current.CancellationToken);

        // Assert
        publish.Status.Should().Be(ResultStatus.Unauthorized);
        rollback.Status.Should().Be(ResultStatus.Unauthorized);
        await _repository.DidNotReceive().FirstOrDefaultAsync(Arg.Any<TenantBrandingByTenantIdSpec>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Publish_InvalidOverride_ReturnsTheProblemList()
    {
        // Act
        var result = await _publish.Handle(
            new PublishTenantBrandingCommand("""{"version":1,"assets":{"icon":"t/43/brand/i.png"}}""", 0),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle().Which.ErrorCode.Should().Be(BrandOverrideProblems.INVALID_ASSET_KEY);
    }

    [Fact]
    public async Task Publish_FirstTime_AddsTheRow()
    {
        // Arrange
        NothingStored();

        // Act
        var result = await _publish.Handle(new PublishTenantBrandingCommand(BLUE, 0), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Revision.Should().Be(1);
        result.Value.PublishedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        await _repository.Received(1).AddAsync(
            Arg.Is<TenantBrandingEntity>(branding => branding.TenantId == TENANT_ID && branding.Revision == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Publish_SameValueAgain_SavesNothing()
    {
        // Arrange
        var branding = Stored(new TenantBrandingEntity(TENANT_ID));
        await _publish.Handle(new PublishTenantBrandingCommand(BLUE, 0), TestContext.Current.CancellationToken);
        _repository.ClearReceivedCalls();

        // Act
        var result = await _publish.Handle(new PublishTenantBrandingCommand(BLUE, 0), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Revision.Should().Be(1);
        branding.Revision.Should().Be(1);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<TenantBrandingEntity>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().AddAsync(Arg.Any<TenantBrandingEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Publish_StaleRevision_IsAConflict()
    {
        // Arrange
        Stored(new TenantBrandingEntity(TENANT_ID));
        await _publish.Handle(new PublishTenantBrandingCommand(BLUE, 0), TestContext.Current.CancellationToken);

        // Act
        var result = await _publish.Handle(new PublishTenantBrandingCommand(GREEN, 0), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Conflict);
    }

    [Fact]
    public async Task Publish_LosesTheRaceAtSave_IsAConflict()
    {
        // Arrange
        Stored(new TenantBrandingEntity(TENANT_ID));
        _repository.UpdateAsync(Arg.Any<TenantBrandingEntity>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new TenantBrandingConflictException("lost"));

        // Act
        var result = await _publish.Handle(new PublishTenantBrandingCommand(BLUE, 0), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Conflict);
    }

    [Fact]
    public async Task Rollback_AtCurrentRevision_SwapsAndSaves()
    {
        // Arrange
        var branding = Stored(new TenantBrandingEntity(TENANT_ID));
        await _publish.Handle(new PublishTenantBrandingCommand(BLUE, 0), TestContext.Current.CancellationToken);
        await _publish.Handle(new PublishTenantBrandingCommand(GREEN, 1), TestContext.Current.CancellationToken);
        _repository.ClearReceivedCalls();

        // Act
        var result = await _rollback.Handle(new RollbackTenantBrandingCommand(2), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Revision.Should().Be(3);
        result.Value.PublishedJson.Should().Be(branding.PublishedJson).And.Contain("#0052d1");
        await _repository.Received(1).UpdateAsync(branding, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rollback_NothingStored_IsInvalid()
    {
        // Arrange
        NothingStored();

        // Act
        var result = await _rollback.Handle(new RollbackTenantBrandingCommand(0), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        await _repository.DidNotReceive().AddAsync(Arg.Any<TenantBrandingEntity>(), Arg.Any<CancellationToken>());
    }

    private TenantBrandingEntity Stored(TenantBrandingEntity branding)
    {
        _repository.FirstOrDefaultAsync(Arg.Any<TenantBrandingByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(branding);
        return branding;
    }

    private void NothingStored() =>
        _repository.FirstOrDefaultAsync(Arg.Any<TenantBrandingByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns((TenantBrandingEntity?)null);
}
