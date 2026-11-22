using Endatix.Core.Branding;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Versioning;
using FluentAssertions;

namespace Endatix.Core.Tests.Entities;

public sealed class TenantBrandingTests
{
    private const long TENANT_ID = 42;
    private const long USER_ID = 7;
    private static readonly DateTime _now = new(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc);
    private static readonly ContentVersionStamp _stamp = new(_now, USER_ID);

    private static readonly BrandOverride _blue = Accent("#0052d1");
    private static readonly BrandOverride _green = Accent("#16a34a");

    [Fact]
    public void New_NothingPublished_AtTheUnsavedVersion()
    {
        // Act
        var branding = new TenantBranding(TENANT_ID);

        // Assert
        branding.TenantId.Should().Be(TENANT_ID);
        branding.Version.Should().Be(TenantBranding.UNSAVED_VERSION);
        branding.PublishedNumber.Should().BeNull();
        branding.PublishedJson.Should().BeNull();
        branding.PublishedAt.Should().BeNull();
    }

    [Fact]
    public void Publish_FirstTime_AppendsVersionOne()
    {
        // Arrange
        var branding = new TenantBranding(TENANT_ID);

        // Act
        var version = branding.Publish(_blue, TenantBranding.UNSAVED_VERSION, _stamp).Value;

        // Assert
        version.Should().BeEquivalentTo(new
        {
            TenantId = TENANT_ID,
            Number = 1L,
            Json = _blue.CanonicalJson,
            _blue.ContentHash,
            CreatedAt = _now,
            CreatedBy = (long?)USER_ID,
            Source = ContentVersionSources.PUBLISH,
            RestoredFromNumber = (long?)null,
        });
        branding.Version.Should().Be(1);
        branding.PublishedNumber.Should().Be(1);
        branding.PublishedJson.Should().Be(_blue.CanonicalJson);
        branding.PublishedAt.Should().Be(_now);
    }

    [Fact]
    public void Publish_AtCurrentVersion_AppendsTheNextNumber()
    {
        // Arrange
        var branding = new TenantBranding(TENANT_ID);
        branding.Publish(_blue, 0, _stamp);

        // Act
        var version = branding.Publish(_green, 1, _stamp with { At = _now.AddMinutes(1) }).Value;

        // Assert
        version!.Number.Should().Be(2);
        branding.PublishedNumber.Should().Be(2);
        branding.PublishedJson.Should().Be(_green.CanonicalJson);
        branding.Version.Should().Be(2);
        branding.PublishedAt.Should().Be(_now.AddMinutes(1));
    }

    [Fact]
    public void Publish_TheLiveValueAgain_ChangesNothingWhateverTheVersion()
    {
        // Arrange
        var branding = new TenantBranding(TENANT_ID);
        branding.Publish(_blue, 0, _stamp);
        branding.Publish(_green, 1, _stamp);

        // Act: a retry of the second publish, with the version it read before
        var result = branding.Publish(Accent("#16a34a"), 1, _stamp with { At = _now.AddMinutes(5) });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        branding.PublishedNumber.Should().Be(2);
        branding.Version.Should().Be(2);
        branding.PublishedAt.Should().Be(_now);
    }

    [Fact]
    public void Publish_TheLiveValueStoredWithKeysReordered_ChangesNothing()
    {
        // Arrange: a reload from jsonb, which reorders keys
        var branding = new TenantBranding(TENANT_ID);
        branding.Publish(_blue, 0, _stamp);
        typeof(TenantBranding).GetProperty(nameof(TenantBranding.PublishedJson))!
            .SetValue(branding, """{"version": 1, "tokens": {"color": {"primary": {"$value": "#0052d1", "$type": "color"}}}}""");

        // Act
        var result = branding.Publish(_blue, 0, _stamp);

        // Assert
        result.Value.Should().BeNull();
        branding.Version.Should().Be(1);
    }

    [Fact]
    public void Publish_StaleVersion_IsRefused()
    {
        // Arrange
        var branding = new TenantBranding(TENANT_ID);
        branding.Publish(_blue, 0, _stamp);

        // Act
        var result = branding.Publish(_green, 0, _stamp);

        // Assert
        result.Status.Should().Be(ResultStatus.Conflict);
        branding.PublishedJson.Should().Be(_blue.CanonicalJson);
        branding.Version.Should().Be(1);
    }

