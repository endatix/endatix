using Endatix.Core.Entities;
using Endatix.Core.Versioning;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Tests.Features.Outbox;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Tests.Data.Config;

/// <summary>
/// The column lengths copy Core's constants, which Infrastructure cannot see; these tests tie the copies to them.
/// </summary>
public sealed class TenantBrandingVersionConfigurationTests
{
    [Fact]
    public void ContentHash_OnPostgreSql_IsTheHashLengthInAsciiCharacters()
    {
        using var context = AppDbContextModelInspectionFactory.CreatePostgreSqlAppDbContext();

        AssertColumns(context, "character(64)");
    }

    [Fact]
    public void ContentHash_OnSqlServer_IsTheHashLengthInAsciiCharacters()
    {
        using var context = AppDbContextModelInspectionFactory.CreateSqlServerAppDbContext();

        AssertColumns(context, "char(64)");
    }

    private static void AssertColumns(AppDbContext context, string contentHashColumnType)
    {
        var entityType = context.Model.FindEntityType(typeof(TenantBrandingVersion))!;
        var contentHash = entityType.FindProperty(nameof(TenantBrandingVersion.ContentHash))!;
        var source = entityType.FindProperty(nameof(TenantBrandingVersion.Source))!;

        contentHash.GetMaxLength().Should().Be(CanonicalContentHash.LENGTH);
        contentHash.IsUnicode().Should().BeFalse();
        contentHash.GetColumnType().Should().Be(contentHashColumnType);
        source.GetMaxLength().Should().Be(ContentVersionSources.MAX_LENGTH);
    }
}
