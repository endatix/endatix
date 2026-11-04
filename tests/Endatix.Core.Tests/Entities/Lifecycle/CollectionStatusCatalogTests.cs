using Endatix.Core.Entities;
using FluentAssertions;

namespace Endatix.Core.Tests.Entities.Lifecycle;

public class CollectionStatusCatalogTests
{
    [Theory]
    [InlineData(CollectionStatusCodes.NotStarted, CollectionPhase.Pending)]
    [InlineData(CollectionStatusCodes.Viewed, CollectionPhase.Pending)]
    [InlineData(CollectionStatusCodes.InProgress, CollectionPhase.Active)]
    [InlineData(CollectionStatusCodes.Complete, CollectionPhase.Completed)]
    [InlineData(CollectionStatusCodes.ScreenOut, CollectionPhase.Closed)]
    [InlineData(CollectionStatusCodes.QuotaFull, CollectionPhase.Closed)]
    [InlineData(CollectionStatusCodes.Abandoned, CollectionPhase.Closed)]
    [InlineData(CollectionStatusCodes.Cancelled, CollectionPhase.Closed)]
    [InlineData(CollectionStatusCodes.Expired, CollectionPhase.Lapsed)]
    [InlineData(HostCollectionStatuses.PanelOverquota, CollectionPhase.Closed)]
    [InlineData(HostCollectionStatuses.InviteExpired, CollectionPhase.Lapsed)]
    public void TryGet_RegisteredCode_ReturnsTheStatusWithItsPhase(string code, CollectionPhase expected)
    {
        // Act
        var found = CollectionStatusCatalog.TryGet(code.ToUpperInvariant(), out var status);

        // Assert
        found.Should().BeTrue();
        status!.Code.Should().Be(code);
        status.Phase.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not_registered")]
    [InlineData("a_code_that_is_much_longer_than_thirty_two")]
    public void TryGet_UnknownOrInvalidCode_ReturnsFalse(string? code)
    {
        // Act
        var found = CollectionStatusCatalog.TryGet(code, out var status);

        // Assert
        found.Should().BeFalse();
        status.Should().BeNull();
    }

    [Fact]
    public void Phase_UnregisteredStoredCode_ReadsAsClosed()
    {
        // Arrange
        var stored = CollectionStatus.FromCode("removed_from_config");

        // Act
        var phase = stored.Phase;

        // Assert
        phase.Should().Be(CollectionPhase.Closed);
    }

    [Fact]
    public void Configure_TheSameCodesAgain_ChangesNothing()
    {
        // Act
        var configure = () => CollectionStatusCatalog.Configure(HostCollectionStatuses.Codes);

        // Assert
        configure.Should().NotThrow();
        CollectionStatusCatalog.Registered.Should().ContainKey(HostCollectionStatuses.InviteSent);
    }

    [Fact]
    public void Configure_NoCodes_ChangesNothing()
    {
        // Act
        var configure = () => CollectionStatusCatalog.Configure(new Dictionary<string, CollectionPhase>());

        // Assert
        configure.Should().NotThrow();
        CollectionStatusCatalog.Registered.Should().ContainKey(HostCollectionStatuses.PanelOverquota);
    }

    [Fact]
    public void Configure_DifferentCodesAfterARead_Throws()
    {
        // Arrange
        CollectionStatusCatalog.TryGet(CollectionStatusCodes.InProgress, out _);

        // Act
        var configure = () => CollectionStatusCatalog.Configure(new Dictionary<string, CollectionPhase>
        {
            ["late_code"] = CollectionPhase.Closed,
        });

        // Assert
        configure.Should().Throw<InvalidOperationException>();
        CollectionStatusCatalog.TryGet("late_code", out _).Should().BeFalse();
    }

    [Fact]
    public void Configure_BuiltInCodeWithAnotherPhase_Throws()
    {
        // Act
        var configure = () => CollectionStatusCatalog.Configure(new Dictionary<string, CollectionPhase>
        {
            [CollectionStatusCodes.Expired] = CollectionPhase.Closed,
        });

        // Assert
        configure.Should().Throw<ArgumentException>().WithMessage("*expired*lapsed*");
    }

    [Fact]
    public void Configure_CodeLongerThan32_Throws()
    {
        // Act
        var configure = () => CollectionStatusCatalog.Configure(new Dictionary<string, CollectionPhase>
        {
            [new string('x', CollectionStatus.CODE_MAX_LENGTH + 1)] = CollectionPhase.Closed,
        });

        // Assert
        configure.Should().Throw<ArgumentException>();
    }
}
