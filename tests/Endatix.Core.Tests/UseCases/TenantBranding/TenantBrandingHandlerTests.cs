using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Branding;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.TenantBranding;
using Endatix.Core.UseCases.TenantBranding.Publishing;
using Endatix.Core.UseCases.TenantBranding.Restoring;
using Endatix.Core.Versioning;
using TenantBrandingEntity = Endatix.Core.Entities.TenantBranding;

namespace Endatix.Core.Tests.UseCases.TenantBranding;

public sealed class TenantBrandingHandlerTests
{
    private const long TENANT_ID = 42;
    private const long USER_ID = 7;
    private const string BLUE = """{"version":1,"tokens":{"color":{"primary":{"$type":"color","$value":"#0052d1"}}}}""";
    private const string GREEN = """{"version":1,"tokens":{"color":{"primary":{"$type":"color","$value":"#16a34a"}}}}""";

    private static readonly DateTimeOffset _publishTime = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _rollbackTime = _publishTime.AddHours(1);

    private readonly ITenantBrandingRepository _repository = Substitute.For<ITenantBrandingRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ITenantEntitlements _entitlements = Substitute.For<ITenantEntitlements>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly Dictionary<long, TenantBrandingVersion> _saved = [];
    private readonly PublishTenantBrandingHandler _publish;
    private readonly RestoreTenantBrandingHandler _restore;

