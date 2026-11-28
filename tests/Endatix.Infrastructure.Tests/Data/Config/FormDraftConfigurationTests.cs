using Endatix.Core.Entities;
using Endatix.Core.Versioning;
using Endatix.Infrastructure.Tests.Features.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Tests.Data.Config;

public sealed class FormDraftConfigurationTests
{
    [Theory]
    [InlineData("character(64)")]
    [InlineData("char(64)")]
    public void Both_hash_columns_match_the_canonical_hash(string columnType)
    {
        using var db = columnType == "char(64)"
            ? AppDbContextModelInspectionFactory.CreateSqlServerAppDbContext()
            : AppDbContextModelInspectionFactory.CreatePostgreSqlAppDbContext();

        foreach (var type in new[] { typeof(FormDraft), typeof(FormDraftOperation) })
        {
            var hash = db.Model.FindEntityType(type)!.FindProperty("ContentHash")!;
            hash.GetMaxLength().Should().Be(CanonicalContentHash.LENGTH);
            hash.IsUnicode().Should().BeFalse();
            hash.GetColumnType().Should().Be(columnType);
        }
    }
}
