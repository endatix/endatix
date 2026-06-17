using System.Text.Json;
using Endatix.Api.Endpoints.Admin.PlatformAdmins;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Features.PlatformAdmin.Common;

namespace Endatix.Api.Tests.Endpoints.Admin.PlatformAdmins;

public sealed class PlatformAdminUserResponseTests
{
    [Fact]
    public void MapPage_PreservesLastLoginAt()
    {
        // Arrange
        var lastLoginAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        Paged<PlatformAdminUserListItem> users = new(
            page: 1,
            pageSize: 10,
            totalRecords: 1,
            totalPages: 1,
            [
                new PlatformAdminUserListItem(
                    Id: 1,
                    TenantId: 14,
                    TenantName: "Numerator",
                    UserName: "admin@example.com",
                    Email: "admin@example.com",
                    DisplayName: "Admin User",
                    AuthProvider: "Endatix",
                    IsExternal: false,
                    IsVerified: true,
                    IsLockedOut: false,
                    LastLoginAt: lastLoginAt,
                    HasExternalPlatformAdminRole: true,
                    Roles: ["PlatformAdmin"])
            ]);

        // Act
        var response = PlatformAdminUserResponse.MapPage(users);

        // Assert
        response.Items.Should().ContainSingle();
        response.Items.First().LastLoginAt.Should().Be(lastLoginAt);
        response.Items.First().HasExternalPlatformAdminRole.Should().BeTrue();
    }

    [Fact]
    public void MapPage_SerializesLastLoginAtAsCamelCase()
    {
        // Arrange
        DateTimeOffset lastLoginAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        Paged<PlatformAdminUserResponse> response = new(
            page: 1,
            pageSize: 10,
            totalRecords: 1,
            totalPages: 1,
            [
                new PlatformAdminUserResponse
                {
                    Id = 1,
                    TenantId = 14,
                    TenantName = "Numerator",
                    UserName = "admin@example.com",
                    Email = "admin@example.com",
                    AuthProvider = "Endatix",
                    IsVerified = true,
                    LastLoginAt = lastLoginAt,
                    HasExternalPlatformAdminRole = true,
                    Roles = ["PlatformAdmin"]
                }
            ]);

        // Act
        string json = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        // Assert
        json.Should().Contain("\"lastLoginAt\"");
        json.Should().Contain("\"hasExternalPlatformAdminRole\"");
        json.Should().NotContain("\"LastLoginAt\"");
        json.Should().NotContain("\"HasExternalPlatformAdminRole\"");
    }
}
