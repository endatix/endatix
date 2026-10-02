using System.Reflection;
using Endatix.Api.Endpoints.Application;
using FastEndpoints;

namespace Endatix.Api.Tests.Endpoints.Application;

public class GetVersionTests
{
    private readonly GetVersion _endpoint = Factory.Create<GetVersion>();

    [Fact]
    public async Task ExecuteAsync_CurrentAssembly_MatchesBuildIdentityAndDropsCommitSuffix()
    {
        // Arrange
        var assembly = typeof(GetVersion).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        var branch = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == BuildIdentity.BranchMetadataKey)
            ?.Value;
        var expected = BuildIdentity.From(informational, branch);

        // Act
        var response = await _endpoint.ExecuteAsync(TestContext.Current.CancellationToken);

        // Assert
        response.Value.Should().Be(expected);
        (response.Value!.Version ?? string.Empty).Should().NotContain("+");
    }

    [Theory]
    [InlineData("0.7.7-canary.44+abcdef", "0.7.7-canary.44", "abcdef")]
    [InlineData("0.7.7", "0.7.7", null)]
    [InlineData("0.8.1-hotfix.1+abcdef", "0.8.1-hotfix.1", "abcdef")]
    [InlineData("1.2.3-rc.1+abcdef", "1.2.3-rc.1", "abcdef")]
    public void From_ReleaseBuild_ReturnsVersionAndCommit(string informational, string version, string? commit)
    {
        // Arrange
        // informational is the theory input.

        // Act
        var identity = BuildIdentity.From(informational, branch: null);

        // Assert
        identity.Version.Should().Be(version);
        identity.Commit.Should().Be(commit);
        identity.Branch.Should().BeNull();
    }

    [Theory]
    [InlineData("0.0.0-local+abcdef")]
    [InlineData("0.0.0-ci+abcdef")]
    public void From_BuildOutsideTheReleasePipeline_ReturnsBranchAndCommitWithoutVersion(string informational)
    {
        // Arrange
        const string branch = "feat/e1130-version-api";

        // Act
        var identity = BuildIdentity.From(informational, branch);

        // Assert
        identity.Version.Should().BeNull();
        identity.Branch.Should().Be(branch);
        identity.Commit.Should().Be("abcdef");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void From_NoInformationalVersion_ReturnsNothing(string? informational)
    {
        // Arrange
        const string blankBranch = " ";

        // Act
        var identity = BuildIdentity.From(informational, blankBranch);

        // Assert
        identity.Should().Be(new ProductVersionResponse(null, null, null));
    }
}
