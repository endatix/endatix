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
        response.Value!.Version.Should().NotBeNullOrWhiteSpace();
        response.Value.Version.Should().NotContain("+");
    }

    [Theory]
    [InlineData("0.7.7-canary.44+abcdef", "0.7.7-canary.44")]
    [InlineData("0.7.7", "0.7.7")]
    [InlineData("0.1.1-beta+abcdef", "0.1.1-beta")]
    [InlineData("1.2.3-beta.2+abcdef", "1.2.3-beta.2")]
    [InlineData("1.2.3-alpha.1+abcdef", "1.2.3-alpha.1")]
    [InlineData("1.2.3-rc.1+abcdef", "1.2.3-rc.1")]
    [InlineData("1.2.3-rc.1", "1.2.3-rc.1")]
    public void WithoutCommit_StripsSourceLinkSuffix(string informational, string expected)
    {
        // Act
        var version = ReleaseVersion.WithoutCommit(informational, assemblyVersion: null);

        // Assert
        version.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void WithoutCommit_BlankInformationalVersion_FallsBackToAssemblyVersion(string? informational)
    {
        // Act
        var version = ReleaseVersion.WithoutCommit(informational, new System.Version(0, 7, 7));

        // Assert
        version.Should().Be("0.7.7");
    }

    [Fact]
    public void WithoutCommit_NoVersionAtAll_ReturnsUnknown()
    {
        // Act
        var version = ReleaseVersion.WithoutCommit(informationalVersion: null, assemblyVersion: null);

        // Assert
        version.Should().Be("unknown");
    }
}
