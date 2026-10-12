using Endatix.Core.Abstractions;
using Endatix.Modules.Jobs.Domain;
using Endatix.Modules.Jobs.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Jobs.Tests.Persistence;

public class JobsSqlServerDbContextTests
{
    [Fact]
    public void Model_ConfiguredForSqlServer_BuildsTheJobsModel()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<JobsSqlServerDbContext>()
            .UseSqlServer("Server=localhost;Database=JobsTests")
            .Options;
        using var context = new JobsSqlServerDbContext(options, Substitute.For<ITenantContext>());

        // Act
        var model = context.Model;

        // Assert
        model.FindEntityType(typeof(BackgroundJob)).Should().NotBeNull();
        model.GetDefaultSchema().Should().Be(JobsPersistence.Schema);
    }

    [Fact]
    public void Model_ConfiguredForPostgreSql_ThrowsNotSupportedException()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<JobsSqlServerDbContext>()
            .UseNpgsql("Host=localhost;Database=jobs_tests")
            .Options;
        using var context = new JobsSqlServerDbContext(options, Substitute.For<ITenantContext>());

        // Act
        var buildModel = () => context.Model;

        // Assert
        buildModel.Should().Throw<NotSupportedException>()
            .WithMessage("JobsSqlServerDbContext is mapped for SQL Server only*Npgsql.EntityFrameworkCore.PostgreSQL*");
    }
}
