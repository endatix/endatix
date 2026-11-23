using Endatix.Api.Common;
using Microsoft.AspNetCore.Http;

namespace Endatix.Api.Tests.Common;

public class EntityTagHeaderTests
{
    [Theory]
    [InlineData(0L, "\"0\"")]
    [InlineData(42L, "\"42\"")]
    public void Format_QuotesTheVersion(long version, string expected)
    {
        EntityTagHeader.Format(version).Should().Be(expected);
    }

    [Theory]
    [InlineData("\"0\"", 0L)]
    [InlineData("\"3\"", 3L)]
    [InlineData(" \"3\" ", 3L)]
    public void TryParse_WithOneStrongTag_ReadsTheVersion(string ifMatch, long expected)
    {
        var parsed = EntityTagHeader.TryParse(ifMatch, out var version);

        parsed.Should().BeTrue();
        version.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("3")]
    [InlineData("W/\"3\"")]
    [InlineData("*")]
    [InlineData("\"1\", \"2\"")]
    [InlineData("\"one\"")]
    [InlineData("\"-1\"")]
    [InlineData("\"\"")]
    public void TryParse_WithAnythingButOneStrongTag_IsFalse(string? ifMatch)
    {
        EntityTagHeader.TryParse(ifMatch, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void ReadIfMatch_WhenMissing_IsPreconditionRequired(string? ifMatch)
    {
        var problem = EntityTagHeader.ReadIfMatch(ifMatch, out _);

        problem!.StatusCode.Should().Be(StatusCodes.Status428PreconditionRequired);
    }

    [Theory]
    [InlineData("3")]
    [InlineData("W/\"3\"")]
    public void ReadIfMatch_WhenUnreadable_IsBadRequest(string ifMatch)
    {
        var problem = EntityTagHeader.ReadIfMatch(ifMatch, out _);

        problem!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void ReadIfMatch_WithOneStrongTag_IsNoProblemAndTheVersion()
    {
        var problem = EntityTagHeader.ReadIfMatch("\"3\"", out var version);

        problem.Should().BeNull();
        version.Should().Be(3);
    }

    [Fact]
    public void SetETag_WritesTheQuotedVersion()
    {
        var context = new DefaultHttpContext();

        EntityTagHeader.SetETag(context.Response, 5);

        context.Response.Headers.ETag.ToString().Should().Be("\"5\"");
    }
}
