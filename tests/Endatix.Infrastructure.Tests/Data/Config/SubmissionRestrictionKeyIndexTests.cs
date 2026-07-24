using Endatix.Core.Entities;
using Endatix.Infrastructure.Tests.Features.Outbox;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Tests.Data.Config;

public sealed class SubmissionRestrictionKeyIndexTests
{
    [Fact]
    public void RestrictionKeyUniqueIndex_OnPostgreSql_ExcludesSoftDeletedRows()
    {
        using var context = AppDbContextModelInspectionFactory.CreatePostgreSqlAppDbContext();

        var index = context.Model
            .FindEntityType(typeof(Submission))!
            .GetIndexes()
            .Single(i => i.GetDatabaseName() == "UX_Submissions_RestrictionKey");

        index.IsUnique.Should().BeTrue();
        index.GetFilter().Should().Be("\"RestrictionKey\" IS NOT NULL AND \"IsDeleted\" = false");
    }

    [Fact]
    public void RestrictionKeyUniqueIndex_OnSqlServer_ExcludesSoftDeletedRows()
    {
        using var context = AppDbContextModelInspectionFactory.CreateSqlServerAppDbContext();

        var index = context.Model
            .FindEntityType(typeof(Submission))!
            .GetIndexes()
            .Single(i => i.GetDatabaseName() == "UX_Submissions_RestrictionKey");

        index.IsUnique.Should().BeTrue();
        index.GetFilter().Should().Be("[RestrictionKey] IS NOT NULL AND [IsDeleted] = 0");
    }
}
