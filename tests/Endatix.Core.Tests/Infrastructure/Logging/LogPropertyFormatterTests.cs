using System.Security.Claims;
using Endatix.Core.Infrastructure.Logging;

namespace Endatix.Core.Tests.Infrastructure.Logging;

public sealed class LogPropertyFormatterTests
{
    [Fact]
    public void FormatForLog_Null_ReturnsNull()
    {
        LogPropertyFormatter.FormatForLog(null).Should().BeNull();
    }

    [Fact]
    public void FormatForLog_SimpleTypes_ReturnsOriginalValue()
    {
        LogPropertyFormatter.FormatForLog(42).Should().Be(42);
        LogPropertyFormatter.FormatForLog("hello").Should().Be("hello");
        LogPropertyFormatter.FormatForLog(true).Should().Be(true);
    }

    [Fact]
    public void FormatForLog_ClaimsPrincipal_ReturnsCompactSummary()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "4ba2d438-ecde-48f5-be8b-a28fc2372409"),
            new Claim("email", "panelist@endatix.com")
        ], authenticationType: "Keycloak"));

        var formatted = LogPropertyFormatter.FormatForLog(principal);

        formatted.Should().Be("ClaimsPrincipal(authenticated=True, userId=4ba2d438-ecde-48f5-be8b-a28fc2372409)");
    }

    [Fact]
    public void FormatForLog_ComplexType_ReturnsTypeName()
    {
        var formatted = LogPropertyFormatter.FormatForLog(new TestResponse { Result = "Success" });

        formatted.Should().Be("[TestResponse]");
    }

    private sealed class TestResponse
    {
        public string Result { get; set; } = string.Empty;
    }
}
