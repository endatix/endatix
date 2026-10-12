using Endatix.Modules.Jobs.Persistence.Migrations.PostgreSql;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Endatix.Infrastructure.Tests.Data;

public sealed class CommittedModelsTests
{
    [Fact]
    public void IsCommittedModel_MigrationWithDesigner_ReturnsTrue()
    {
        // Arrange
        var migrationType = typeof(InitialBackgroundJobs);

        // Act
        var isCommittedModel = CommittedModels.IsCommittedModel(migrationType);

        // Assert
        isCommittedModel.Should().BeTrue();
    }

    [Fact]
    public void IsCommittedModel_MigrationWithoutDesigner_ReturnsFalse()
    {
        // Arrange
        var migrationType = typeof(MigrationWithoutDesigner);

        // Act
        var isCommittedModel = CommittedModels.IsCommittedModel(migrationType);

        // Assert
        isCommittedModel.Should().BeFalse();
    }

    [Migration("20000101000000_MigrationWithoutDesigner")]
    private sealed class MigrationWithoutDesigner : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No operations: only the missing designer matters here.
        }
    }
}
