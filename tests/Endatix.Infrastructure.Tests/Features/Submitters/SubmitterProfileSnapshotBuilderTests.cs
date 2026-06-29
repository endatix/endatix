using System.Text.Json;
using Endatix.Infrastructure.Features.Submitters;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Tests.Features.Submitters;

public sealed class SubmitterProfileSnapshotBuilderTests
{
    [Fact]
    public void Build_WithProfileSnapshotFields_ReturnsConfiguredSubset()
    {
        SubmitterProfileSnapshotBuilder builder = new(Options.Create(new SubmitterOptions
        {
            ProfileSnapshotFields = ["email", "given_name"]
        }));

        string? snapshot = builder.Build(new Dictionary<string, string>
        {
            ["email"] = "respondent@example.com",
            ["given_name"] = "Jane",
            ["family_name"] = "Doe"
        });

        Dictionary<string, string>? values = JsonSerializer.Deserialize<Dictionary<string, string>>(snapshot!);
        values.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["email"] = "respondent@example.com",
            ["given_name"] = "Jane"
        });
    }

    [Fact]
    public void Build_WithNoProfileSnapshotFields_ReturnsNull()
    {
        SubmitterProfileSnapshotBuilder builder = new(Options.Create(new SubmitterOptions()));

        string? snapshot = builder.Build(new Dictionary<string, string>
        {
            ["email"] = "respondent@example.com"
        });

        snapshot.Should().BeNull();
    }
}
