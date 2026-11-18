using Endatix.Core.Branding;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using FluentAssertions;

namespace Endatix.Core.Tests.Entities;

public sealed class TenantBrandingTests
{
    private const long TenantId = 42;
    private static readonly DateTime Now = new(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc);

    private static readonly BrandOverride Blue = Accent("#0052d1");
    private static readonly BrandOverride Green = Accent("#16a34a");

    [Fact]
    public void New_NothingPublished_AtRevisionZero()
    {
        // Act
        var branding = new TenantBranding(TenantId);

        // Assert
        branding.TenantId.Should().Be(TenantId);
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
        var branding = new TenantBranding(TenantId);
        branding.Publish(Blue, 0, Now);

        // Act
        var result = branding.Publish(Green, 1, Now.AddMinutes(1));

        // Assert
        result.IsSuccess.Should().BeTrue();
        branding.PublishedJson.Should().Be(Green.CanonicalJson);
        branding.PreviousJson.Should().Be(Blue.CanonicalJson);
        branding.Revision.Should().Be(2);
        branding.PublishedAt.Should().Be(Now.AddMinutes(1));
    }

    [Fact]
    public void Publish_SameValueTwice_KeepsThePreviousVersion()
    {
        // Arrange
        var branding = new TenantBranding(TenantId);
        branding.Publish(Blue, 0, Now);
        branding.Publish(Green, 1, Now);

        // Act: a retry of the second publish, with the revision it read before
        var result = branding.Publish(Accent("#16a34a"), 1, Now.AddMinutes(5));

        // Assert
        result.IsSuccess.Should().BeTrue();
        branding.PublishedJson.Should().Be(Green.CanonicalJson);
        branding.PreviousJson.Should().Be(Blue.CanonicalJson);
        branding.Revision.Should().Be(2);
        branding.PublishedAt.Should().Be(Now);
    }

    [Fact]
    public void Publish_StaleRevision_IsRefused()
    {
        // Arrange
        var branding = new TenantBranding(TenantId);
        branding.Publish(Blue, 0, Now);

        // Act
        var result = branding.Publish(Green, 0, Now);

        // Assert
        result.Status.Should().Be(ResultStatus.Conflict);
        branding.PublishedJson.Should().Be(Blue.CanonicalJson);
        branding.Revision.Should().Be(1);
    }

    [Fact]
    public void Publish_OverrideCheckedForAnotherTenant_Throws()
    {
        // Arrange
        var branding = new TenantBranding(TenantId + 1);

        // Act
        var act = () => branding.Publish(Blue, 0, Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rollback_Twice_ReturnsToTheStart()
    {
        // Arrange
        var branding = new TenantBranding(TenantId);
        branding.Publish(Blue, 0, Now);
        branding.Publish(Green, 1, Now);

        // Act
        var first = branding.Rollback(2, Now.AddMinutes(1));
        var afterFirst = (branding.PublishedJson, branding.PreviousJson);
        var second = branding.Rollback(3, Now.AddMinutes(2));

        // Assert
        first.IsSuccess.Should().BeTrue();
        afterFirst.Should().Be((Blue.CanonicalJson, Green.CanonicalJson));
        second.IsSuccess.Should().BeTrue();
        branding.PublishedJson.Should().Be(Green.CanonicalJson);
        branding.PreviousJson.Should().Be(Blue.CanonicalJson);
        branding.Revision.Should().Be(4);
        branding.PublishedAt.Should().Be(Now.AddMinutes(2));
    }

    [Fact]
    public void Rollback_AfterFirstPublish_ReturnsToTheDeploymentBrand()
    {
        // Arrange
        var branding = new TenantBranding(TenantId);
        branding.Publish(Blue, 0, Now);

        // Act
        var result = branding.Rollback(1, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        branding.PublishedJson.Should().BeNull();
        branding.PreviousJson.Should().Be(Blue.CanonicalJson);
    }

    [Fact]
    public void Rollback_Retried_IsRefusedInsteadOfUndoingItself()
    {
        // Arrange
        var branding = new TenantBranding(TenantId);
        branding.Publish(Blue, 0, Now);
        branding.Publish(Green, 1, Now);
        branding.Rollback(2, Now);

        // Act
        var retry = branding.Rollback(2, Now);

        // Assert
        retry.Status.Should().Be(ResultStatus.Conflict);
        branding.PublishedJson.Should().Be(Blue.CanonicalJson);
        branding.Revision.Should().Be(3);
    }

    [Fact]
    public void Rollback_NothingPublishedBefore_IsInvalid()
    {
        // Arrange
        var branding = new TenantBranding(TenantId);

        // Act
        var result = branding.Rollback(0, Now);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle().Which.ErrorCode.Should().Be(BrandOverrideProblems.NO_PREVIOUS_VERSION);
        branding.Revision.Should().Be(0);
    }

    private static BrandOverride Accent(string hex) =>
        BrandOverride.Parse("{\"version\":1,\"tokens\":{\"color\":{\"primary\":{\"$type\":\"color\",\"$value\":\"" + hex + "\"}}}}", TenantId).Value;
}
