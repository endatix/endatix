using Endatix.Core.Abstractions.Authorization;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Tests.Identity.Authorization;

public sealed class PlatformAdminLocalApprovalGateTests
{
    [Fact]
    public async Task ApplyAsync_NoPlatformAdminRole_ReturnsMappedAuthorizationUnchanged()
    {
        // Arrange
        var dbOptions = new DbContextOptionsBuilder<AppIdentityDbContext>().Options;
        var valueGeneratorFactory = Substitute.For<EfCoreValueGeneratorFactory>(Substitute.For<Core.Abstractions.IIdGenerator<long>>());
        var idGenerator = Substitute.For<Core.Abstractions.IIdGenerator<long>>();
        var db = Substitute.For<AppIdentityDbContext>(dbOptions, valueGeneratorFactory, idGenerator);
        var gate = new PlatformAdminLocalApprovalGate(db, new UpperInvariantLookupNormalizer());

        // Act
        var result = await gate.ApplyAsync(
            tenantId: 1,
            authProvider: AuthProviders.Keycloak,
            externalSubjectId: "external-1",
            userId: null,
            mappedRoles: [SystemRole.Admin.Name],
            mappedPermissions: [Actions.Access.Hub],
            CancellationToken.None);

        // Assert
        result.Roles.Should().BeEquivalentTo([SystemRole.Admin.Name]);
        result.Permissions.Should().BeEquivalentTo([Actions.Access.Hub]);
    }
}
