using Endatix.Core.Abstractions;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Tests.Persistence;

public class AudiencePostgreSqlDbContextTests
{
    [Fact]
    public void Model_ConfiguredForPostgreSql_BuildsTheAudienceModel()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AudiencePostgreSqlDbContext>()
            .UseNpgsql("Host=localhost;Database=audience_tests")
            .Options;
        using var context = new AudiencePostgreSqlDbContext(options, Substitute.For<ITenantContext>());

        // Act
        var model = context.Model;

        // Assert
        model.FindEntityType(typeof(Member)).Should().NotBeNull();
        model.GetDefaultSchema().Should().Be(AudiencePersistence.Schema);
    }

    [Fact]
    public void Model_ConfiguredForSqlServer_ThrowsNotSupportedException()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AudiencePostgreSqlDbContext>()
            .UseSqlServer("Server=localhost;Database=AudienceTests")
            .Options;
        using var context = new AudiencePostgreSqlDbContext(options, Substitute.For<ITenantContext>());

        // Act
        var buildModel = () => context.Model;

        // Assert
        buildModel.Should().Throw<NotSupportedException>()
            .WithMessage("AudiencePostgreSqlDbContext is mapped for PostgreSQL only*Microsoft.EntityFrameworkCore.SqlServer*");
    }
}
