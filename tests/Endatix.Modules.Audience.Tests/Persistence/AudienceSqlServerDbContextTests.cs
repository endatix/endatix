using Endatix.Core.Abstractions;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Endatix.Modules.Audience.Tests.Persistence;

public class AudienceSqlServerDbContextTests
{
    private const string NotDeleted = "[IsDeleted] = 0";

    [Fact]
    public void Model_ConfiguredForSqlServer_BuildsTheAudienceModel()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var model = context.Model;

        // Assert
        model.FindEntityType(typeof(Member)).Should().NotBeNull();
        model.GetDefaultSchema().Should().Be(AudiencePersistence.Schema);
    }

    [Fact]
    public void Model_ConfiguredForPostgreSql_ThrowsNotSupportedException()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AudienceSqlServerDbContext>()
            .UseNpgsql("Host=localhost;Database=audience_tests")
            .Options;
        using var context = new AudienceSqlServerDbContext(options, Substitute.For<ITenantContext>());

        // Act
        var buildModel = () => context.Model;

        // Assert
        buildModel.Should().Throw<NotSupportedException>()
            .WithMessage("AudienceSqlServerDbContext is mapped for SQL Server only*Npgsql.EntityFrameworkCore.PostgreSQL*");
    }

    [Fact]
    public void OnModelCreating_SqlServerContext_MapsConstraintsIndexesAndJson()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var model = context.GetService<IDesignTimeModel>().Model;

        // Assert
        CheckConstraintsOf(model).Should().BeEquivalentTo(
            "CK_Imports_TenantId", "CK_Links_TenantId", "CK_Members_TenantId", "CK_Memberships_TenantId",
            "CK_Properties_TenantId", "CK_PropertyValues_TenantId", "CK_Settings_TenantId");
        model.GetEntityTypes().SelectMany(type => type.GetCheckConstraints())
            .Should().OnlyContain(constraint => constraint.Sql == "[TenantId] > 0");
        FilteredIndexesOf(model).Should().BeEquivalentTo(
        [
            new IndexShape("IX_Links_Membership", "TenantId,FormId,MembershipId", true),
            new IndexShape("IX_Links_TokenHash", "TokenHash", true),
            new IndexShape("IX_Members_Identifier", "TenantId,NormalizedIdentifier", true),
            new IndexShape("IX_Memberships_Form", "TenantId,FormId", false),
            new IndexShape("IX_Memberships_Member", "TenantId,FormId,MemberId", true),
            new IndexShape("IX_Properties_Form", "TenantId,FormId", false),
            new IndexShape("IX_Properties_VariableName", "TenantId,FormId,VariableName", true),
            new IndexShape("IX_PropertyValues_Cell", "TenantId,MembershipId,PropertyId", true),
            new IndexShape("IX_PropertyValues_Property", "TenantId,PropertyId", false),
            new IndexShape("IX_Settings_Tenant", "TenantId", true),
        ]);
        model.FindEntityType(typeof(Property))!.FindProperty(nameof(Property.ChoicesJson))!
            .GetColumnType().Should().Be("json");
    }

    [Fact]
    public void OnModelCreating_SqlServerContext_ComparesTheMatchKeyByCodePoint()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var collation = context.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(Member))!
            .FindProperty(nameof(Member.NormalizedIdentifier))!
            .GetCollation();

        // Assert
        collation.Should().Be("Latin1_General_100_BIN2");
    }

    private static AudienceSqlServerDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AudienceSqlServerDbContext>()
            .UseSqlServer("Server=localhost;Database=AudienceTests")
            .Options;
        return new AudienceSqlServerDbContext(options, Substitute.For<ITenantContext>());
    }

    private static IEnumerable<string> CheckConstraintsOf(IModel model) =>
        model.GetEntityTypes().SelectMany(type => type.GetCheckConstraints()).Select(constraint => constraint.Name!);

    private static IEnumerable<IndexShape> FilteredIndexesOf(IModel model) =>
        model.GetEntityTypes()
            .SelectMany(type => type.GetIndexes())
            .Where(index => index.GetFilter() == NotDeleted)
            .Select(index => new IndexShape(
                index.GetDatabaseName()!,
                string.Join(',', index.Properties.Select(property => property.Name)),
                index.IsUnique));

    private sealed record IndexShape(string Name, string Columns, bool IsUnique);
}
