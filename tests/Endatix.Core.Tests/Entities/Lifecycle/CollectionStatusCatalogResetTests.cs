using Endatix.Core.Entities;
using FluentAssertions;

namespace Endatix.Core.Tests.Entities.Lifecycle;

/// <summary>Tests that reset the process-wide catalog, alone, after every parallel test has run.</summary>
[CollectionDefinition(nameof(CollectionStatusCatalogResetTests), DisableParallelization = true)]
public sealed class CollectionStatusCatalogResetCollection;

[Collection(nameof(CollectionStatusCatalogResetTests))]
public sealed class CollectionStatusCatalogResetTests : IDisposable
{
    public CollectionStatusCatalogResetTests() => CollectionStatusCatalog.ResetForTests();

    // Puts back the host codes the other tests in this assembly register.
    public void Dispose()
    {
        CollectionStatusCatalog.ResetForTests();
        CollectionStatusCatalog.Configure(HostCollectionStatuses.Codes);
    }

    [Fact]
    public void Configure_AfterReset_RegistersOtherCodes()
    {
        // Act
        CollectionStatusCatalog.Configure(new Dictionary<string, CollectionPhase> { ["other_host"] = CollectionPhase.Closed });

        // Assert
        CollectionStatusCatalog.TryGet("other_host", out _).Should().BeTrue();
        CollectionStatusCatalog.TryGet(HostCollectionStatuses.PanelOverquota, out _).Should().BeFalse();
    }

    [Fact]
    public void Configure_NoCodesWhenNoneRegistered_ChangesNothing()
    {
        // Act
        var configure = () => CollectionStatusCatalog.Configure(new Dictionary<string, CollectionPhase>());

        // Assert
        configure.Should().NotThrow();
        CollectionStatusCatalog.TryGet(CollectionStatusCodes.ScreenOut, out _).Should().BeTrue();
    }
}
