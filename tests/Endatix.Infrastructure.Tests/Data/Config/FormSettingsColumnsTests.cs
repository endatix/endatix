using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Tests.Features.Outbox;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Tests.Data.Config;

/// <summary>
/// Every form settings section is a JSON column on both providers, and a form never loads its settings.
/// </summary>
public sealed class FormSettingsColumnsTests
{
    [Fact]
    public void Sections_OnPostgreSql_AreJsonb()
    {
        using var context = AppDbContextModelInspectionFactory.CreatePostgreSqlAppDbContext();

        AssertSections(context, "jsonb");
    }

    [Fact]
    public void Sections_OnSqlServer_AreJson()
    {
        using var context = AppDbContextModelInspectionFactory.CreateSqlServerAppDbContext();

        AssertSections(context, "json");
    }

    [Fact]
    public void Form_HasNoNavigationToItsSettings()
    {
        using var context = AppDbContextModelInspectionFactory.CreatePostgreSqlAppDbContext();

        var form = context.Model.FindEntityType(typeof(Form))!;

        form.GetNavigations().Should().NotContain(navigation => navigation.TargetEntityType.ClrType == typeof(FormSettings));
        typeof(Form).GetProperties().Should().NotContain(property => property.PropertyType == typeof(FormSettings));
    }

    private static void AssertSections(AppDbContext context, string columnType)
    {
        var settings = context.Model.FindEntityType(typeof(FormSettings))!;
        var sections = typeof(FormSettings).GetProperties()
            .Where(property => property.Name.EndsWith("Json", StringComparison.Ordinal))
            .ToList();

        sections.Should().NotBeEmpty();
        foreach (var section in sections)
        {
            var column = settings.FindProperty(section.Name);
            column.Should().NotBeNull($"{section.Name} is stored");
            column!.GetColumnType().Should().Be(columnType, section.Name);
            column.IsNullable.Should().BeTrue($"{section.Name} is null until it is written");
        }
    }
}
