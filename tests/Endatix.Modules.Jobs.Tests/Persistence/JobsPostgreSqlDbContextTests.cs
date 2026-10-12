using Endatix.Core.Abstractions;
using Endatix.Modules.Jobs.Domain;
using Endatix.Modules.Jobs.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Jobs.Tests.Persistence;

public class JobsPostgreSqlDbContextTests
{
    [Fact]
    public void Model_ConfiguredForPostgreSql_BuildsTheJobsModel()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<JobsPostgreSqlDbContext>()
            .UseNpgsql("Host=localhost;Database=jobs_tests")
            .Options;
        using var context = new JobsPostgreSqlDbContext(options, Substitute.For<ITenantContext>());

        // Act
        var model = context.Model;

        // Assert
        model.FindEntityType(typeof(BackgroundJob)).Should().NotBeNull();
        model.GetDefaultSchema().Should().Be(JobsPersistence.Schema);
    }

    [Fact]
    public void Model_ConfiguredForSqlServer_ThrowsNotSupportedException()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<JobsPostgreSqlDbContext>()
            .UseSqlServer("Server=localhost;Database=JobsTests")
            .Options;
        using var context = new JobsPostgreSqlDbContext(options, Substitute.For<ITenantContext>());

        // Act
        var buildModel = () => context.Model;

        // Assert
        buildModel.Should().Throw<NotSupportedException>()
            .WithMessage("JobsPostgreSqlDbContext is mapped for PostgreSQL only*Microsoft.EntityFrameworkCore.SqlServer*");
    }
}