    [Fact]
    public void Publish_OverrideCheckedForAnotherTenant_Throws()
    {
        // Arrange
        var branding = new TenantBranding(TENANT_ID + 1);

        // Act
        var act = () => branding.Publish(_blue, 0, _stamp);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Restore_AnEarlierVersion_AppendsACopy()
    {
        // Arrange
        var branding = new TenantBranding(TENANT_ID);
        var first = branding.Publish(_blue, 0, _stamp).Value!;
        branding.Publish(_green, 1, _stamp);

        // Act
        var copy = branding.Restore(first, 2, _stamp with { At = _now.AddMinutes(1) }).Value;

        // Assert
        copy.Should().BeEquivalentTo(new
        {
            Number = 3L,
            Json = _blue.CanonicalJson,
            _blue.ContentHash,
            Source = ContentVersionSources.RESTORE,
            RestoredFromNumber = (long?)1,
        });
        branding.PublishedNumber.Should().Be(3);
        branding.PublishedJson.Should().Be(_blue.CanonicalJson);
        branding.Version.Should().Be(3);
        branding.PublishedAt.Should().Be(_now.AddMinutes(1));
    }

    [Fact]
    public void Restore_TheLiveContent_ChangesNothing()
    {
        // Arrange
        var branding = new TenantBranding(TENANT_ID);
        var first = branding.Publish(_blue, 0, _stamp).Value!;

        // Act
        var result = branding.Restore(first, 1, _stamp);

        // Assert
        result.Value.Should().BeNull();
        branding.Version.Should().Be(1);
    }

    [Fact]
    public void Restore_AVersionOfAnotherTenant_Throws()
    {
        // Arrange
        var other = new TenantBranding(TENANT_ID + 1);
        var foreign = other.Publish(Accent("#0052d1", TENANT_ID + 1), 0, _stamp).Value!;
        var branding = new TenantBranding(TENANT_ID);
        branding.Publish(_green, 0, _stamp);

        // Act
        var act = () => branding.Restore(foreign, 1, _stamp);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RestoreTarget_Rollback_IsTheVersionBeforeTheLiveOne()
    {
        // Arrange
        var branding = new TenantBranding(TENANT_ID);
        branding.Publish(_blue, 0, _stamp);
        branding.Publish(_green, 1, _stamp);

        // Act
        var target = branding.RestoreTarget(null);

        // Assert
        target.Value.Should().Be(1);
    }

    [Fact]
    public void Rollback_Twice_TogglesAndNeverRewinds()
    {
        // Arrange
        var branding = new TenantBranding(TENANT_ID);
        var history = new List<TenantBrandingVersion> { branding.Publish(_blue, 0, _stamp).Value!, branding.Publish(_green, 1, _stamp).Value! };

        // Act
        history.Add(branding.Restore(history[(int)branding.RestoreTarget(null).Value - 1], 2, _stamp).Value!);
        var afterFirst = branding.PublishedJson;
        history.Add(branding.Restore(history[(int)branding.RestoreTarget(null).Value - 1], 3, _stamp).Value!);

        // Assert
        afterFirst.Should().Be(_blue.CanonicalJson);
        branding.PublishedJson.Should().Be(_green.CanonicalJson);
        history.Select(version => $"{version.Number}<{version.RestoredFromNumber}")
            .Should().Equal("1<", "2<", "3<1", "4<2");
        branding.Version.Should().Be(4);
    }

    [Fact]
    public void Rollback_Retried_IsRefusedInsteadOfUndoingItself()
    {
        // Arrange
        var branding = new TenantBranding(TENANT_ID);
        var first = branding.Publish(_blue, 0, _stamp).Value!;
        var second = branding.Publish(_green, 1, _stamp).Value!;
        branding.Restore(first, 2, _stamp);

        // Act: the retry's target is now the version before the restored one
        var retry = branding.Restore(second, 2, _stamp);

        // Assert
        branding.RestoreTarget(null).Value.Should().Be(second.Number);
        retry.Status.Should().Be(ResultStatus.Conflict);
        branding.PublishedJson.Should().Be(_blue.CanonicalJson);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    public void RestoreTarget_RollbackWithNothingBeforeTheLiveVersion_IsInvalid(long published)
    {
        // Arrange
        var branding = new TenantBranding(TENANT_ID);
        if (published > 0)
        {
            branding.Publish(_blue, 0, _stamp);
        }

        // Act
        var result = branding.RestoreTarget(null);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle().Which.ErrorCode.Should().Be(BrandOverrideProblems.NO_PREVIOUS_VERSION);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(3L)]
    public void RestoreTarget_ANumberNotInTheHistory_IsInvalid(long number)
    {
        // Arrange
        var branding = new TenantBranding(TENANT_ID);
        branding.Publish(_blue, 0, _stamp);
        branding.Publish(_green, 1, _stamp);

        // Act
        var result = branding.RestoreTarget(number);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle().Which.ErrorCode.Should().Be(BrandOverrideProblems.UNKNOWN_VERSION);
    }

    [Fact]
    public void RestoreTarget_ANumberInTheHistory_IsThatNumber()
    {
        // Arrange
        var branding = new TenantBranding(TENANT_ID);
        branding.Publish(_blue, 0, _stamp);
        branding.Publish(_green, 1, _stamp);

        // Act & Assert
        branding.RestoreTarget(2).Value.Should().Be(2);
        branding.RestoreTarget(1).Value.Should().Be(1);
    }

    [Fact]
    public void Reset_ACustomBrand_AppendsAResetAndShowsTheDeploymentBrand()
    {
        // Arrange
        var branding = new TenantBranding(TENANT_ID);
        branding.Publish(_blue, 0, _stamp);

        // Act
        var reset = branding.Reset(1, _stamp with { At = _now.AddMinutes(1) }).Value;

        // Assert
        reset.Should().BeEquivalentTo(new
        {
            TenantId = TENANT_ID,
            Number = 2L,
            Json = TenantBranding.DEPLOYMENT_BRAND_JSON,
            CreatedAt = _now.AddMinutes(1),
            CreatedBy = (long?)USER_ID,
            Source = ContentVersionSources.RESET,
            RestoredFromNumber = (long?)null,
        });
        branding.PublishedNumber.Should().Be(2);
        branding.PublishedJson.Should().BeNull();
        branding.PublishedAt.Should().Be(_now.AddMinutes(1));
        branding.Version.Should().Be(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Reset_NothingPublished_ChangesNothingWhateverTheVersion(long expectedVersion)
    {
        // Arrange
        var branding = new TenantBranding(TENANT_ID);

        // Act
        var result = branding.Reset(expectedVersion, _stamp);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        branding.Version.Should().Be(TenantBranding.UNSAVED_VERSION);
        branding.PublishedNumber.Should().BeNull();
    }

    [Fact]
    public void Reset_Twice_ChangesNothingTheSecondTimeWhateverTheVersion()
    {
        // Arrange
        var branding = new TenantBranding(TENANT_ID);
        branding.Publish(_blue, 0, _stamp);
        branding.Reset(1, _stamp);

        // Act
        var result = branding.Reset(1, _stamp);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        branding.Version.Should().Be(2);
        branding.PublishedNumber.Should().Be(2);
    }

    [Fact]
    public void Reset_StaleVersion_IsRefused()
    {
        // Arrange
        var branding = new TenantBranding(TENANT_ID);
        branding.Publish(_blue, 0, _stamp);
        branding.Publish(_green, 1, _stamp);

        // Act
        var result = branding.Reset(1, _stamp);

        // Assert
        result.Status.Should().Be(ResultStatus.Conflict);
        branding.PublishedJson.Should().Be(_green.CanonicalJson);
        branding.Version.Should().Be(2);
    }

    [Fact]
    public void Rollback_AfterAReset_BringsTheBrandBack()
    {
        // Arrange
        var branding = new TenantBranding(TENANT_ID);
        var first = branding.Publish(_blue, 0, _stamp).Value!;
        branding.Reset(1, _stamp);

        // Act
        branding.RestoreTarget(null).Value.Should().Be(1);
        var copy = branding.Restore(first, 2, _stamp).Value;

        // Assert
        copy!.Number.Should().Be(3);
        copy.RestoredFromNumber.Should().Be(1);
        branding.PublishedJson.Should().Be(_blue.CanonicalJson);
        branding.PublishedNumber.Should().Be(3);
    }

    [Fact]
    public void Publish_AfterAReset_AppendsTheNextNumber()
    {
        // Arrange
        var branding = new TenantBranding(TENANT_ID);
        branding.Publish(_blue, 0, _stamp);
        branding.Reset(1, _stamp);

        // Act
        var version = branding.Publish(_blue, 2, _stamp).Value;

        // Assert
        version!.Number.Should().Be(3);
        branding.PublishedJson.Should().Be(_blue.CanonicalJson);
    }

    [Fact]
    public void Restore_AReset_ShowsTheDeploymentBrandAgain()
    {
        // Arrange
        var branding = new TenantBranding(TENANT_ID);
        branding.Publish(_blue, 0, _stamp);
        var reset = branding.Reset(1, _stamp).Value!;
        branding.Publish(_green, 2, _stamp);

        // Act
        var copy = branding.Restore(reset, 3, _stamp).Value;

        // Assert
        copy.Should().BeEquivalentTo(new
        {
            Number = 4L,
            Json = TenantBranding.DEPLOYMENT_BRAND_JSON,
            reset.ContentHash,
            Source = ContentVersionSources.RESTORE,
            RestoredFromNumber = (long?)2,
        });
        branding.PublishedJson.Should().BeNull();
        branding.PublishedNumber.Should().Be(4);
    }

    [Fact]
    public void Restore_AResetWhileTheDeploymentBrandIsLive_ChangesNothing()
    {
        // Arrange
        var branding = new TenantBranding(TENANT_ID);
        branding.Publish(_blue, 0, _stamp);
        var reset = branding.Reset(1, _stamp).Value!;

        // Act
        var result = branding.Restore(reset, 0, _stamp);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        branding.Version.Should().Be(2);
    }

    private static BrandOverride Accent(string hex, long tenantId = TENANT_ID) =>
        BrandOverride.Parse("{\"version\":1,\"tokens\":{\"color\":{\"primary\":{\"$type\":\"color\",\"$value\":\"" + hex + "\"}}}}", tenantId).Value;
}
