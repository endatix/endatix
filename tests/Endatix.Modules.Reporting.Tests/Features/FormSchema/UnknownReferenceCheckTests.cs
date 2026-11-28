using System.Text.Json;
using Endatix.Core.UseCases.FormDrafts;
using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Reporting.Features.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema;

public sealed class UnknownReferenceCheckTests
{
    [Theory]
    [InlineData(true, "warning")]
    [InlineData(false, "info")]
    public async Task RunAsync_UnknownRootRepeated_GroupsReferencesWithFormScopedSeverity(bool declared, string severity)
    {
        // Arrange
        var catalog = Substitute.For<IAudiencePropertyCatalog>();
        catalog.ListForFormAsync(23, 42, Arg.Any<CancellationToken>())
            .Returns(declared ? [new AudiencePropertyName("division", "Division", 1)] : []);
        var check = new UnknownReferenceCheck([catalog]);
        var json = JsonSerializer.Deserialize<JsonElement>("""
            {"title":"{Host.team} {HOST} {locale}","elements":[{"type":"text","name":"q",
             "visibleIf":"{q} <> '{literal}' and {host} <> 'x'","description":"{division}"}]}
            """);

        // Act
        var findings = await check.RunAsync(new FormCheckInput(23, 42, 2, "", json), TestContext.Current.CancellationToken);

        // Assert
        check.Stage.Should().Be(FormCheckStage.Publication);
        findings.Select(f => f.Key).Should().BeEquivalentTo(declared
            ? ["reference/unknown:host"] : new[] { "reference/unknown:host", "reference/unknown:division" });
        var root = findings.Single(f => f.Key == "reference/unknown:host");
        root.Data!.Value.GetProperty("count").GetInt32().Should().Be(3);
        root.Severity.Should().Be(severity);
        root.RequiresAck.Should().Be(declared);
        root.Blocking.Should().BeFalse();
    }
}
