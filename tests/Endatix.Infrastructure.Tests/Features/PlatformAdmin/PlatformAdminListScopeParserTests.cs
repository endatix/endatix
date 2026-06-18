using Endatix.Infrastructure.Features.PlatformAdmin.Common;

namespace Endatix.Infrastructure.Tests.Features.PlatformAdmin;

public sealed class PlatformAdminListScopeParserTests
{
    [Theory]
    [InlineData(null, PlatformAdminListScope.All)]
    [InlineData("", PlatformAdminListScope.All)]
    [InlineData("all", PlatformAdminListScope.All)]
    [InlineData("approved", PlatformAdminListScope.Approved)]
    [InlineData("Candidates", PlatformAdminListScope.Candidates)]
    public void Parse_MapsKnownValues(string? scope, PlatformAdminListScope expected) =>
        PlatformAdminListScopeParser.Parse(scope).Should().Be(expected);
}
