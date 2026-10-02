using Endatix.Api.Endpoints.Application;
using FastEndpoints;

namespace Endatix.Api.Tests.Endpoints.Application;

public class GetVersionTests
{
    private readonly GetVersion _endpoint = Factory.Create<GetVersion>();

    [Fact]
    public async Task ExecuteAsync_ReturnsVersionWithoutCommitSuffix()
    {
        // Act
        var response = await _endpoint.ExecuteAsync(TestContext.Current.CancellationToken);

        // Assert
        response.Value.Should().NotBeNull();
        (response.Value!.Version ?? string.Empty).Should().NotContain("+");
    }

    [Theory]
    [InlineData("0.7.7-canary.44+abcdef", "0.7.7-canary.44", "abcdef")]
    [InlineData("0.7.7", "0.7.7", null)]
    [InlineData("0.8.1-hotfix.1+abcdef", "0.8.1-hotfix.1", "abcdef")]
    [InlineData("1.2.3-rc.1+abcdef", "1.2.3-rc.1", "abcdef")]
    public void From_ReleaseBuild_ReturnsVersionAndCommit(string informational, string version, string? commit)
    {
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
        // Act
        var identity = BuildIdentity.From(informational, branch: "feat/e1130-version-api");

        // Assert
        identity.Version.Should().BeNull();
        identity.Branch.Should().Be("feat/e1130-version-api");
        identity.Commit.Should().Be("abcdef");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void From_NoInformationalVersion_ReturnsNothing(string? informational)
    {
        // Act
        var identity = BuildIdentity.From(informational, branch: " ");

        // Assert
        identity.Should().Be(new ProductVersionResponse(null, null, null));
    }
}
