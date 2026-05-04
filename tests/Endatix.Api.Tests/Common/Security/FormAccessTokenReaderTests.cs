using Endatix.Api.Common.Security;
using Microsoft.AspNetCore.Http;

namespace Endatix.Api.Tests.Common.Security;

public class FormAccessTokenReaderTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ReadToken_MissingOrEmptyAuthorizationHeader_ReturnsNull(string? authHeader)
    {
        var httpRequest = CreateHttpRequest(authHeader);

        var result = FormAccessTokenReader.ReadToken(httpRequest);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("Basic xyz")]
    [InlineData("Bearer")]
    [InlineData("Bearer  ")]
    [InlineData("NotBearer xyz")]
    public void ReadToken_InvalidAuthorizationScheme_ReturnsNull(string authHeader)
    {
        var httpRequest = CreateHttpRequest(authHeader);

        var result = FormAccessTokenReader.ReadToken(httpRequest);

        result.Should().BeNull();
    }

    [Fact]
    public void ReadToken_NullHttpRequest_ReturnsNull()
    {
        var result = FormAccessTokenReader.ReadToken(null!);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9")]
    [InlineData("Bearer token123")]
    [InlineData("Bearer abc.def.ghi")]
    [InlineData("Bearer  token-with-spaces")]
    [InlineData("bearer lower")]
    [InlineData("BEARER upper")]
    public void ReadToken_ValidBearerToken_ReturnsToken(string authHeader)
    {
        var httpRequest = CreateHttpRequest(authHeader);
        var prefixLen = authHeader.IndexOf(' ') + 1;
        var expectedToken = authHeader[prefixLen..].Trim();

        var result = FormAccessTokenReader.ReadToken(httpRequest);

        result.Should().Be(expectedToken);
    }

    [Fact]
    public void ReadToken_BearerTokenWithExtraSpaces_ParsesTokenCorrectly()
    {
        var httpRequest = CreateHttpRequest("Bearer   token123  ");

        var result = FormAccessTokenReader.ReadToken(httpRequest);

        result.Should().Be("token123");
    }

    [Fact]
    public void ReadToken_BearerPrefixCaseInsensitive_Matches()
    {
        var httpRequest = CreateHttpRequest("bearer mytoken");

        var result = FormAccessTokenReader.ReadToken(httpRequest);

        result.Should().Be("mytoken");
    }

    private static HttpRequest CreateHttpRequest(string? authHeader)
    {
        var httpContext = new DefaultHttpContext();
        if (authHeader != null)
        {
            httpContext.Request.Headers.Authorization = authHeader;
        }
        return httpContext.Request;
    }
}