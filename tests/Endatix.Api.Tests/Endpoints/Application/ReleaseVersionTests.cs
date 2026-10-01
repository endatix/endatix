using Endatix.Api.Endpoints.Application;

namespace Endatix.Api.Tests.Endpoints.Application;

public class ReleaseVersionTests
{
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

    [Fact]
    public void WithoutCommit_FallsBackToAssemblyVersion()
    {
        // Act
        var version = ReleaseVersion.WithoutCommit(informationalVersion: null, new global::System.Version(0, 7, 7));

        // Assert
        version.Should().Be("0.7.7");
    }
}