    public TenantBrandingHandlerTests()
    {
        _tenantContext.TenantId.Returns(TENANT_ID);
        _entitlements.IsEntitledAsync(TENANT_ID, EntitlementNames.BRANDING, Arg.Any<CancellationToken>()).Returns(true);
        _dateTimeProvider.Now.Returns(_publishTime);
        _repository.SaveOrConcedeAsync(Arg.Any<TenantBrandingEntity>(), Arg.Any<TenantBrandingVersion>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var version = call.Arg<TenantBrandingVersion>();
                _saved[version.Number] = version;
                return true;
            });
        _repository.FindVersionAsync(TENANT_ID, Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(call => _saved.GetValueOrDefault(call.ArgAt<long>(1)));
        var access = new TenantBrandingAccess(_tenantContext, _entitlements, new());
        _publish = new PublishTenantBrandingHandler(_repository, access, _dateTimeProvider);
        _restore = new RestoreTenantBrandingHandler(_repository, access, _dateTimeProvider);
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Publish_NotEntitled_IsForbiddenAndReadsNothing()
    {
        // Arrange
        _entitlements.IsEntitledAsync(TENANT_ID, EntitlementNames.BRANDING, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var result = await _publish.Handle(new PublishTenantBrandingCommand(BLUE, 0), Cancellation);

        // Assert
        result.Status.Should().Be(ResultStatus.Forbidden);
        await _repository.DidNotReceive().FindAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rollback_NotEntitled_IsForbidden()
    {
        // Arrange
        _entitlements.IsEntitledAsync(TENANT_ID, EntitlementNames.BRANDING, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var rollback = await _restore.Handle(new RollbackTenantBrandingCommand(1), Cancellation);
        var restore = await _restore.Handle(new RestoreTenantBrandingCommand(1, 1), Cancellation);
        var reset = await _restore.Handle(new ResetTenantBrandingCommand(1), Cancellation);

        // Assert
        rollback.Status.Should().Be(ResultStatus.Forbidden);
        restore.Status.Should().Be(ResultStatus.Forbidden);
        reset.Status.Should().Be(ResultStatus.Forbidden);
    }

    [Fact]
    public async Task NoTenant_IsUnauthorizedAndReadsNothing()
    {
        // Arrange
        _tenantContext.TenantId.Returns(0);

        // Act
        var publish = await _publish.Handle(new PublishTenantBrandingCommand(BLUE, 0), Cancellation);
        var rollback = await _restore.Handle(new RollbackTenantBrandingCommand(1), Cancellation);
        var reset = await _restore.Handle(new ResetTenantBrandingCommand(1), Cancellation);

        // Assert
        publish.Status.Should().Be(ResultStatus.Unauthorized);
        rollback.Status.Should().Be(ResultStatus.Unauthorized);
        reset.Status.Should().Be(ResultStatus.Unauthorized);
        await _repository.DidNotReceive().FindAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Publish_InvalidOverride_ReturnsTheProblemList()
    {
        // Act
        var result = await _publish.Handle(
            new PublishTenantBrandingCommand("""{"version":1,"assets":{"icon":"t/43/brand/i.png"}}""", 0),
            Cancellation);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle().Which.ErrorCode.Should().Be(BrandOverrideProblems.INVALID_ASSET_KEY);
    }

    [Fact]
    public async Task Publish_NullOverride_IsRequiredAndReadsNothing()
    {
        // Act
        var result = await _publish.Handle(new PublishTenantBrandingCommand(null!, 0), Cancellation);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        var error = result.ValidationErrors.Should().ContainSingle().Subject;
        error.ErrorCode.Should().Be(BrandOverrideProblems.REQUIRED);
        error.Identifier.Should().BeEmpty();
        await _repository.DidNotReceive().FindAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Publish_FirstTime_SavesTheRowAndVersionOne()
    {
        // Arrange
        NothingStored();

        // Act
        var result = await _publish.Handle(new PublishTenantBrandingCommand(BLUE, TenantBrandingEntity.UNSAVED_VERSION, USER_ID), Cancellation);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be(1);
        result.Value.PublishedNumber.Should().Be(1);
        result.Value.PublishedAt.Should().Be(_publishTime.UtcDateTime);
        await _repository.Received(1).SaveOrConcedeAsync(
            Arg.Is<TenantBrandingEntity>(branding => branding.TenantId == TENANT_ID && branding.Version == 1),
            Arg.Is<TenantBrandingVersion>(version => version.Number == 1 && version.CreatedBy == USER_ID
                && version.CreatedAt == _publishTime.UtcDateTime && version.Source == ContentVersionSources.PUBLISH),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Publish_SameValueAgain_SavesNothing()
    {
        // Arrange
        var branding = Stored(new TenantBrandingEntity(TENANT_ID));
        await _publish.Handle(new PublishTenantBrandingCommand(BLUE, 0), Cancellation);
        _repository.ClearReceivedCalls();

        // Act
        var result = await _publish.Handle(new PublishTenantBrandingCommand(BLUE, 0), Cancellation);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be(1);
        branding.Version.Should().Be(1);
        await _repository.DidNotReceive().SaveOrConcedeAsync(
            Arg.Any<TenantBrandingEntity>(), Arg.Any<TenantBrandingVersion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Publish_StaleVersion_IsAConflict()
    {
        // Arrange
        Stored(new TenantBrandingEntity(TENANT_ID));
        await _publish.Handle(new PublishTenantBrandingCommand(BLUE, 0), Cancellation);

        // Act
        var result = await _publish.Handle(new PublishTenantBrandingCommand(GREEN, 0), Cancellation);

        // Assert
        result.Status.Should().Be(ResultStatus.Conflict);
    }

    [Fact]
    public async Task Publish_LosesTheRaceAtSave_IsAConflict()
    {
        // Arrange
        NothingStored();
        _repository.SaveOrConcedeAsync(Arg.Any<TenantBrandingEntity>(), Arg.Any<TenantBrandingVersion>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _publish.Handle(new PublishTenantBrandingCommand(BLUE, 0), Cancellation);

        // Assert
        result.Status.Should().Be(ResultStatus.Conflict);
    }

    [Fact]
    public async Task Rollback_AtCurrentVersion_RestoresThePreviousVersionAsANewOne()
    {
        // Arrange
        var branding = Stored(new TenantBrandingEntity(TENANT_ID));
        await _publish.Handle(new PublishTenantBrandingCommand(BLUE, 0), Cancellation);
        await _publish.Handle(new PublishTenantBrandingCommand(GREEN, 1), Cancellation);
        _repository.ClearReceivedCalls();
        _dateTimeProvider.Now.Returns(_rollbackTime);

        // Act
        var result = await _restore.Handle(new RollbackTenantBrandingCommand(2, USER_ID), Cancellation);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be(3);
        result.Value.PublishedNumber.Should().Be(3);
        result.Value.PublishedAt.Should().Be(_rollbackTime.UtcDateTime);
        result.Value.PublishedJson.Should().Be(branding.PublishedJson).And.Contain("#0052d1");
        await _repository.Received(1).SaveOrConcedeAsync(
            branding,
            Arg.Is<TenantBrandingVersion>(version => version.Number == 3 && version.RestoredFromNumber == 1
                && version.Source == ContentVersionSources.RESTORE && version.CreatedBy == USER_ID),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Restore_ANamedVersion_PublishesItAgain()
    {
        // Arrange
        Stored(new TenantBrandingEntity(TENANT_ID));
        await _publish.Handle(new PublishTenantBrandingCommand(BLUE, 0), Cancellation);
        await _publish.Handle(new PublishTenantBrandingCommand(GREEN, 1), Cancellation);
        await _publish.Handle(new PublishTenantBrandingCommand(BLUE.Replace("#0052d1", "#8a2be2", StringComparison.Ordinal), 2), Cancellation);

        // Act
        var result = await _restore.Handle(new RestoreTenantBrandingCommand(1, 3), Cancellation);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PublishedNumber.Should().Be(4);
        result.Value.PublishedJson.Should().Contain("#0052d1");
        _saved[4].RestoredFromNumber.Should().Be(1);
    }

    [Fact]
    public async Task Restore_Retried_WithAStaleVersion_ChangesNothing()
    {
        // Arrange: restoring version 1 made it live at version 3; the retry still names version 2
        var branding = Stored(new TenantBrandingEntity(TENANT_ID));
        await _publish.Handle(new PublishTenantBrandingCommand(BLUE, 0), Cancellation);
        await _publish.Handle(new PublishTenantBrandingCommand(GREEN, 1), Cancellation);
        await _restore.Handle(new RestoreTenantBrandingCommand(1, 2), Cancellation);
        _repository.ClearReceivedCalls();

        // Act
        var result = await _restore.Handle(new RestoreTenantBrandingCommand(1, 2), Cancellation);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be(3);
        result.Value.PublishedNumber.Should().Be(3);
        branding.Version.Should().Be(3);
        await _repository.DidNotReceive().SaveOrConcedeAsync(
            Arg.Any<TenantBrandingEntity>(), Arg.Any<TenantBrandingVersion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Restore_StaleVersion_WhenTheContentIsNotLive_IsAConflict()
    {
        // Arrange
        var branding = Stored(new TenantBrandingEntity(TENANT_ID));
        await _publish.Handle(new PublishTenantBrandingCommand(BLUE, 0), Cancellation);
        await _publish.Handle(new PublishTenantBrandingCommand(GREEN, 1), Cancellation);
        _repository.ClearReceivedCalls();

        // Act
        var result = await _restore.Handle(new RestoreTenantBrandingCommand(1, 1), Cancellation);

        // Assert
        result.Status.Should().Be(ResultStatus.Conflict);
        branding.Version.Should().Be(2);
        await _repository.DidNotReceive().SaveOrConcedeAsync(
            Arg.Any<TenantBrandingEntity>(), Arg.Any<TenantBrandingVersion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Restore_ANumberNotInTheHistory_IsInvalidAndSavesNothing()
    {
        // Arrange
        Stored(new TenantBrandingEntity(TENANT_ID));
        await _publish.Handle(new PublishTenantBrandingCommand(BLUE, 0), Cancellation);
        _repository.ClearReceivedCalls();

        // Act
        var result = await _restore.Handle(new RestoreTenantBrandingCommand(5, 1), Cancellation);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle().Which.ErrorCode.Should().Be(BrandOverrideProblems.UNKNOWN_VERSION);
        await _repository.DidNotReceive().FindVersionAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Restore_VersionGoneSinceTheRead_IsAConflict()
    {
        // Arrange: the row says version 1 exists, the version read finds none
        Stored(new TenantBrandingEntity(TENANT_ID));
        await _publish.Handle(new PublishTenantBrandingCommand(BLUE, 0), Cancellation);
        await _publish.Handle(new PublishTenantBrandingCommand(GREEN, 1), Cancellation);
        _saved.Clear();

        // Act
        var result = await _restore.Handle(new RollbackTenantBrandingCommand(2), Cancellation);

        // Assert
        result.Status.Should().Be(ResultStatus.Conflict);
    }

    [Fact]
    public async Task Rollback_NothingStored_IsInvalid()
    {
        // Arrange
        NothingStored();

        // Act
        var result = await _restore.Handle(new RollbackTenantBrandingCommand(0), Cancellation);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle().Which.ErrorCode.Should().Be(BrandOverrideProblems.NO_PREVIOUS_VERSION);
        await _repository.DidNotReceive().SaveOrConcedeAsync(
            Arg.Any<TenantBrandingEntity>(), Arg.Any<TenantBrandingVersion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reset_ALiveBrand_SavesAResetVersionWithTheCaller()
    {
        // Arrange
        var branding = Stored(new TenantBrandingEntity(TENANT_ID));
        await _publish.Handle(new PublishTenantBrandingCommand(BLUE, 0), Cancellation);
        _repository.ClearReceivedCalls();
        _dateTimeProvider.Now.Returns(_rollbackTime);

        // Act
        var result = await _restore.Handle(new ResetTenantBrandingCommand(1, USER_ID), Cancellation);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new TenantBrandingDto(null, 2, 2, _rollbackTime.UtcDateTime));
        await _repository.Received(1).SaveOrConcedeAsync(
            branding,
            Arg.Is<TenantBrandingVersion>(version => version.Number == 2 && version.Source == ContentVersionSources.RESET
                && version.CreatedBy == USER_ID && version.CreatedAt == _rollbackTime.UtcDateTime),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reset_NothingStored_SavesNothing()
    {
        // Arrange
        NothingStored();

        // Act
        var result = await _restore.Handle(new ResetTenantBrandingCommand(0), Cancellation);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new TenantBrandingDto(null, null, TenantBrandingEntity.UNSAVED_VERSION, null));
        await _repository.DidNotReceive().SaveOrConcedeAsync(
            Arg.Any<TenantBrandingEntity>(), Arg.Any<TenantBrandingVersion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reset_StaleVersion_IsAConflict()
    {
        // Arrange
        Stored(new TenantBrandingEntity(TENANT_ID));
        await _publish.Handle(new PublishTenantBrandingCommand(BLUE, 0), Cancellation);
        await _publish.Handle(new PublishTenantBrandingCommand(GREEN, 1), Cancellation);

        // Act
        var result = await _restore.Handle(new ResetTenantBrandingCommand(1), Cancellation);

        // Assert
        result.Status.Should().Be(ResultStatus.Conflict);
    }

    [Fact]
    public async Task Rollback_AfterAReset_PublishesTheBrandBeforeIt()
    {
        // Arrange
        Stored(new TenantBrandingEntity(TENANT_ID));
        await _publish.Handle(new PublishTenantBrandingCommand(BLUE, 0), Cancellation);
        await _restore.Handle(new ResetTenantBrandingCommand(1), Cancellation);

        // Act
        var result = await _restore.Handle(new RollbackTenantBrandingCommand(2), Cancellation);

        // Assert
        result.Value.PublishedNumber.Should().Be(3);
        result.Value.PublishedJson.Should().Contain("#0052d1");
    }

    private TenantBrandingEntity Stored(TenantBrandingEntity branding)
    {
        _repository.FindAsync(TENANT_ID, Arg.Any<CancellationToken>()).Returns(branding);
        return branding;
    }

    private void NothingStored() =>
        _repository.FindAsync(TENANT_ID, Arg.Any<CancellationToken>()).Returns((TenantBrandingEntity?)null);
}
