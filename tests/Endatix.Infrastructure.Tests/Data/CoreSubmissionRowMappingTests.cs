using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Reporting.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Tests.Data;

/// <summary>
/// The Reporting contexts map core <c>Submissions</c> read-only as <see cref="CoreSubmissionRow"/>. The core
/// <see cref="AppDbContext"/> owns the table, so every mapped column must match its <see cref="Submission"/> mapping.
/// </summary>
public sealed class CoreSubmissionRowMappingTests
{
    // The core context sets no schema, so its tables are in the provider's default one.
    public static TheoryData<string, Type, string> ReportingContexts => new()
    {
        { "postgresql", typeof(ReportingPostgreSqlDbContext), "public" },
        { "sqlserver", typeof(ReportingSqlServerDbContext), "dbo" },
    };

    [Theory]
    [MemberData(nameof(ReportingContexts))]
    public void CoreSubmissionRow_MappedColumns_MatchTheCoreSubmissionsTable(
        string provider,
        Type reportingContextType,
        string defaultSchema)
    {
        // Arrange
        using var services = CommittedSnapshotDriftTests.ServicesFor(provider);
        var core = services.GetRequiredService<AppDbContext>().Model.FindEntityType(typeof(Submission))!;
        var row = ((DbContext)services.GetRequiredService(reportingContextType)).Model.FindEntityType(typeof(CoreSubmissionRow))!;

        // Act
        var rowColumns = Columns(row);
        var coreColumns = Columns(core);

        // Assert
        $"{row.GetSchema()}.{row.GetTableName()}".Should().Be($"{core.GetSchema() ?? defaultSchema}.{core.GetTableName()}");
        coreColumns.Should().Contain(rowColumns);
    }

    // The table's columns, owned types included, as "type null|not null".
    private static Dictionary<string, string> Columns(IEntityType entity) =>
        entity.Model.GetRelationalModel()
            .FindTable(entity.GetTableName()!, entity.GetSchema())!
            .Columns
            .ToDictionary(column => column.Name, column => $"{column.StoreType} {(column.IsNullable ? "null" : "not null")}");
}
