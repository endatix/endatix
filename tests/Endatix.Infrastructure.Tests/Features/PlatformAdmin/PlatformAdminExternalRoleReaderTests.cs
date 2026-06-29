using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Infrastructure.Features.PlatformAdmin.Common;

namespace Endatix.Infrastructure.Tests.Features.PlatformAdmin;

public sealed class PlatformAdminExternalRoleReaderTests
{
    [Theory]
    [InlineData("[\"PlatformAdmin\"]", true)]
    [InlineData("[\"platformadmin\"]", true)]
    [InlineData("[\"Admin\"]", false)]
    [InlineData("[\"NotPlatformAdmin\"]", false)]
    [InlineData(null, false)]
    [InlineData("not-json", false)]
    public void HasPlatformAdminRole_ParsesJsonArrayElements(string? externalRolesJson, bool expected)
    {
        PlatformAdminExternalRoleReader.HasPlatformAdminRole(externalRolesJson).Should().Be(expected);
    }

    [Fact]
    public void QuotedPlatformAdminRoleName_MatchesSerializedArrayElement()
    {
        var json = JsonSerializer.Serialize(new[] { SystemRole.PlatformAdmin.Name });

        json.Should().Contain(PlatformAdminExternalRoleReader.QuotedPlatformAdminRoleName);
    }
}
