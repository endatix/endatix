using Endatix.Core.Entities;
using Endatix.Infrastructure.Tests.Features.Outbox;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Tests.Data.Config;

public sealed class SubmissionRestrictionKeyIndexTests
{
    [Fact]
    public void PostgreSql_unique_RestrictionKey_index_excludes_soft_deleted_rows()
    {
        using var context = AppDbContextModelInspectionFactory.CreatePostgreSqlAppDbContext();

        var index = context.Model
            .FindEntityType(typeof(Submission))!
            .GetIndexes()
            .Single(i => i.GetDatabaseName() == "UX_Submissions_RestrictionKey");

        index.IsUnique.Should().BeTrue();
        index.GetFilter().Should().Be("\"RestrictionKey\" IS NOT NULL AND \"IsDeleted\" = false");
    }
}
