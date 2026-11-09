using Endatix.Core.Entities;
using Endatix.Infrastructure.Features.Submissions;
using Microsoft.Extensions.Configuration;

namespace Endatix.Infrastructure.Tests.Features.Submissions;

public sealed class CollectionStatusRegistrationTests
{
    [Fact]
    public void Read_CodesWithPhaseNames_MapsEachCodeToItsPhase()
    {
        // Arrange
        var section = Section(new() { ["panel_overquota"] = "closed", ["invite_expired"] = "Lapsed" });

        // Act
        var codes = CollectionStatusRegistration.Read(section);

        // Assert
        codes.Should().BeEquivalentTo(new Dictionary<string, CollectionPhase>
        {
            ["panel_overquota"] = CollectionPhase.Closed,
            ["invite_expired"] = CollectionPhase.Lapsed,
        });
    }

    [Fact]
    public void Read_CodeWithUnknownPhase_ThrowsNamingTheSetting()
    {
        // Arrange
        var section = Section(new() { ["panel_overquota"] = "terminal" });

        // Act
        var read = () => CollectionStatusRegistration.Read(section);

        // Assert
        read.Should().Throw<InvalidOperationException>().WithMessage("*CollectionStatuses:panel_overquota*terminal*");
    }

    [Fact]
    public void Read_NoSection_ReturnsNoCodes()
    {
        // Act
        var codes = CollectionStatusRegistration.Read(Section([]));

        // Assert
        codes.Should().BeEmpty();
    }

    private static IConfigurationSection Section(Dictionary<string, string?> codes) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(codes.ToDictionary(pair => $"{CollectionStatusRegistration.SectionPath}:{pair.Key}", pair => pair.Value))
            .Build()
            .GetSection(CollectionStatusRegistration.SectionPath);
}
