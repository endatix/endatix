using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.Export;

public sealed class ColumnAliasTransformerRegistryTests
{
    [Fact]
    public void Default_ResolvesNativeAndCrunch()
    {
        ColumnAliasTransformerRegistry.Default.TryGet(ColumnAliasProfile.Native, out IColumnAliasTransformer? native)
            .Should().BeTrue();
        native!.Profile.Should().Be(ColumnAliasProfile.Native);

        ColumnAliasTransformerRegistry.Default.TryGet(ColumnAliasProfile.Crunch, out IColumnAliasTransformer? crunch)
            .Should().BeTrue();
        crunch!.Profile.Should().Be(ColumnAliasProfile.Crunch);
    }

    [Fact]
    public void GetCatalog_ReturnsUiMetadataForRegisteredTransformers()
    {
        IReadOnlyList<ColumnAliasNamingConventionDto> catalog =
            ColumnAliasTransformerRegistry.Default.GetCatalog();

        catalog.Should().HaveCount(2);
        catalog.Should().Contain(entry =>
            entry.WireKey == "native" &&
            entry.Label == "Survey keys" &&
            !string.IsNullOrWhiteSpace(entry.Description) &&
            !string.IsNullOrWhiteSpace(entry.Example));
        catalog.Should().Contain(entry =>
            entry.WireKey == "crunch" &&
            entry.Label == "Question index" &&
            !string.IsNullOrWhiteSpace(entry.Description) &&
            !string.IsNullOrWhiteSpace(entry.Example));
    }

    [Fact]
    public void GetRequired_WithUnregisteredProfile_Throws()
    {
        ColumnAliasTransformerRegistry registry = new(
            [ColumnAliasTransformerRegistry.Default.GetRequired(ColumnAliasProfile.Native)]);

        Action act = () => registry.GetRequired(ColumnAliasProfile.Crunch);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Crunch*");
    }
}
