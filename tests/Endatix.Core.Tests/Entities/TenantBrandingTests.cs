using Endatix.Core.Branding;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using FluentAssertions;

namespace Endatix.Core.Tests.Entities;

public sealed class TenantBrandingTests
{
    private const long TENANT_ID = 42;
    private static readonly DateTime _now = new(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc);

    private static readonly BrandOverride _blue = Accent("#0052d1");
    private static readonly BrandOverride _green = Accent("#16a34a");

    [Fact]
    public void New_NothingPublished_AtRevisionZero()
    {
        // Act
        var branding = new TenantBranding(TENANT_ID);

        // Assert
        branding.TenantId.Should().Be(TENANT_ID);
        branding.PublishedJson.Should().BeNull();
        branding.PreviousJson.Should().BeNull();
        branding.DraftJson.Should().BeNull();
        branding.Revision.Should().Be(0);
        branding.PublishedAt.Should().BeNull();
    }

    [Fact]
    public void Publish_AtCurrentRevision_KeepsTheOldValueAsPrevious()
    {
        // Arrange
        var branding = new TenantBranding(TENANT_ID);
        branding.Publish(_blue, 0, _now);

        // Act
        var result = branding.Publish(_green, 1, _now.AddMinutes(1));

        // Assert
        result.IsSuccess.Should().BeTrue();
        branding.PublishedJson.Should().Be(_green.CanonicalJson);
        branding.PreviousJson.Should().Be(_blue.CanonicalJson);
        branding.Revision.Should().Be(2);
        branding.PublishedAt.Should().Be(_now.AddMinutes(1));
    }

    [Fact]
    public void Publish_SameValueTwice_KeepsThePreviousVersion()
    {
        // Arrange
        var branding = new TenantBranding(TENANT_ID);
        branding.Publish(_blue, 0, _now);
        branding.Publish(_green, 1, _now);

        // Act: a retry of the second publish, with the revision it read before
        var result = branding.Publish(Accent("#16a34a"), 1, _now.AddMinutes(5));

        // Assert
        result.IsSuccess.Should().BeTrue();
        branding.PublishedJson.Should().Be(_green.CanonicalJson);
        branding.PreviousJson.Should().Be(_blue.CanonicalJson);
        branding.Revision.Should().Be(2);
        branding.PublishedAt.Should().Be(_now);
    }

    [Fact]
    public void Publish_StaleRevision_IsRefused()
    {
        // Arrange
        var branding = new TenantBranding(TENANT_ID);
        branding.Publish(_blue, 0, _now);

        // Act
        var result = branding.Publish(_green, 0, _now);

        // Assert
        result.Status.Should().Be(ResultStatus.Conflict);
        branding.PublishedJson.Should().Be(_blue.CanonicalJson);
        branding.Revision.Should().Be(1);
    }

    [Fact]
    public void Publish_OverrideCheckedForAnotherTenant_Throws()
    {
        // Arrange
        var branding = new TenantBranding(TENANT_ID + 1);

        // Act
        var act = () => branding.Publish(_blue, 0, _now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rollback_Twice_ReturnsToTheStart()
    {
        // Arrange
        var branding = new TenantBranding(TENANT_ID);
        branding.Publish(_blue, 0, _now);
        branding.Publish(_green, 1, _now);

        // Act
        var first = branding.Rollback(2, _now.AddMinutes(1));
        var afterFirst = (branding.PublishedJson, branding.PreviousJson);
        var second = branding.Rollback(3, _now.AddMinutes(2));

        // Assert
        first.IsSuccess.Should().BeTrue();
        afterFirst.Should().Be((_blue.CanonicalJson, _green.CanonicalJson));
        second.IsSuccess.Should().BeTrue();
        branding.PublishedJson.Should().Be(_green.CanonicalJson);
        branding.PreviousJson.Should().Be(_blue.CanonicalJson);
        branding.Revision.Should().Be(4);
        branding.PublishedAt.Should().Be(_now.AddMinutes(2));
    }

    [Fact]
    public void Rollback_AfterFirstPublish_ReturnsToTheDeploymentBrand()
    {
        // Arrange
        var branding = new TenantBranding(TENANT_ID);
        branding.Publish(_blue, 0, _now);

        // Act
        var result = branding.Rollback(1, _now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        branding.PublishedJson.Should().BeNull();
        branding.PreviousJson.Should().Be(_blue.CanonicalJson);
    }

    [Fact]
    public void Rollback_Retried_IsRefusedInsteadOfUndoingItself()
    {
        // Arrange
        var branding = new TenantBranding(TENANT_ID);
        branding.Publish(_blue, 0, _now);
        branding.Publish(_green, 1, _now);
        branding.Rollback(2, _now);

        // Act
        var retry = branding.Rollback(2, _now);

        // Assert
        retry.Status.Should().Be(ResultStatus.Conflict);
        branding.PublishedJson.Should().Be(_blue.CanonicalJson);
        branding.Revision.Should().Be(3);
    }

    [Fact]
    public void Rollback_NothingPublishedBefore_IsInvalid()
    {
        // Arrange
        var branding = new TenantBranding(TENANT_ID);

        // Act
        var result = branding.Rollback(0, _now);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle().Which.ErrorCode.Should().Be(BrandOverrideProblems.NO_PREVIOUS_VERSION);
        branding.Revision.Should().Be(0);
    }

    private static BrandOverride Accent(string hex) =>
        BrandOverride.Parse("{\"version\":1,\"tokens\":{\"color\":{\"primary\":{\"$type\":\"color\",\"$value\":\"" + hex + "\"}}}}", TENANT_ID).Value;
}
